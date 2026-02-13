#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;

namespace SceneBlueprint.Editor.Markers.Renderers
{
    /// <summary>
    /// AreaMarker 的 Gizmo 渲染器。
    /// <para>
    /// Box 模式：半透明立方体填充 + 线框。
    /// Polygon 模式：底面/顶面边线 + 竖线 + 顶点标记。
    /// Interactive Phase：选中时绘制编辑 Handle（Box Handle / 顶点拖拽）。
    /// </para>
    /// </summary>
    public class AreaMarkerRenderer : IMarkerGizmoRenderer
    {
        public Type TargetType => typeof(AreaMarker);

        // Interactive Phase 用的 BoxBoundsHandle（复用）
        private BoxBoundsHandle? _boxHandle;

        // ─── Phase 0: Fill ───

        public void DrawFill(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            if (am.Shape == AreaShape.Box)
                DrawBoxFill(am, ctx);
            else
                DrawPolygonFill(am, ctx);
        }

        private void DrawBoxFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;

            var fillColor = ctx.IsHighlighted
                ? new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.4f)
                : ctx.FillColor;

            DrawAllBoxFaces(am.BoxSize, fillColor);

            Handles.matrix = matrix;
        }

        private void DrawPolygonFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            var verts = am.GetWorldVertices();
            if (verts.Count < 3) return;

            // 底面填充（用 DrawAAConvexPolygon）
            var fillColor = ctx.IsHighlighted
                ? new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.35f)
                : ctx.FillColor;

            Handles.color = fillColor;
            var polyVerts = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
                polyVerts[i] = verts[i];
            Handles.DrawAAConvexPolygon(polyVerts);

            // 顶面填充
            var topVerts = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
                topVerts[i] = verts[i] + Vector3.up * am.Height;
            Handles.DrawAAConvexPolygon(topVerts);
        }

        private static void DrawSolidFace(Color color, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Handles.DrawSolidRectangleWithOutline(
                new[] { a, b, c, d }, color, Color.clear);
        }

        /// <summary>绘制 Box 的完整 6 面半透明填充</summary>
        private static void DrawAllBoxFaces(Vector3 size, Color fillColor)
        {
            var half = size * 0.5f;
            // 底面
            DrawSolidFace(fillColor,
                new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, half.z), new Vector3(-half.x, -half.y, half.z));
            // 顶面
            DrawSolidFace(fillColor,
                new Vector3(-half.x, half.y, -half.z), new Vector3(half.x, half.y, -half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z));
            // 前面 (z-)
            DrawSolidFace(fillColor,
                new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z));
            // 后面 (z+)
            DrawSolidFace(fillColor,
                new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z));
            // 左面 (x-)
            DrawSolidFace(fillColor,
                new Vector3(-half.x, -half.y, -half.z), new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x, half.y, half.z), new Vector3(-half.x, half.y, -half.z));
            // 右面 (x+)
            DrawSolidFace(fillColor,
                new Vector3(half.x, -half.y, -half.z), new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(half.x, half.y, -half.z));
        }

        /// <summary>用 Handles.DrawLine 手动绘制 Box 的 12 条边线</summary>
        private static void DrawBoxWireLines(Vector3 size, Color wireColor)
        {
            var half = size * 0.5f;
            Handles.color = wireColor;

            // 底面 4 条边
            var b0 = new Vector3(-half.x, -half.y, -half.z);
            var b1 = new Vector3( half.x, -half.y, -half.z);
            var b2 = new Vector3( half.x, -half.y,  half.z);
            var b3 = new Vector3(-half.x, -half.y,  half.z);
            Handles.DrawLine(b0, b1); Handles.DrawLine(b1, b2);
            Handles.DrawLine(b2, b3); Handles.DrawLine(b3, b0);

            // 顶面 4 条边
            var t0 = new Vector3(-half.x, half.y, -half.z);
            var t1 = new Vector3( half.x, half.y, -half.z);
            var t2 = new Vector3( half.x, half.y,  half.z);
            var t3 = new Vector3(-half.x, half.y,  half.z);
            Handles.DrawLine(t0, t1); Handles.DrawLine(t1, t2);
            Handles.DrawLine(t2, t3); Handles.DrawLine(t3, t0);

            // 竖边 4 条
            Handles.DrawLine(b0, t0); Handles.DrawLine(b1, t1);
            Handles.DrawLine(b2, t2); Handles.DrawLine(b3, t3);
        }

        // ─── Phase 1: Wireframe ───

        public void DrawWireframe(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            if (am.Shape == AreaShape.Box)
                DrawBoxWireframe(am, ctx);
            else
                DrawPolygonWireframe(am, ctx);
        }

        private void DrawBoxWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;

            var wireColor = (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.6f);

            DrawBoxWireLines(am.BoxSize, wireColor);

            // 高亮外扩框
            if (ctx.IsHighlighted)
            {
                var glowColor = new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.5f);
                DrawBoxWireLines(am.BoxSize * 1.1f, glowColor);
            }

            Handles.matrix = matrix;
        }

        private void DrawPolygonWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var verts = am.GetWorldVertices();
            if (verts.Count < 2) return;

            var edgeColor = (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.6f);

            Handles.color = edgeColor;
            for (int i = 0; i < verts.Count; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Count];

                // 底面线
                Handles.DrawLine(a, b);

                // 顶面线
                var aTop = a + Vector3.up * am.Height;
                var bTop = b + Vector3.up * am.Height;
                Handles.DrawLine(aTop, bTop);

                // 竖线
                Handles.color = new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                    ctx.IsHighlighted ? 0.6f : 0.3f);
                Handles.DrawLine(a, aTop);
                Handles.color = edgeColor;
            }

            // 选中或高亮时显示顶点
            if (ctx.IsSelected || ctx.IsHighlighted)
            {
                Handles.color = Color.white;
                float vertSize = 0.15f;
                foreach (var v in verts)
                    Handles.DotHandleCap(0, v, Quaternion.identity, vertSize * 0.5f, EventType.Repaint);
            }
        }

        // ─── Phase 3: Interactive ───

        public bool DrawInteractive(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            if (am.Shape == AreaShape.Box)
            {
                DrawBoxEditHandles(am, ctx);
                // Box 模式接管 Fill/Wireframe，因为 BoxBoundsHandle 自带线框
                return true;
            }
            else
            {
                DrawPolygonEditHandles(am, ctx);
                // Polygon 不完全接管，仍需管线绘制 Fill/Wireframe
                return false;
            }
        }

        private void DrawBoxEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            _boxHandle ??= new BoxBoundsHandle();

            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;

            _boxHandle.center = Vector3.zero;
            _boxHandle.size = am.BoxSize;

            // 绘制填充（编辑模式，6 面完整绘制）
            var fillColor = ctx.FillColor;
            DrawAllBoxFaces(am.BoxSize, fillColor);

            EditorGUI.BeginChangeCheck();
            _boxHandle.DrawHandle();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整区域尺寸");
                am.BoxSize = _boxHandle.size;
                EditorUtility.SetDirty(am);
            }

            Handles.matrix = matrix;
        }

        private void DrawPolygonEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            if (am.Vertices.Count == 0) return;

            var transform = ctx.Transform;
            var verts = am.Vertices;

            for (int i = 0; i < verts.Count; i++)
            {
                var worldPos = transform.TransformPoint(verts[i]);
                float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.08f;

                // Shift+点击删除顶点
                if (Event.current.shift)
                {
                    Handles.color = Color.red;
                    if (Handles.Button(worldPos, Quaternion.identity, handleSize * 1.5f, handleSize * 2f, Handles.DotHandleCap))
                    {
                        if (verts.Count > 3)
                        {
                            Undo.RecordObject(am, "删除区域顶点");
                            verts.RemoveAt(i);
                            EditorUtility.SetDirty(am);
                            return;
                        }
                    }
                }
                else
                {
                    // 拖拽顶点
                    Handles.color = Color.white;
                    EditorGUI.BeginChangeCheck();
#if UNITY_2022_1_OR_NEWER
                    var newWorldPos = Handles.FreeMoveHandle(
                        worldPos, handleSize, Vector3.zero, Handles.DotHandleCap);
#else
                    var newWorldPos = Handles.FreeMoveHandle(
                        worldPos, Quaternion.identity, handleSize, Vector3.zero, Handles.DotHandleCap);
#endif
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(am, "移动区域顶点");
                        verts[i] = transform.InverseTransformPoint(newWorldPos);
                        EditorUtility.SetDirty(am);
                    }
                }

                // 顶点序号标签
                Handles.color = Color.white;
                GizmoLabelUtil.DrawCustomLabel($"[{i}]", worldPos + Vector3.up * 0.3f, Color.white, 9);
            }

            // 边中点（添加顶点）
            if (!Event.current.shift)
                DrawEdgeMidpoints(am, transform, verts);
        }

        private void DrawEdgeMidpoints(AreaMarker am, Transform transform, List<Vector3> verts)
        {
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);

            for (int i = 0; i < verts.Count; i++)
            {
                int next = (i + 1) % verts.Count;
                var midLocal = (verts[i] + verts[next]) * 0.5f;
                var midWorld = transform.TransformPoint(midLocal);
                float handleSize = HandleUtility.GetHandleSize(midWorld) * 0.05f;

                if (Handles.Button(midWorld, Quaternion.identity, handleSize, handleSize * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(am, "插入区域顶点");
                    verts.Insert(next, midLocal);
                    EditorUtility.SetDirty(am);
                    return;
                }
            }
        }

        // ─── Phase 4: Highlight ───

        public void DrawHighlight(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;
            var center = am.GetRepresentativePosition();

            // 中心光晕
            float glowRadius = 2f;
            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.25f);
            Handles.SphereHandleCap(0, center, Quaternion.identity, glowRadius * 2f, EventType.Repaint);
        }

        // ─── Phase 5: Label ───

        public void DrawLabel(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;
            var labelPos = am.GetRepresentativePosition() + Vector3.up * (am.Height + 0.5f);
            GizmoLabelUtil.DrawStandardLabel(ctx.Marker, labelPos, ctx.EffectiveColor);
        }

        // ─── Phase 6: Pick ───

        public PickBounds GetPickBounds(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;
            var center = am.GetRepresentativePosition();
            float radius;

            if (am.Shape == AreaShape.Box)
            {
                radius = Mathf.Max(am.BoxSize.x, am.BoxSize.z) * 0.5f;
            }
            else
            {
                radius = 2f;
                if (am.Vertices.Count > 0)
                {
                    float maxR = 0;
                    foreach (var v in am.Vertices)
                        maxR = Mathf.Max(maxR, v.magnitude);
                    radius = maxR;
                }
            }

            return new PickBounds { Center = center, Radius = radius };
        }
    }
}
