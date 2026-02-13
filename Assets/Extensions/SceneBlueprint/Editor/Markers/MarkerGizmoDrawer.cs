#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 场景标记 Gizmo 统一绘制器。
    /// <para>
    /// 为所有 <see cref="SceneMarker"/> 子类绘制 Gizmo：
    /// - PointMarker → 实心球 + 方向箭头 + 标签
    /// - AreaMarker (Box) → 半透明立方体 + 线框
    /// - AreaMarker (Polygon) → 半透明多边形 + 边框线
    /// - EntityMarker → 菱形图标 + Prefab 名称标签
    /// </para>
    /// <para>
    /// 颜色由 Tag 前缀（图层）决定：
    /// Combat → 红色, Trigger → 蓝色, Environment → 黄色, Camera → 绿色, Narrative → 紫色
    /// </para>
    /// </summary>
    public static class MarkerGizmoDrawer
    {
        // ─── 图层颜色映射 ───

        private static readonly Color CombatColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);
        private static readonly Color TriggerColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);
        private static readonly Color EnvironmentColor = new Color(0.9f, 0.8f, 0.2f, 0.8f);
        private static readonly Color CameraColor = new Color(0.2f, 0.8f, 0.4f, 0.8f);
        private static readonly Color NarrativeColor = new Color(0.7f, 0.3f, 0.8f, 0.8f);
        private static readonly Color DefaultColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);

        // ─── 半透明填充色（用于区域） ───

        private static Color GetFillColor(Color baseColor)
        {
            return new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
        }

        /// <summary>
        /// 根据标记的 Tag 前缀获取 Gizmo 颜色。
        /// </summary>
        public static Color GetMarkerColor(SceneMarker marker)
        {
            var prefix = marker.GetLayerPrefix();
            return prefix switch
            {
                "Combat" => CombatColor,
                "Trigger" => TriggerColor,
                "Environment" => EnvironmentColor,
                "Camera" => CameraColor,
                "Narrative" => NarrativeColor,
                _ => DefaultColor,
            };
        }

        // ─── 高亮效果辅助 ───

        /// <summary>将颜色加亮（用于蓝图选中关联标记的高亮效果）</summary>
        private static Color GetHighlightColor(Color baseColor)
        {
            // 提高亮度和饱和度
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            v = Mathf.Min(v + 0.3f, 1f);
            s = Mathf.Max(s - 0.1f, 0f);
            var bright = Color.HSVToRGB(h, s, v);
            bright.a = 1f;
            return bright;
        }

        /// <summary>脉冲缩放效果（用于高亮标记的动态大小变化）</summary>
        private static float GetPulseScale(float maxScale)
        {
            // 使用 EditorApplication.timeSinceStartup 产生 sin 脉冲
            float t = (float)UnityEditor.EditorApplication.timeSinceStartup;
            float pulse = 1f + (maxScale - 1f) * (0.5f + 0.5f * Mathf.Sin(t * 4f));
            return pulse;
        }

        // ═══════════════════════════════════════════════════
        //  PointMarker Gizmo
        // ═══════════════════════════════════════════════════

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawPointMarkerGizmo(PointMarker marker, GizmoType gizmoType)
        {
            if (!MarkerLayerSystem.IsMarkerVisible(marker.GetLayerPrefix())) return;

            var color = GetMarkerColor(marker);
            bool selected = (gizmoType & GizmoType.Selected) != 0;
            bool highlighted = SceneMarkerSelectionBridge.IsMarkerHighlighted(marker.MarkerId);

            // 高亮：脉冲放大 + 颜色加亮
            if (highlighted) color = GetHighlightColor(color);

            var pos = marker.transform.position;
            float radius = marker.GizmoRadius;
            float scale = highlighted ? GetPulseScale(1.3f) : (selected ? 1.2f : 1f);

            // 实心球
            Gizmos.color = (selected || highlighted) ? color : new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawSphere(pos, radius * scale);

            // 线框球（选中/高亮时更明显）
            Gizmos.color = color;
            Gizmos.DrawWireSphere(pos, radius * scale);

            // 方向箭头
            if (marker.ShowDirection)
            {
                float arrowLen = radius * 3f;
                var forward = marker.transform.forward * arrowLen;
                Gizmos.color = color;
                Gizmos.DrawRay(pos, forward);

                // 箭头头部
                float headSize = radius * 0.6f;
                var headPos = pos + forward;
                var right = marker.transform.right * headSize;
                var up = marker.transform.up * headSize;
                Gizmos.DrawRay(headPos, -forward.normalized * headSize + right * 0.5f);
                Gizmos.DrawRay(headPos, -forward.normalized * headSize - right * 0.5f);
            }

            // 标签
            DrawLabel(marker, pos + Vector3.up * (radius + 0.5f), color);
        }

        // ═══════════════════════════════════════════════════
        //  AreaMarker Gizmo
        // ═══════════════════════════════════════════════════

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawAreaMarkerGizmo(AreaMarker marker, GizmoType gizmoType)
        {
            if (!MarkerLayerSystem.IsMarkerVisible(marker.GetLayerPrefix())) return;

            var color = GetMarkerColor(marker);
            bool selected = (gizmoType & GizmoType.Selected) != 0;
            bool highlighted = SceneMarkerSelectionBridge.IsMarkerHighlighted(marker.MarkerId);
            if (highlighted) color = GetHighlightColor(color);

            if (marker.Shape == AreaShape.Box)
            {
                DrawBoxArea(marker, color, selected);
            }
            else
            {
                DrawPolygonArea(marker, color, selected);
            }

            // 标签
            var labelPos = marker.GetRepresentativePosition() + Vector3.up * (marker.Height + 0.5f);
            DrawLabel(marker, labelPos, color);
        }

        private static void DrawBoxArea(AreaMarker marker, Color color, bool selected)
        {
            var pos = marker.transform.position;
            var size = marker.BoxSize;

            // 半透明填充
            var matrix = Gizmos.matrix;
            Gizmos.matrix = marker.transform.localToWorldMatrix;
            Gizmos.color = GetFillColor(color);
            Gizmos.DrawCube(Vector3.zero, size);

            // 线框
            Gizmos.color = selected ? color : new Color(color.r, color.g, color.b, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = matrix;
        }

        private static void DrawPolygonArea(AreaMarker marker, Color color, bool selected)
        {
            var verts = marker.GetWorldVertices();
            if (verts.Count < 2) return;

            // 底面边框线
            Gizmos.color = selected ? color : new Color(color.r, color.g, color.b, 0.6f);
            for (int i = 0; i < verts.Count; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Count];
                Gizmos.DrawLine(a, b);

                // 顶面线
                var aTop = a + Vector3.up * marker.Height;
                var bTop = b + Vector3.up * marker.Height;
                Gizmos.DrawLine(aTop, bTop);

                // 竖线
                Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                Gizmos.DrawLine(a, aTop);
                Gizmos.color = selected ? color : new Color(color.r, color.g, color.b, 0.6f);
            }

            // 选中时绘制顶点标记
            if (selected)
            {
                Gizmos.color = Color.white;
                float vertSize = 0.15f;
                foreach (var v in verts)
                {
                    Gizmos.DrawCube(v, Vector3.one * vertSize);
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  EntityMarker Gizmo
        // ═══════════════════════════════════════════════════

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawEntityMarkerGizmo(EntityMarker marker, GizmoType gizmoType)
        {
            if (!MarkerLayerSystem.IsMarkerVisible(marker.GetLayerPrefix())) return;

            var color = GetMarkerColor(marker);
            bool selected = (gizmoType & GizmoType.Selected) != 0;
            bool highlighted = SceneMarkerSelectionBridge.IsMarkerHighlighted(marker.MarkerId);
            if (highlighted) color = GetHighlightColor(color);

            var pos = marker.transform.position;
            float size = highlighted ? 0.6f * GetPulseScale(1.3f) : 0.6f;

            // 菱形图标（上下两个四面体组合）
            Gizmos.color = (selected || highlighted) ? color : new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawSphere(pos + Vector3.up * size * 0.5f, size * 0.4f);

            // 线框
            Gizmos.color = color;
            Gizmos.DrawWireSphere(pos + Vector3.up * size * 0.5f, size * 0.4f);

            // 底部十字
            float crossSize = size * 0.8f;
            Gizmos.DrawLine(pos + Vector3.forward * crossSize, pos - Vector3.forward * crossSize);
            Gizmos.DrawLine(pos + Vector3.right * crossSize, pos - Vector3.right * crossSize);

            // 标签（含 Prefab 名称）
            string label = marker.GetDisplayLabel();
            if (marker.PrefabRef != null)
            {
                label += $"\n[{marker.PrefabRef.name}]";
                if (marker.Count > 1)
                    label += $" ×{marker.Count}";
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = selected ? 11 : 9,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal
            };
            style.normal.textColor = color;
            Handles.Label(pos + Vector3.up * (size + 0.5f), label, style);
        }

        // ═══════════════════════════════════════════════════
        //  通用标签绘制
        // ═══════════════════════════════════════════════════

        private static void DrawLabel(SceneMarker marker, Vector3 position, Color color)
        {
            string text = marker.GetDisplayLabel();
            if (!string.IsNullOrEmpty(marker.Tag))
                text += $"\n<size=8>[{marker.Tag}]</size>";

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                richText = true
            };
            style.normal.textColor = color;
            Handles.Label(position, text, style);
        }
    }
}
