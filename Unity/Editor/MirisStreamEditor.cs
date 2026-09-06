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
                DrawBadge((MirisStream)target);
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

        private void DrawBadge(MirisStream stream)
        {
            MirisStream.Status status = stream.GetStatus();
            Color color = GetStatusColor(status);

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
            GUI.Label(titleRect, GetStatusTitle(status), m_titleStyle);

            Rect detailRect = new Rect(textX, badgeRect.y + 21.0f, textWidth, 16.0f);
            GUI.Label(detailRect, GetStatusDetail(status), m_detailStyle);
        }

        private void DrawCompactBadge(MirisStream stream)
        {
            MirisStream.Status status = stream.GetStatus();
            Color color = GetStatusColor(status);

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
            GUI.Label(labelRect, $"{stream.name} — {GetStatusTitle(status)}", m_detailStyle);
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

        private static Color GetStatusColor(MirisStream.Status status)
        {
            switch (status)
            {
                case MirisStream.Status.Disabled:
                    return s_idleColor;

                case MirisStream.Status.NoController:
                case MirisStream.Status.ControllerInactive:
                case MirisStream.Status.NoData:
                    return s_errorColor;

                case MirisStream.Status.NoAssetId:
                    return s_actionColor;

                case MirisStream.Status.NotLoaded:
                case MirisStream.Status.Streaming:
                case MirisStream.Status.Ready:
                    return s_progressColor;

                case MirisStream.Status.Rendered:
                    return s_readyColor;

                default:
                    return s_idleColor;
            }
        }

        private static string GetStatusTitle(MirisStream.Status status)
        {
            switch (status)
            {
                case MirisStream.Status.Disabled:
                    return "Disabled";

                case MirisStream.Status.NoController:
                    return "No Controller";

                case MirisStream.Status.ControllerInactive:
                    return "Controller Inactive";

                case MirisStream.Status.NoAssetId:
                    return "No Asset Id";

                case MirisStream.Status.NotLoaded:
                    return "Not Loaded";

                case MirisStream.Status.Streaming:
                    return "Streaming";

                case MirisStream.Status.NoData:
                    return "No Data";

                case MirisStream.Status.Ready:
                    return "Ready";

                case MirisStream.Status.Rendered:
                    return "Rendered";

                default:
                    return status.ToString();
            }
        }

        private static string GetStatusDetail(MirisStream.Status status)
        {
            switch (status)
            {
                case MirisStream.Status.Disabled:
                    return "Component is inactive. Render resources have been released.";

                case MirisStream.Status.NoController:
                    return "Assign a Miris Stream Controller, or add one to the scene.";

                case MirisStream.Status.ControllerInactive:
                    return "Controller assigned, but its client is not running yet.";

                case MirisStream.Status.NoAssetId:
                    return "Set an Asset Id to stream content.";

                case MirisStream.Status.NotLoaded:
                    return "Asset Id set. Waiting to register with the scene.";

                case MirisStream.Status.Streaming:
                    return "Registered with the scene. Waiting for asset contents.";

                case MirisStream.Status.NoData:
                    return "Asset arrived but carries no usable render data.";

                case MirisStream.Status.Ready:
                    return "Render data is valid. Building GPU resources.";

                case MirisStream.Status.Rendered:
                    return "Rendering.";
                default:
                    return string.Empty;
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
