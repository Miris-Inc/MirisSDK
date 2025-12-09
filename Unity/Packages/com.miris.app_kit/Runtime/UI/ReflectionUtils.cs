using UnityEngine;

using System;
using System.Reflection;

namespace Miris.Runtime
{
    public class ReflectionUtils
    {
        static public void GetFloatFieldRange(Type classType, string fieldName, out float min, out float max)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            min = range.min;
            max = range.max;
        }
    }
}