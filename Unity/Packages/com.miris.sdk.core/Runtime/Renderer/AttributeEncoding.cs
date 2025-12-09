// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine.Experimental.Rendering;

namespace Miris.Runtime
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
        RGBA_Compressed_ASTC_4x4_LDR,
        RGBA_Compressed_ASTC_5x5_LDR,
        RGBA_Compressed_ASTC_6x6_LDR,
        RGBA_Compressed_ASTC_8x8_LDR,
        RGBA_Compressed_ASTC_10x10_LDR,
        RGBA_Compressed_ASTC_12x12_LDR,
        RGBA_Compressed_ASTC_4x4_HDR,
        RGBA_Compressed_ASTC_5x5_HDR,
        RGBA_Compressed_ASTC_6x6_HDR,
        RGBA_Compressed_ASTC_8x8_HDR,
        RGBA_Compressed_ASTC_10x10_HDR,
        RGBA_Compressed_ASTC_12x12_HDR
    }

    // Extends the AttributeEncoding enum with methods
    static public class AttributeEncodingExtensions
    {

        static public GraphicsFormat ToGraphicsFormat(this AttributeEncoding encoding)
        {
            return encoding switch
            {
                AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => GraphicsFormat.RGBA_ASTC4X4_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_5x5_LDR => GraphicsFormat.RGBA_ASTC5X5_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_6x6_LDR => GraphicsFormat.RGBA_ASTC6X6_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_8x8_LDR => GraphicsFormat.RGBA_ASTC8X8_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_10x10_LDR => GraphicsFormat.RGBA_ASTC10X10_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_12x12_LDR => GraphicsFormat.RGBA_ASTC12X12_UNorm,
                AttributeEncoding.RGBA_Compressed_ASTC_4x4_HDR => GraphicsFormat.RGBA_ASTC4X4_UFloat,
                AttributeEncoding.RGBA_Compressed_ASTC_5x5_HDR => GraphicsFormat.RGBA_ASTC5X5_UFloat,
                AttributeEncoding.RGBA_Compressed_ASTC_6x6_HDR => GraphicsFormat.RGBA_ASTC6X6_UFloat,
                AttributeEncoding.RGBA_Compressed_ASTC_8x8_HDR => GraphicsFormat.RGBA_ASTC8X8_UFloat,
                AttributeEncoding.RGBA_Compressed_ASTC_10x10_HDR => GraphicsFormat.RGBA_ASTC10X10_UFloat,
                AttributeEncoding.RGBA_Compressed_ASTC_12x12_HDR => GraphicsFormat.RGBA_ASTC12X12_UFloat,
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
