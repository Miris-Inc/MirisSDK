// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Text.RegularExpressions;

namespace Miris.Editor
{
    public sealed class SdkVersion : IComparable<SdkVersion>
    {
        private static readonly Regex s_componentPattern = new Regex(@"^(0|[1-9][0-9]*)$");

        private readonly string[] m_components;

        private SdkVersion(string[] components)
        {
            m_components = components;
        }

        public static bool TryParse(string input, out SdkVersion result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] parts = input.Split('.');
            if (parts.Length != 3)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!s_componentPattern.IsMatch(parts[i]))
                    return false;
            }

            result = new SdkVersion(parts);
            return true;
        }

        public int CompareTo(SdkVersion other)
        {
            if (other is null) return 1;
            for (int i = 0; i < m_components.Length; i++)
            {
                int maxLen = Math.Max(m_components[i].Length, other.m_components[i].Length);
                int cmp = string.Compare(
                    m_components[i].PadLeft(maxLen, '0'),
                    other.m_components[i].PadLeft(maxLen, '0'),
                    StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
            return 0;
        }

        public override string ToString() =>
            $"{m_components[0]}.{m_components[1]}.{m_components[2]}";

        public static bool operator >(SdkVersion a, SdkVersion b) => a.CompareTo(b) > 0;
        public static bool operator <(SdkVersion a, SdkVersion b) => a.CompareTo(b) < 0;
    }
}
