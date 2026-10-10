// Copyright © 2026 Miris, Inc. All rights reserved.

using Miris.Runtime;

using UnityEditor;
using UnityEngine;

namespace Miris.Editor
{
    /// <summary>
    /// Draws a live status badge above the default <see cref="MirisStream"/> inspector,
    /// showing how far the stream has progressed towards rendering.
    /// </summary>
    [CustomEditor(typeof(MirisStream))]
    [CanEditMultipleObjects]
    public class MirisStreamEditor : UnityEditor.Editor
    {
        private static readonly Color s_errorColor = new Color(0.79f, 0.29f, 0.25f);
        private static readonly Color s_actionColor = new Color(0.85f, 0.62f, 0.20f);
        private static readonly Color s_progressColor = new Color(0.27f, 0.52f, 0.78f);
        private static readonly Color s_readyColor = new Color(0.35f, 0.65f, 0.35f);
        private static readonly Color s_idleColor = new Color(0.45f, 0.45f, 0.45f);

        private const float BadgeHeight = 42.0f;
        private const float CompactBadgeHeight = 20.0f;
        private const float DotDiameter = 10.0f;
        private const float Padding = 8.0f;

        private GUIStyle m_titleStyle;
        private GUIStyle m_detailStyle;

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();

            if (targets.Length == 1)
            {
                MirisStream stream = (MirisStream)target;
                DrawBadge(stream);
                EditorGUILayout.Space();
                DrawViewingVolumeAndDefaultCamera(stream);
            }
            else
            {
                foreach (Object streamTarget in targets)
                {
                    DrawCompactBadge((MirisStream)streamTarget);
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        /// <summary>
        /// Lets the Scene view's Frame Selected (F) fit the streamed content. Unity can't measure
        /// a stream on its own because the splats aren't drawn by a Renderer.
        /// </summary>
        public bool HasFrameBounds()
        {
            return TryGetFrameBounds(out Bounds _);
        }

        public Bounds OnGetFrameBounds()
        {
            TryGetFrameBounds(out Bounds bounds);
            return bounds;
        }

        private bool TryGetFrameBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;

            foreach (Object streamTarget in targets)
            {
                Bounds streamBounds = ((MirisStream)streamTarget).GetWorldBounds();
                if (!IsFramable(streamBounds))
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(streamBounds);
                }
                else
                {
                    bounds = streamBounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        // Rejects the zero-size box of an unloaded stream and any non-finite or inverted box.
        private static bool IsFramable(Bounds bounds)
        {
            Vector3 size = bounds.size;
            for (int axis = 0; axis < 3; axis++)
            {
                if (!float.IsFinite(size[axis]) || size[axis] < 0.0f
                    || !float.IsFinite(bounds.center[axis]))
                {
                    return false;
                }
            }

            return size.sqrMagnitude > 0.0f;
        }

        /// <summary>
        /// Read-only summary of the default camera / viewing volume this stream's content
        /// authored -- both are per-stream, so this always reflects this component's own asset
        /// rather than whatever else the scene has loaded. Omitted entirely when the content
        /// doesn't author one, rather than shown as a blank/placeholder row.
        /// </summary>
        private void DrawViewingVolumeAndDefaultCamera(MirisStream stream)
        {
            if (stream.TryGetDefaultCameraWorldTransform(out Matrix4x4 cameraTransform))
            {
                EditorGUILayout.LabelField("Default Camera", cameraTransform.GetPosition().ToString("F2"));
            }

            if (stream.TryGetViewingVolumeBounds(out Bounds localBounds, out Matrix4x4 volumeToWorld))
            {
                EditorGUILayout.LabelField("Viewing Volume",
                    $"{localBounds.size.ToString("F2")} at {volumeToWorld.GetPosition().ToString("F2")}");
            }
        }

        private void DrawBadge(MirisStream stream)
        {
            Badge badge = GetBadge(stream);
            Color color = badge.m_color;

            Rect badgeRect = EditorGUILayout.GetControlRect(false, BadgeHeight);
            DrawBadgeBackground(badgeRect, color);

            Rect dotRect = new Rect(
                badgeRect.x + Padding,
                badgeRect.y + (BadgeHeight - DotDiameter) * 0.5f,
                DotDiameter,
                DotDiameter
            );
            DrawStatusDot(dotRect, color);

            float textX = dotRect.xMax + Padding;
            float textWidth = badgeRect.xMax - textX - Padding;

            Rect titleRect = new Rect(textX, badgeRect.y + 5.0f, textWidth, 16.0f);
            GUI.Label(titleRect, badge.m_title, m_titleStyle);

            Rect detailRect = new Rect(textX, badgeRect.y + 21.0f, textWidth, 16.0f);
            GUI.Label(detailRect, badge.m_detail, m_detailStyle);
        }

        private void DrawCompactBadge(MirisStream stream)
        {
            Badge badge = GetBadge(stream);
            Color color = badge.m_color;

            Rect badgeRect = EditorGUILayout.GetControlRect(false, CompactBadgeHeight);
            DrawBadgeBackground(badgeRect, color);

            Rect dotRect = new Rect(
                badgeRect.x + Padding,
                badgeRect.y + (CompactBadgeHeight - DotDiameter) * 0.5f,
                DotDiameter,
                DotDiameter
            );
            DrawStatusDot(dotRect, color);

            float textX = dotRect.xMax + Padding;
            Rect labelRect = new Rect(textX, badgeRect.y + 2.0f, badgeRect.xMax - textX - Padding, 16.0f);
            GUI.Label(labelRect, $"{stream.name} — {badge.m_title}", m_detailStyle);
        }

        private static void DrawStatusDot(Rect dotRect, Color color)
        {
            GUI.DrawTexture(
                dotRect,
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true,
                0.0f,
                color,
                0.0f,
                DotDiameter * 0.5f
            );
        }

        private static void DrawBadgeBackground(Rect badgeRect, Color color)
        {
            Color background = color;
            background.a = EditorGUIUtility.isProSkin ? 0.16f : 0.12f;
            EditorGUI.DrawRect(badgeRect, background);
        }

        private struct Badge
        {
            public string m_title;
            public string m_detail;
            public Color m_color;

            public Badge(string title, string detail, Color color)
            {
                m_title = title;
                m_detail = detail;
                m_color = color;
            }
        }

        // The component's own setup comes first; past that, the native status decides.
        private static Badge GetBadge(MirisStream stream)
        {
            if (!stream.isActiveAndEnabled)
            {
                return new Badge("Disabled", "Component is inactive. Render resources have been released.", s_idleColor);
            }

            if (stream.m_streamController == null)
            {
                return new Badge("No Controller", "Assign a Miris Stream Controller, or add one to the scene.", s_errorColor);
            }

            if (!stream.m_streamController.IsActive())
            {
                return new Badge("Controller Inactive", "Controller assigned, but its client is not running yet.", s_errorColor);
            }

            if (string.IsNullOrEmpty(stream.m_assetId))
            {
                return new Badge("No Asset Id", "Set an Asset Id to stream content.", s_actionColor);
            }

            if (stream.IsRendered())
            {
                return new Badge("Rendered", "Rendering.", s_readyColor);
            }

            switch (stream.GetStatus())
            {
                case StreamStatus.Resolving:
                    return new Badge("Resolving", "Looking up the Asset Id.", s_progressColor);

                case StreamStatus.Loading:
                    return new Badge("Loading", "Registered with the scene. Fetching asset contents.", s_progressColor);

                case StreamStatus.Ready:
                    return new Badge("Ready", "Render data is resident but not drawn this frame.", s_progressColor);

                case StreamStatus.Failed:
                    return new Badge("Failed", "The asset could not be loaded. See the console for details.", s_errorColor);

                default:
                    return new Badge("Not Loaded", "Asset Id set. Waiting to register with the scene.", s_progressColor);
            }
        }

        private void EnsureStyles()
        {
            if (m_titleStyle != null)
            {
                return;
            }

            m_titleStyle = new GUIStyle(EditorStyles.boldLabel);

            m_detailStyle = new GUIStyle(EditorStyles.miniLabel);
            m_detailStyle.wordWrap = false;
        }
    }
}
