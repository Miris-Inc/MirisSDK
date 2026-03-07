// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Miris.Runtime
{
    // Helper class for enqueing a buffer -> buffer copy on the GPU
    public class BufferCopier
    {
        private List<BufferCopyCommand> m_copyCommands = new();
        
        private ComputeShader m_copyShader; 
        private int m_copyKernel;

        private static readonly int CopyBufferSrc = Shader.PropertyToID("_sourceBuffer");
        private static readonly int CopyBufferDst = Shader.PropertyToID("_destBuffer");

        private static readonly int CopyBufferLength = Shader.PropertyToID("_Length");
        private static readonly int CopyBufferSrcOffset = Shader.PropertyToID("_SrcOffset");
        private static readonly int CopyBufferDestOffset = Shader.PropertyToID("_DestOffset");

        static readonly ProfilerMarker PerformCopyProfileMarker = new ProfilerMarker("PerformCopy");

        public BufferCopier()
        {
            m_copyShader = (ComputeShader)Resources.Load("Shaders/CopyBuffer");
            m_copyKernel = m_copyShader.FindKernel("CopyBufferMain");
        }

        public struct BufferCopyCommand
        {
            public IGpuBuffer sourceBuffer;
            public IGpuBuffer destBuffer;
            public int sourceOffset;
            public int destOffset;
            public int length;
        }


        // Add a new buffer copy to be triggered during rendering
        public void EnqueueBufferCopy(IGpuBuffer sourceBuffer, IGpuBuffer destBuffer, int sourceOffset,
            int destOffset,
            int length)
        {
            BufferCopyCommand command;

            command.sourceBuffer = sourceBuffer;
            command.destBuffer = destBuffer;
            
            Debug.Assert( sourceOffset % 4 == 0, "sourceOffset has to be 4 byte aligned as our buffers are sizeof(int) strided");
            Debug.Assert( destOffset % 4 == 0, "destOffset has to be 4 byte aligned as our buffers are sizeof(int) strided");
            Debug.Assert( length % 4 == 0, "length has to be 4 byte aligned as our buffers are sizeof(int) strided");
            
            command.sourceOffset = sourceOffset / 4;
            command.destOffset = destOffset / 4;
            command.length = length / 4;
            m_copyCommands.Add(command);
        }

        // Add all enqueued copy commands to the commandBuffer
        public void Execute(CommandBuffer commandBuffer)
        {
            if (m_copyCommands.Count == 0)
            {
                return;
            }

            // pre-calculate the compute kernel thread group sizes
            m_copyShader.GetKernelThreadGroupSizes( m_copyKernel, out uint threadsPerGroupX, out uint threadsPerGroupY, out uint threadsPerGroupZ );
            
            //m_copyCommands.Sort((BufferCopyCommand a, BufferCopyCommand b) => a.destBuffer.GetHashCode().CompareTo(b.destBuffer.GetHashCode()));
            foreach (BufferCopyCommand command in m_copyCommands)
            {
                PerformCopyProfileMarker.Begin();

                Debug.Assert(command.sourceBuffer != null);
                Debug.Assert(command.destBuffer != null);
                Debug.Assert(command.length > 0);
                
                command.sourceBuffer.SetBufferOnComputeShader(commandBuffer, m_copyShader, m_copyKernel, CopyBufferSrc);
                command.destBuffer.SetBufferOnComputeShader(commandBuffer, m_copyShader, m_copyKernel, CopyBufferDst);

                commandBuffer.SetComputeIntParam(m_copyShader, CopyBufferLength, (int) command.length);
                commandBuffer.SetComputeIntParam(m_copyShader, CopyBufferSrcOffset, (int) command.sourceOffset);
                commandBuffer.SetComputeIntParam(m_copyShader, CopyBufferDestOffset, (int) command.destOffset);

                var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount( 
                    ( threadsPerGroupX, threadsPerGroupY, threadsPerGroupZ ),
                    (int)command.length
                );

                //Dispatch the compute shader
                commandBuffer.DispatchCompute(m_copyShader, m_copyKernel, threadGroupCountX, 1, 1);

                PerformCopyProfileMarker.End();
            }
            
            m_copyCommands.Clear();
        }

    }
}