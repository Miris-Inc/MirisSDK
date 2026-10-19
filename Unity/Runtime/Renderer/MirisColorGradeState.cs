// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Miris.Runtime
{
    // The grade runs as ONE full-screen pass after everything the app has rendered, so it covers
    // Miris content, Unity geometry and Shark's composite alike \
    public static class MirisColorGradeState
    {
        public const string c_shaderResourcePath = "Shaders/MirisColorGrade";

        private const string c_lutKeyword = "MIRIS_COLOR_GRADING_LUT";

        private static class ShaderIds
        {
            public static readonly int MirisLutTex = Shader.PropertyToID("_MirisLutTex");
            public static readonly int MirisLutParams = Shader.PropertyToID("_MirisLutParams");
            public static readonly int MirisLutStrength = Shader.PropertyToID("_MirisLutStrength");
        }

        private static Texture2D m_lut;
        private static float m_strength = 1.0f;
        private static Vector4 m_lutParams = Vector4.zero;

        private static Texture2D m_validatedLut;

        // The grade is one full-screen pass over one process-wide state, so exactly one component
        // owns it. Without an owner, two enabled components overwrite each other's LUT and either
        // one's OnDisable takes the grade away from the other.
        private static MirisColorGrade m_owner;

        public static Texture2D Lut => m_lut;

        public static float Strength => m_strength;

        public static bool IsActive => m_lut != null;

        public static bool IsOwner(MirisColorGrade component) => m_owner == component;

        /// <summary>
        /// Claims the grade for <paramref name="component"/> and sets it. The first component to
        /// ask owns the grade until it releases it.
        /// </summary>
        /// <returns>False if another component already owns the grade, leaving the state alone.</returns>
        public static bool SetFrom(MirisColorGrade component, Texture2D lut, float strength)
        {
            // Unity's null overload: an owner destroyed without releasing compares equal to null,
            // so the grade is claimable again rather than stuck.
            if (m_owner == null)
            {
                m_owner = component;
            }
            else if (m_owner != component)
            {
                return false;
            }

            Set(lut, strength);
            return true;
        }

        /// <summary>
        /// Releases the grade if <paramref name="component"/> owns it. A non-owner's call does
        /// nothing, so disabling a second component cannot clear the owner's grade.
        /// </summary>
        public static void ClearFrom(MirisColorGrade component)
        {
            if (m_owner != component)
            {
                return;
            }

            m_owner = null;
            Set(null, m_strength);
        }

        /// <summary>
        /// Drops the grade and its owner. For tests and editor tooling driving the state directly;
        /// components release through <see cref="ClearFrom"/> instead.
        /// </summary>
        public static void Reset()
        {
            m_owner = null;
            Set(null, 1.0f);
        }

        public static void Set(Texture2D lut, float strength)
        {
            m_strength = Mathf.Clamp01(strength);

            if (m_lut != lut)
            {
                m_lut = lut;
                m_lutParams = CalculateLutParams(lut);

                if (lut != null && lut != m_validatedLut)
                {
                    m_validatedLut = lut;
                    Validate(lut);
                }
            }
        }

        public static Vector4 CalculateLutParams(Texture2D lut)
        {
            if (lut == null || lut.height <= 0)
            {
                return Vector4.zero;
            }

            float size = lut.height;
            return new Vector4(1.0f / (size * size), 1.0f / size, size - 1.0f, 0.0f);
        }

        public static void ApplyTo(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (!IsActive)
            {
                material.DisableKeyword(c_lutKeyword);
                return;
            }

            material.EnableKeyword(c_lutKeyword);
            material.SetTexture(ShaderIds.MirisLutTex, m_lut);
            material.SetVector(ShaderIds.MirisLutParams, m_lutParams);
            material.SetFloat(ShaderIds.MirisLutStrength, m_strength);
        }

        // the user's imported asset in the Editor, which is not ours to do.
        public static void Validate(Texture2D lut)
        {
            if (lut == null)
            {
                return;
            }

            string prefix = $"[MirisColorGradeState] LUT '{lut.name}'";

            // A strip is `size` square slices side by side.
            if (lut.width != lut.height * lut.height)
            {
                Debug.LogWarning($"{prefix} is {lut.width}x{lut.height}, which is not a 2D strip - "
                                 + $"expected width to be height squared ({lut.height * lut.height}x{lut.height}). "
                                 + "The grade will sample the wrong cells.");
            }

            if (GraphicsFormatUtility.IsSRGBFormat(lut.graphicsFormat))
            {
                Debug.LogWarning($"{prefix} is imported as sRGB. Disable 'sRGB (Color Texture)' on the "
                                 + "texture - a LUT stores output values, not a colour to be decoded.");
            }

            if (GraphicsFormatUtility.IsCompressedFormat(lut.graphicsFormat))
            {
                Debug.LogWarning($"{prefix} is compressed ({lut.graphicsFormat}). Set Compression to 'None' - "
                                 + "block compression across LUT cells causes visible colour artefacts.");
            }

            if (lut.mipmapCount > 1)
            {
                Debug.LogWarning($"{prefix} has mipmaps. Disable 'Generate Mip Maps' - a mip averages "
                                 + "across LUT slice boundaries.");
            }

            if (lut.filterMode != FilterMode.Bilinear)
            {
                Debug.LogWarning($"{prefix} uses {lut.filterMode} filtering. Set Filter Mode to 'Bilinear' - "
                                 + "the grade interpolates between LUT cells.");
            }

            if (lut.wrapMode != TextureWrapMode.Clamp)
            {
                Debug.LogWarning($"{prefix} uses {lut.wrapMode} wrapping. Set Wrap Mode to 'Clamp'.");
            }
        }
    }
}
