#nullable enable
using System;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;

namespace SceneBlueprint.Editor.Markers.Renderers
{
    /// <summary>
    /// MarkerGroup 的 Gizmo 渲染器。
    /// <para>
    /// 绘制：
    /// - 成员点之间的贝塞尔曲线连接
    /// - 成员点编号标签
    /// - 组中心图标
    /// - 组名称标签
    /// </para>
    /// </summary>
    public class MarkerGroupRenderer : IMarkerGizmoRenderer
    {
        public Type TargetType => typeof(MarkerGroup);

        private const float MemberPointRadius = 0.3f;
        private const float CenterIconSize = 0.8f;

        public void DrawWireframe(in GizmoDrawContext ctx)
        {
            var group = (MarkerGroup)ctx.Marker;
            if (!group.ShowConnectionLines || group.Members.Count < 2)
                return;

            // 绘制成员点之间的贝塞尔曲线连接
            DrawMemberConnections(group, ctx);
        }

        public void DrawIcon(in GizmoDrawContext ctx)
        {
            var group = (MarkerGroup)ctx.Marker;

            // 绘制成员点
            DrawMemberPoints(group, ctx);

            // 绘制组中心图标
            DrawCenterIcon(group, ctx);
        }

        public void DrawLabel(in GizmoDrawContext ctx)
        {
            var group = (MarkerGroup)ctx.Marker;

            // 绘制成员编号
            DrawMemberLabels(group, ctx);

            // 绘制组名称
            var centerPos = group.GetRepresentativePosition();
            var labelPos = centerPos + Vector3.up * (CenterIconSize + 0.8f);
            GizmoLabelUtil.DrawStandardLabel(group, labelPos, ctx.EffectiveColor);
        }

        public void DrawHighlight(in GizmoDrawContext ctx)
        {
            var group = (MarkerGroup)ctx.Marker;

            // 高亮时绘制脉冲光晕
            var centerPos = group.GetRepresentativePosition();
            float glowSize = CenterIconSize * 2f * ctx.PulseScale;

            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.4f);

            // 绘制外圈光晕
            Handles.DrawWireDisc(centerPos, Vector3.up, glowSize);
            Handles.DrawWireDisc(centerPos, Vector3.forward, glowSize);
        }

        public PickBounds GetPickBounds(in GizmoDrawContext ctx)
        {
            var group = (MarkerGroup)ctx.Marker;
            var centerPos = group.GetRepresentativePosition();

            // 如果有成员，计算包围所有成员的半径
            float radius = CenterIconSize * 2f;
            if (group.Members.Count > 0)
            {
                float maxDist = 0f;
                foreach (var member in group.Members)
                {
                    if (member != null)
                    {
                        float dist = Vector3.Distance(centerPos, member.position);
                        if (dist > maxDist) maxDist = dist;
                    }
                }
                radius = Mathf.Max(radius, maxDist + MemberPointRadius);
            }

            return new PickBounds
            {
                Center = centerPos,
                Radius = radius
            };
        }

        // ─── 辅助绘制方法 ───

        /// <summary>
        /// 绘制成员点之间的贝塞尔曲线连接
        /// </summary>
        private void DrawMemberConnections(MarkerGroup group, in GizmoDrawContext ctx)
        {
            var positions = group.GetMemberWorldPositions();
            if (positions.Count < 2)
                return;

            Color lineColor = group.UseCustomGizmoColor
                ? group.ConnectionColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.6f);

            Handles.color = lineColor;

            // 绘制连续的贝塞尔曲线
            for (int i = 0; i < positions.Count - 1; i++)
            {
                DrawBezierCurve(positions[i], positions[i + 1], lineColor);
            }

            // 如果是闭环（首尾相连），可选
            // DrawBezierCurve(positions[positions.Count - 1], positions[0], lineColor);
        }

        /// <summary>
        /// 绘制两点之间的贝塞尔曲线
        /// </summary>
        private void DrawBezierCurve(Vector3 start, Vector3 end, Color color)
        {
            // 计算控制点（让曲线有一定弧度）
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // 控制点偏移：垂直于连线方向，高度为距离的 20%
            Vector3 midPoint = (start + end) * 0.5f;
            Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.up);
            if (perpendicular.magnitude < 0.01f)
            {
                // 如果方向几乎垂直，使用 forward 作为参考
                perpendicular = Vector3.Cross(direction.normalized, Vector3.forward);
            }

            Vector3 controlPoint = midPoint + perpendicular.normalized * (distance * 0.15f);

            // 绘制贝塞尔曲线
            Handles.DrawBezier(
                start,
                end,
                controlPoint,
                controlPoint,
                color,
                null,
                2f);
        }

        /// <summary>
        /// 绘制成员点
        /// </summary>
        private void DrawMemberPoints(MarkerGroup group, in GizmoDrawContext ctx)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null) continue;

                Vector3 pos = member.position;

                // 绘制小圆点
                Handles.color = ctx.EffectiveColor;
                Handles.SphereHandleCap(0, pos, Quaternion.identity, MemberPointRadius * 2f, EventType.Repaint);

                // 绘制线框
                Handles.DrawWireDisc(pos, Vector3.up, MemberPointRadius);
            }
        }

        /// <summary>
        /// 绘制成员编号标签
        /// </summary>
        private void DrawMemberLabels(MarkerGroup group, in GizmoDrawContext ctx)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null) continue;

                Vector3 labelPos = member.position + Vector3.up * (MemberPointRadius + 0.3f);
                string label = (i + 1).ToString();

                // 绘制编号（小号字体）
                GizmoLabelUtil.DrawCustomLabel(label, labelPos, ctx.EffectiveColor, 9);
            }
        }

        /// <summary>
        /// 绘制组中心图标（菱形）
        /// </summary>
        private void DrawCenterIcon(MarkerGroup group, in GizmoDrawContext ctx)
        {
            var centerPos = group.GetRepresentativePosition();
            float size = CenterIconSize * ctx.PulseScale;

            // 绘制菱形（用4条线）
            Vector3 top = centerPos + Vector3.up * size;
            Vector3 bottom = centerPos + Vector3.down * size;
            Vector3 left = centerPos + Vector3.left * size;
            Vector3 right = centerPos + Vector3.right * size;

            Handles.color = ctx.EffectiveColor;

            // 绘制菱形轮廓
            Handles.DrawLine(top, right);
            Handles.DrawLine(right, bottom);
            Handles.DrawLine(bottom, left);
            Handles.DrawLine(left, top);

            // 绘制中心点
            float dotSize = size * 0.3f;
            Handles.SphereHandleCap(0, centerPos, Quaternion.identity, dotSize * 2f, EventType.Repaint);
        }
    }
}
