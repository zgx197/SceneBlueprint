#nullable enable
using System;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;

namespace SceneBlueprint.Editor.Markers.Renderers
{
    /// <summary>
    /// EntityMarker 的 Gizmo 渲染器。
    /// <para>
    /// 绘制：实心球 + 线框 + 底部十字 + Prefab 名称标签。
    /// 高亮时：脉冲缩放 + 外圈光晕。
    /// </para>
    /// </summary>
    public class EntityMarkerRenderer : IMarkerGizmoRenderer
    {
        public Type TargetType => typeof(EntityMarker);

        private const float BaseSize = 0.6f;

        public void DrawIcon(in GizmoDrawContext ctx)
        {
            var pos = ctx.Transform.position;
            float size = BaseSize * ctx.PulseScale;

            // 实心球（偏上，菱形效果的近似）
            Handles.color = (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.5f);
            Handles.SphereHandleCap(0, pos + Vector3.up * size * 0.5f,
                Quaternion.identity, size * 0.8f, EventType.Repaint);

            // 线框球
            Handles.color = ctx.EffectiveColor;
            Handles.DrawWireDisc(pos + Vector3.up * size * 0.5f, Vector3.up, size * 0.4f);
        }

        public void DrawWireframe(in GizmoDrawContext ctx)
        {
            var pos = ctx.Transform.position;
            float crossSize = BaseSize * 0.8f;

            // 底部十字
            Handles.color = ctx.EffectiveColor;
            Handles.DrawLine(pos + Vector3.forward * crossSize, pos - Vector3.forward * crossSize);
            Handles.DrawLine(pos + Vector3.right * crossSize, pos - Vector3.right * crossSize);
        }

        public void DrawHighlight(in GizmoDrawContext ctx)
        {
            var pos = ctx.Transform.position + Vector3.up * BaseSize * 0.5f;

            // 外圈光晕
            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.3f);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, BaseSize * 2.4f, EventType.Repaint);

            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.7f);
            Handles.DrawWireDisc(pos, Vector3.up, BaseSize * 1.2f);
        }

        public void DrawLabel(in GizmoDrawContext ctx)
        {
            var em = (EntityMarker)ctx.Marker;
            var pos = ctx.Transform.position;

            // 含 Prefab 名称的标签
            string label = em.GetDisplayLabel();
            if (em.PrefabRef != null)
            {
                label += $"\n[{em.PrefabRef.name}]";
                if (em.Count > 1)
                    label += $" ×{em.Count}";
            }

            var labelPos = pos + Vector3.up * (BaseSize + 0.5f);
            int fontSize = ctx.IsSelected ? 11 : 9;
            GizmoLabelUtil.DrawCustomLabel(label, labelPos, ctx.EffectiveColor, fontSize);
        }

        public PickBounds GetPickBounds(in GizmoDrawContext ctx)
        {
            return new PickBounds
            {
                Center = ctx.Transform.position + Vector3.up * 0.3f,
                Radius = BaseSize
            };
        }
    }
}
