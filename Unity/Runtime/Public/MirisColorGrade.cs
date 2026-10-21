// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ImageEffectAllowedInSceneView]
    [AddComponentMenu("Miris/Miris Color Grade")]
    public class MirisColorGrade : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("2D strip LUT applied to the whole rendered image. Leave empty for no grading. Import it with sRGB off, Compression None, no mipmaps, Bilinear and Clamp.")]
        private Texture2D m_lut;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        [Tooltip("Blend between ungraded and fully graded. 0 leaves the image untouched, 1 applies the whole LUT.")]
        private float m_strength = 1.0f;

        private Material m_material;
        private bool m_hasWarnedMissingShader;
        private bool m_hasWarnedNotOwner;

        public Texture2D Lut => m_lut;

        public float Strength => m_strength;

        /// <summary>
        /// Sets the LUT and its strength, taking effect on the next frame. Pass null to turn
        /// grading off, which returns the image to its ungraded output exactly.
        /// </summary>
        /// <param name="lut">A 2D strip LUT, or null for no grading.</param>
        /// <param name="strength">Blend between ungraded and fully graded, clamped to 0..1.</param>
        public void SetGrade(Texture2D lut, float strength)
        {
            m_lut = lut;
            m_strength = Mathf.Clamp01(strength);
            Apply();
        }

        private void Apply()
        {
            if (MirisColorGradeState.SetFrom(this, m_lut, m_strength))
            {
                return;
            }

            if (!m_hasWarnedNotOwner)
            {
                m_hasWarnedNotOwner = true;
                Debug.LogWarning($"[MirisColorGrade] '{name}' is doing nothing: the grade is a single "
                                 + "full-screen pass over the whole image, so one component owns it per "
                                 + "scene. Disable the other Miris Color Grade component to hand it over.",
                                 this);
            }
        }

        private Material GetMaterial()
        {
            if (m_material != null)
            {
                return m_material;
            }

            Shader shader = Resources.Load<Shader>(MirisColorGradeState.c_shaderResourcePath);
            if (shader == null)
            {
                if (!m_hasWarnedMissingShader)
                {
                    m_hasWarnedMissingShader = true;
                    Debug.LogError($"[MirisColorGrade] '{MirisColorGradeState.c_shaderResourcePath}' is missing "
                                   + "from the package's Resources - colour grading will not be applied.");
                }
                return null;
            }

            m_material = new Material(shader) { name = "MirisColorGradeMaterial", hideFlags = HideFlags.HideAndDontSave };
            return m_material;
        }

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        protected void OnEnable()
        {
            Apply();
        }

        protected void OnDisable()
        {
            MirisColorGradeState.ClearFrom(this);
            m_hasWarnedNotOwner = false;

            if (m_material != null)
            {
                DestroyImmediate(m_material);
                m_material = null;
            }
        }

        protected void OnValidate()
        {
            m_strength = Mathf.Clamp01(m_strength);
            Apply();
        }

        protected void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Material material = MirisColorGradeState.IsOwner(this) && MirisColorGradeState.IsActive
                ? GetMaterial()
                : null;

            if (material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            MirisColorGradeState.ApplyTo(material);
            Graphics.Blit(source, destination, material);
        }
    }
}
