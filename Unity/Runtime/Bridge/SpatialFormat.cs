// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Miris.Runtime
{
#endif

#if __cplusplus
public enum class UpAxis 
#else
public enum UpAxis 
#endif
{ X, Y, Z };

#if __cplusplus
public enum class MatrixOrder 
#else
public enum MatrixOrder 
#endif
{ RowMajor, ColumnMajor };


#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct SpatialFormat
    {
#if __cplusplus
        SpatialFormat()
        {
            m_metersPerUnit = 1.0f;
            m_upAxis = UpAxis::Y;
            m_matrixOrder = MatrixOrder::RowMajor;
        }
#endif
        public double m_metersPerUnit;
        public UpAxis m_upAxis;
        public MatrixOrder m_matrixOrder;
    };

#if USING_CSHARP
} // Aqua.Runtime
#else

#undef public
#endif
