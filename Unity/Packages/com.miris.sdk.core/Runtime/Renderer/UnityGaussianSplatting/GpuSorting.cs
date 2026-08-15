using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace GaussianSplatting.Runtime
{
    // GPU (uint key, uint payload) 8 bit-LSD radix sort, using reduce-then-scan
    // Copyright Thomas Smith 2024, MIT license
    // https://github.com/b0nes164/GPUSorting

    public class DeviceRadixSort
    {
        //The size of a threadblock partition in the sort
        const uint DEVICE_RADIX_SORT_PARTITION_SIZE = 3840;

        //The size of our radix in bits
        const uint DEVICE_RADIX_SORT_BITS = 8;

        //Number of digits in our radix, 1 << DEVICE_RADIX_SORT_BITS
        const uint DEVICE_RADIX_SORT_RADIX = 256;

        //Number of sorting passes required to sort a 32bit key, KEY_BITS / DEVICE_RADIX_SORT_BITS
        const uint DEVICE_RADIX_SORT_PASSES = 4;

        //Keywords to enable for the shader
        private LocalKeyword m_keyFloatKeyword;
        private LocalKeyword m_payloadUintKeyword;
        private LocalKeyword m_ascendKeyword;
        private LocalKeyword m_sortPairKeyword;
        private LocalKeyword m_vulkanKeyword;

        public struct Args
        {
            public uint             count;
            public ComputeBuffer   inputKeys;
            public ComputeBuffer   inputValues;
            public SupportResources resources;
            internal int workGroupCount;
        }

        public struct SupportResources
        {
            public uint countLimit;
            public ComputeBuffer altBuffer;
            public ComputeBuffer altPayloadBuffer;
            public ComputeBuffer passHistBuffer;
            public ComputeBuffer globalHistBuffer;

            public static SupportResources Load(uint count)
            {
                //This is threadBlocks * DEVICE_RADIX_SORT_RADIX
                uint scratchBufferSize = DivRoundUp(count, DEVICE_RADIX_SORT_PARTITION_SIZE) * DEVICE_RADIX_SORT_RADIX; 
                uint reducedScratchBufferSize = DEVICE_RADIX_SORT_RADIX * DEVICE_RADIX_SORT_PASSES;

                var target = ComputeBufferType.Structured;
                var resources = new SupportResources
                {
                    countLimit = count,
                    altBuffer = new ComputeBuffer((int)count, 4, target) { name = "DeviceRadixAlt"}, 
                    altPayloadBuffer = new ComputeBuffer((int)count, 4, target) { name = "DeviceRadixAltPayload" },
                    passHistBuffer = new ComputeBuffer((int)scratchBufferSize, 4, target) { name = "DeviceRadixPassHistogram" },
                    globalHistBuffer = new ComputeBuffer((int)reducedScratchBufferSize, 4, target) { name = "DeviceRadixGlobalHistogram" },
                };
                return resources;
            }

            public void Dispose()
            {
                altBuffer?.Dispose();
                altPayloadBuffer?.Dispose();
                passHistBuffer?.Dispose();
                globalHistBuffer?.Dispose();

                altBuffer = null;
                altPayloadBuffer = null;
                passHistBuffer = null;
                globalHistBuffer = null;
            }
        }

        readonly ComputeShader m_CS;
        readonly int m_kernelInitDeviceRadixSort = -1;
        readonly int m_kernelUpsweep = -1;
        readonly int m_kernelScan = -1;
        readonly int m_kernelDownsweep = -1;

        readonly bool m_Valid;

        public bool Valid => m_Valid;

        public DeviceRadixSort(ComputeShader cs)
        {
            m_CS = cs;
            if (cs)
            {
                m_kernelInitDeviceRadixSort = cs.FindKernel("InitDeviceRadixSort");
                m_kernelUpsweep = cs.FindKernel("Upsweep");
                m_kernelScan = cs.FindKernel("Scan");
                m_kernelDownsweep = cs.FindKernel("Downsweep");
            }

            m_Valid = m_kernelInitDeviceRadixSort >= 0 &&
                      m_kernelUpsweep >= 0 &&
                      m_kernelScan >= 0 &&
                      m_kernelDownsweep >= 0;
            if (m_Valid)
            {
                if (!cs.IsSupported(m_kernelInitDeviceRadixSort) ||
                    !cs.IsSupported(m_kernelUpsweep) ||
                    !cs.IsSupported(m_kernelScan) ||
                    !cs.IsSupported(m_kernelDownsweep))
                {
                    m_Valid = false;
                }
            }

            m_keyFloatKeyword = new LocalKeyword(cs, "KEY_FLOAT");
            m_payloadUintKeyword = new LocalKeyword(cs, "PAYLOAD_UINT");
            m_ascendKeyword = new LocalKeyword(cs, "SHOULD_ASCEND");
            m_sortPairKeyword = new LocalKeyword(cs, "SORT_PAIRS");
            m_vulkanKeyword = new LocalKeyword(cs, "VULKAN");

            cs.EnableKeyword(m_keyFloatKeyword);
            cs.EnableKeyword(m_payloadUintKeyword);
            //cs.EnableKeyword(m_ascendKeyword);
            cs.EnableKeyword(m_sortPairKeyword);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
                cs.EnableKeyword(m_vulkanKeyword);
            else
                cs.DisableKeyword(m_vulkanKeyword);
        }

        static uint DivRoundUp(uint x, uint y) => (x + y - 1) / y;

        //Can we remove the last 4 padding without breaking?
        struct SortConstants
        {
            public uint numKeys;                        // The number of keys to sort
            public uint radixShift;                     // The radix shift value for the current pass
            public uint threadBlocks;                   // threadBlocks
            public uint padding0;                       // Padding - unused
        }

        public void Dispatch(CommandBuffer cmd, Args args, ComputeBuffer indirectDrawBuffer)
        {
            Assert.IsTrue(Valid);

            ComputeBuffer srcKeyBuffer = args.inputKeys;
            ComputeBuffer srcPayloadBuffer = args.inputValues;
            ComputeBuffer dstKeyBuffer = args.resources.altBuffer;
            ComputeBuffer dstPayloadBuffer = args.resources.altPayloadBuffer;

            SortConstants constants = default;
            constants.numKeys = args.count;
            constants.threadBlocks = DivRoundUp(args.count, DEVICE_RADIX_SORT_PARTITION_SIZE);

            // Setup overall constants
            cmd.SetComputeIntParam(m_CS, "e_numKeys", (int)constants.numKeys);
            cmd.SetComputeIntParam(m_CS, "e_threadBlocks", (int)constants.threadBlocks);

            //Set statically located buffers
            //Upsweep
            cmd.SetComputeBufferParam(m_CS, m_kernelUpsweep, "b_passHist", args.resources.passHistBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelUpsweep, "b_globalHist", args.resources.globalHistBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelUpsweep, "indirectDrawBuffer", indirectDrawBuffer );

            //Scan
            cmd.SetComputeBufferParam(m_CS, m_kernelScan, "b_passHist", args.resources.passHistBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelScan, "indirectDrawBuffer", indirectDrawBuffer);

            //Downsweep
            cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_passHist", args.resources.passHistBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_globalHist", args.resources.globalHistBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "indirectDrawBuffer", indirectDrawBuffer);

            //Clear the global histogram
            cmd.SetComputeBufferParam(m_CS, m_kernelInitDeviceRadixSort, "b_globalHist", args.resources.globalHistBuffer);
            cmd.DispatchCompute(m_CS, m_kernelInitDeviceRadixSort, 1, 1, 1);

            // Execute the sort algorithm in 8-bit increments
            for (constants.radixShift = 0; constants.radixShift < 32; constants.radixShift += DEVICE_RADIX_SORT_BITS)
            {
                cmd.SetComputeIntParam(m_CS, "e_radixShift", (int)constants.radixShift);

                //Upsweep
                cmd.SetComputeBufferParam(m_CS, m_kernelUpsweep, "b_sort", srcKeyBuffer);
                cmd.DispatchCompute(m_CS, m_kernelUpsweep, (int)constants.threadBlocks, 1, 1);

                // Scan
                cmd.DispatchCompute(m_CS, m_kernelScan, (int)DEVICE_RADIX_SORT_RADIX, 1, 1);

                // Downsweep
                cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_sort", srcKeyBuffer);
                cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_sortPayload", srcPayloadBuffer);
                cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_alt", dstKeyBuffer);
                cmd.SetComputeBufferParam(m_CS, m_kernelDownsweep, "b_altPayload", dstPayloadBuffer);
                cmd.DispatchCompute(m_CS, m_kernelDownsweep, (int)constants.threadBlocks, 1, 1);

                // Swap
                (srcKeyBuffer, dstKeyBuffer) = (dstKeyBuffer, srcKeyBuffer);
                (srcPayloadBuffer, dstPayloadBuffer) = (dstPayloadBuffer, srcPayloadBuffer);
            }
        }
    }
}
