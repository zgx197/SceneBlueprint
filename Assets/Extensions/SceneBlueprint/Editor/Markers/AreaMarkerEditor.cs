#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// AreaMarker 的自定义 Inspector 和 Scene View 编辑器。
    /// <para>
    /// 功能：
    /// <list type="bullet">
    ///   <item>Polygon 模式：可拖拽顶点 Handle + 边中点添加顶点 + 右键删除顶点</item>
    ///   <item>Box 模式：BoxBoundsHandle 调整尺寸</item>
    ///   <item>Inspector 面板：显示基础属性 + 快捷操作按钮</item>
    /// </list>
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AreaMarker))]
    [CanEditMultipleObjects]
    public class AreaMarkerEditor : UnityEditor.Editor
    {
        private BoxBoundsHandle? _boxHandle;
        private AreaMarker? _marker;

        // 当前被拖拽的顶点索引（-1 表示无）
        private int _draggingVertexIndex = -1;

        private void OnEnable()
        {
            _marker = target as AreaMarker;
            _boxHandle = new BoxBoundsHandle();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 基础属性
            DrawDefaultInspector();

            if (_marker == null) return;

            EditorGUILayout.Space(8);

            if (_marker.Shape == AreaShape.Polygon)
            {
                DrawPolygonTools();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>绘制 Polygon 模式的快捷工具按钮</summary>
        private void DrawPolygonTools()
        {
            if (_marker == null) return;

            EditorGUILayout.LabelField("多边形工具", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("添加顶点"))
            {
                Undo.RecordObject(_marker, "添加区域顶点");
                // 在最后一个顶点偏移位置添加新顶点，或在原点添加第一个
                Vector3 newVert;
                if (_marker.Vertices.Count > 0)
                {
                    var last = _marker.Vertices[_marker.Vertices.Count - 1];
                    newVert = last + Vector3.right * 2f;
                }
                else
                {
                    newVert = Vector3.right * 2f;
                }
                _marker.Vertices.Add(newVert);
                EditorUtility.SetDirty(_marker);
            }

            if (GUILayout.Button("清除全部") && _marker.Vertices.Count > 0)
            {
                if (EditorUtility.DisplayDialog("清除顶点", "确定要清除所有多边形顶点吗？", "确定", "取消"))
                {
                    Undo.RecordObject(_marker, "清除区域顶点");
                    _marker.Vertices.Clear();
                    EditorUtility.SetDirty(_marker);
                }
            }

            EditorGUILayout.EndHorizontal();

            // 创建默认矩形
            if (_marker.Vertices.Count == 0)
            {
                if (GUILayout.Button("创建默认矩形 (5×5)"))
                {
                    Undo.RecordObject(_marker, "创建默认矩形");
                    _marker.Vertices.AddRange(new[]
                    {
                        new Vector3(-2.5f, 0, -2.5f),
                        new Vector3( 2.5f, 0, -2.5f),
                        new Vector3( 2.5f, 0,  2.5f),
                        new Vector3(-2.5f, 0,  2.5f),
                    });
                    EditorUtility.SetDirty(_marker);
                }
            }

            EditorGUILayout.HelpBox(
                "在 Scene View 中拖拽顶点调整形状\n" +
                "点击边中点(+)添加新顶点\n" +
                "Shift+点击顶点删除",
                MessageType.Info);
        }

        // ═══════════════════════════════════════════════════
        //  Scene View Handle 绘制
        // ═══════════════════════════════════════════════════

        private void OnSceneGUI()
        {
            if (_marker == null) return;

            if (_marker.Shape == AreaShape.Box)
            {
                DrawBoxHandles();
            }
            else
            {
                DrawPolygonHandles();
            }
        }

        // ─── Box 模式：BoxBoundsHandle ───

        private void DrawBoxHandles()
        {
            if (_marker == null || _boxHandle == null) return;

            var matrix = Handles.matrix;
            Handles.matrix = _marker.transform.localToWorldMatrix;

            _boxHandle.center = Vector3.zero;
            _boxHandle.size = _marker.BoxSize;

            EditorGUI.BeginChangeCheck();
            _boxHandle.DrawHandle();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_marker, "调整区域尺寸");
                _marker.BoxSize = _boxHandle.size;
                EditorUtility.SetDirty(_marker);
            }

            Handles.matrix = matrix;
        }

        // ─── Polygon 模式：顶点 Handle ───

        private void DrawPolygonHandles()
        {
            if (_marker == null || _marker.Vertices.Count == 0) return;

            var transform = _marker.transform;
            var verts = _marker.Vertices;

            // 绘制每个顶点的可拖拽 Handle
            for (int i = 0; i < verts.Count; i++)
            {
                var worldPos = transform.TransformPoint(verts[i]);
                float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.08f;

                // 顶点颜色
                Handles.color = Color.white;

                // Shift+点击删除顶点
                if (Event.current.shift)
                {
                    Handles.color = Color.red;
                    if (Handles.Button(worldPos, Quaternion.identity, handleSize * 1.5f, handleSize * 2f, Handles.DotHandleCap))
                    {
                        if (verts.Count > 3) // 至少保留 3 个顶点
                        {
                            Undo.RecordObject(_marker, "删除区域顶点");
                            verts.RemoveAt(i);
                            EditorUtility.SetDirty(_marker);
                            return; // 数组已变，退出本帧
                        }
                    }
                }
                else
                {
                    // 正常拖拽
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
                        Undo.RecordObject(_marker, "移动区域顶点");
                        verts[i] = transform.InverseTransformPoint(newWorldPos);
                        EditorUtility.SetDirty(_marker);
                    }
                }

                // 顶点序号标签
                Handles.color = Color.white;
                Handles.Label(worldPos + Vector3.up * 0.3f, $"[{i}]",
                    new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.white } });
            }

            // 绘制边中点（点击添加新顶点）
            DrawEdgeMidpoints(transform, verts);
        }

        /// <summary>在每条边的中点绘制可点击的"+"按钮，点击后在该位置插入新顶点</summary>
        private void DrawEdgeMidpoints(Transform transform, List<Vector3> verts)
        {
            if (_marker == null || Event.current.shift) return; // Shift 模式下不显示中点

            Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);

            for (int i = 0; i < verts.Count; i++)
            {
                int next = (i + 1) % verts.Count;
                var midLocal = (verts[i] + verts[next]) * 0.5f;
                var midWorld = transform.TransformPoint(midLocal);
                float handleSize = HandleUtility.GetHandleSize(midWorld) * 0.05f;

                if (Handles.Button(midWorld, Quaternion.identity, handleSize, handleSize * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(_marker, "插入区域顶点");
                    verts.Insert(next, midLocal);
                    EditorUtility.SetDirty(_marker);
                    return;
                }
            }
        }
    }
}
