using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Aqua.Runtime
{
    public class PinnedMemoryArray<T> : IDisposable where T : struct
    {
        private GCHandle m_handle;
        private T[] m_array;

        public int Length => m_array.Length;

        public int TotalBytes => Length * Marshal.SizeOf<T>();

        public PinnedMemoryArray(int length)
        {
            m_array = new T[length];
            m_handle = GCHandle.Alloc(m_array, GCHandleType.Pinned);
        }

        // Get the NativeArray wrapping our pinned memory
        public NativeArray<byte> GetNativeArray()
        {
            return DataFormatUtils.WrapVoidPtrWithNativeArray(m_handle.AddrOfPinnedObject(), TotalBytes);
        }

        public void Dispose()
        {
            if (m_handle.IsAllocated)
            {
                Debug.WriteLine($"Disposing buffer {Length} bytes");
                m_handle.Free();
            }
        }
    }
}
