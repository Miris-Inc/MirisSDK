// Copyright © 2024 Miris. All rights reserved.

using System;
using UnityEngine;

namespace Aqua.Runtime
{
    public class LinearAllocator
    {
        public LinearAllocator(UInt64 size)
        {
            m_size = size;
        }

        private UInt64 ComputeFreeOffsetWithAlignment(int alignment)
        {
            UInt64 freeOffset = m_freeOffset;
            if (alignment > 1)
            {
                UInt64 r = m_freeOffset % (UInt64)alignment;
                UInt64 aligned = m_freeOffset / (UInt64)alignment;
                aligned += (r != 0UL ? 1UL : 0UL);

                freeOffset = aligned * (UInt64)alignment;
            }
            return freeOffset;
        }

        public Int64 Allocate(UInt64 size, int alignment = 16)
        {
            UInt64 freeOffset = ComputeFreeOffsetWithAlignment(alignment);
            UInt64 newFreeOffset = freeOffset + size;
            if (newFreeOffset >= m_size)
            {
                return -1;
            }

            m_numAllocations++;

            Int64 offset = (Int64)freeOffset;
            m_freeOffset = newFreeOffset;

            AquaUnityApi.PlotMetric("GPU Mem free", (long)(m_size - m_freeOffset));
            AquaUnityApi.PlotMetric("GPU Mem Allocs", m_numAllocations);
            return offset;
        }

        public bool CanAllocate(UInt64 size, int alignment = 16)
        {
            UInt64 freeOffset = ComputeFreeOffsetWithAlignment(alignment);
            UInt64 newFreeOffset = freeOffset + size;
            if (newFreeOffset >= m_size)
            {
                return false;
            }
            return true;
        }

        public void DumpStats()
        {
            Int64 freeMem = (Int64)(m_size - m_freeOffset);
            Debug.Log($"num allocations:{m_numAllocations} free mem:{freeMem}");
        }

        private int m_numAllocations = 0;
        private UInt64 m_size;
        private UInt64 m_freeOffset = 0;
        
    }
}