// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity Engine
using UnityEngine.Experimental.Rendering;

// Unity packages
using Unity.Mathematics;

namespace Aqua.Runtime
{
    // Various ways to encode/compress the underlying data.
    // Not every encoding is supported by its semantic.
    public enum AttributeEncoding : int
    {
        // Linear array formats
        Float32 = 0, // 4 bytes - F32
        Float32x3, // 12 bytes - F32.F32.F32
        Float32x4, // 16 bytes - F32.F32.F32.F32
        Float16x3, // 6 bytes - F16.F16.F16
        Float16x4, // 8 bytes - F16.F16.F16.F16
        UInt16x3, // 6 bytes - U16.U16.U16

        // Compressed texture formats
        // See https://docs.unity3d.com/Manual/class-TextureImporterOverride.html
        RGBA_Compressed_ASTC_4x4_LDR // 1 byte
    }

    // Extends the AttributeEncoding enum with methods
    static public class AttributeEncodingExtensions
    {
        static public int GetBytesPerElement(this AttributeEncoding encoding)
        {
            return encoding switch
            {
                AttributeEncoding.Float32 => 4,
                AttributeEncoding.Float32x3 => 12,
                AttributeEncoding.Float32x4 => 16,
                AttributeEncoding.Float16x3 => 6,
                AttributeEncoding.Float16x4 => 8,
                AttributeEncoding.UInt16x3 => 6,
                AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(AttributeEncoding), encoding.ToString(), null)
            };
        }

        static unsafe public void WriteFloat3(this AttributeEncoding encoding, float3 value, byte* outputPtr)
        {
            switch (encoding)
            {
                case AttributeEncoding.Float32x3:
                    {
                        ((float*)outputPtr)[0] = value.x;
                        ((float*)outputPtr)[1] = value.y;
                        ((float*)outputPtr)[2] = value.z;
                        break;
                    }
                default:
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(AttributeEncoding) + " " + encoding.ToString() + " does not support writing float3's"
                        );
                    }
            }
        }

        static unsafe public void WriteFloat4(this AttributeEncoding encoding, float4 value, byte* outputPtr)
        {
            switch (encoding)
            {
                case AttributeEncoding.Float32x4:
                    {
                        ((float*)outputPtr)[0] = value.x;
                        ((float*)outputPtr)[1] = value.y;
                        ((float*)outputPtr)[2] = value.z;
                        ((float*)outputPtr)[3] = value.w;
                        break;
                    }
                default:
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(AttributeEncoding) + " " + encoding.ToString() + " does not support writing float4's"
                        );
                    }
            }
        }

        static public GraphicsFormat ToGraphicsFormat(this AttributeEncoding encoding)
        {
            return encoding switch
            {
                AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => GraphicsFormat.RGBA_ASTC4X4_UNorm,
                _ => GraphicsFormat.None
            };
        }

        // Should this encoding be stored as a texture?
        static public bool IsTextureEncoding(this AttributeEncoding encoding)
        {
            return encoding.ToGraphicsFormat() != GraphicsFormat.None;
        }
    }
}
