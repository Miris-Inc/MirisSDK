// Copyright © 2024 Miris. All rights reserved.

using System;

namespace Miris.Runtime
{
    public struct AquaHash : IEquatable<AquaHash>
    {
        private UInt64 m_hash0;
        private UInt64 m_hash1;
        private int m_hash;

        public AquaHash(int a, int b, int c, int d)
        {
            m_hash0 = ((UInt64)a) << 32 | ((UInt64)b);
            m_hash1 = ((UInt64)c) << 32 | ((UInt64)d);
            m_hash = GenerateHashCode(m_hash0, m_hash1);
        }
        
        public AquaHash(UInt64 a, UInt64 b)
        {
            m_hash0 = a;
            m_hash1 = b;
            m_hash = GenerateHashCode(m_hash0, m_hash1);
        }
        
        // Implement IEquatable<T> for better comparison
        public override bool Equals(object obj)
        {
            return obj is AquaHash other && Equals(other);
        }

        public bool Equals(AquaHash other)
        {
            return m_hash0 == other.m_hash0 && m_hash1 == other.m_hash1;
        }

        // Override GetHashCode for correct dictionary behavior
        public override readonly int GetHashCode()
        {
            return m_hash;
        }

        private static int GenerateHashCode(UInt64 hash0, UInt64 hash1)
        {
            return hash0.GetHashCode() ^ hash1.GetHashCode();
        }

        public Tuple<UInt32, UInt32, UInt32, UInt32> ToTuple()
        {
            return Tuple.Create((UInt32)(m_hash0 >> 32), (UInt32)m_hash0, (UInt32)(m_hash1 >> 32), (UInt32)m_hash1);
        }
        

    }
}