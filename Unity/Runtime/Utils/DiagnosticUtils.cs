// Copyright © 2026 Miris, Inc. All rights reserved.

using System;

namespace Miris.Runtime
{
    public class DiagnosticUtils
    {
        static public void ValidateEnum<T>(int intValue) where T: Enum
        {
            // Validates that our enum is within the range of [0, Count)
            if (!Enum.IsDefined(typeof(T), intValue))
            {
                throw new IndexOutOfRangeException(typeof(T).ToString() + " '" + intValue + "' is invalid");
            }
        }
    }
}
