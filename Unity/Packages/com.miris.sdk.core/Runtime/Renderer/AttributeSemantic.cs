// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;

namespace Miris.Runtime
{
    // For associating a piece of data with its intended usage / purpose,
    // and determines how it is processed throughout the system.
    public enum AttributeSemantic : int
    {
        Position = 0,
        BlockBounds,
        Scale,
        Orientation,
        Color,
        SHCoefficients

        // Extend this with others like, SphericalHarmonics
    }

    // Extends the AttributeSemantic enum with methods
    static public class AttributeSemanticExtensions
    {
        public static bool SupportsEncoding(this AttributeSemantic semantic, AttributeEncoding encoding)
        {
            switch (semantic)
            {
                case AttributeSemantic.Position:
                    {
                        return encoding switch
                        {
                            AttributeEncoding.Float32x3 => true,
                            AttributeEncoding.Float16x3 => true,
                            AttributeEncoding.Float16x4 => true,
                            AttributeEncoding.UInt16x3 => true,
                            _ => false
                        };
                    }
                case AttributeSemantic.BlockBounds:
                    {
                        return encoding switch
                        {
                            AttributeEncoding.Float32 => true,
                            _ => false
                        };
                    }

                case AttributeSemantic.Scale:
                    {
                        return encoding switch
                        {
                            AttributeEncoding.Float32x3 => true,
                            AttributeEncoding.Float16x3 => true,
                            AttributeEncoding.Float16x4 => true,
                            AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => true,
                            _ => false
                        };
                    }

                case AttributeSemantic.Orientation:
                    {
                        return encoding switch
                        {
                            AttributeEncoding.Float32x4 => true,
                            AttributeEncoding.Float16x4 => true,
                            AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => true,
                            AttributeEncoding.RGBA_Compressed_ASTC_4x4_HDR => true,
                            _ => false
                        };
                    }

                case AttributeSemantic.Color:
                    {
                        return encoding switch
                        {
                            AttributeEncoding.Float32x4 => true,
                            AttributeEncoding.Float16x4 => true,
                            AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR => true,
                            AttributeEncoding.RGBA_Compressed_ASTC_4x4_HDR => true,
                            _ => false
                        };
                    }
                
                case AttributeSemantic.SHCoefficients:
                {
                    return encoding switch
                    {
                        AttributeEncoding.Float32x3 => true,
                        AttributeEncoding.Float16x3 => true, 
                        _ => false
                    };
                }

                default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(AttributeSemantic), semantic.ToString(), null);
                    }
            }
        }

        static public void ValidateEncodingSupport(this AttributeSemantic semantic, AttributeEncoding encoding)
        {
            if (!semantic.SupportsEncoding(encoding))
            {
                throw new ArgumentException(
                    nameof(AttributeSemantic) + " " + semantic.ToString() +
                    " does not support " +
                    nameof(AttributeEncoding) + " " + encoding.ToString()
                );
            }
        }

        public static AttributeEncoding GetDefaultEncoding(this AttributeSemantic semantic)
        {
            return semantic switch
            {
                AttributeSemantic.Position => AttributeEncoding.Float32x3,
                AttributeSemantic.Scale => AttributeEncoding.Float32x3, 
                AttributeSemantic.Orientation => AttributeEncoding.Float32x4,
                AttributeSemantic.Color => AttributeEncoding.Float32x4,
                AttributeSemantic.BlockBounds => AttributeEncoding.Float32,
                AttributeSemantic.SHCoefficients => AttributeEncoding.Float32x3,
                _ => throw new ArgumentOutOfRangeException(nameof(AttributeSemantic), semantic.ToString(), null)
            };
        }
    }
}
