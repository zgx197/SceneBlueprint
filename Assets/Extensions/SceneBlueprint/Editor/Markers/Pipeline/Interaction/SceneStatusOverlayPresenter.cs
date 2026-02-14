#nullable enable
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// SceneView 状态叠层提示实现。
    /// 仅负责可视提示，不参与输入或命中逻辑。
    /// </summary>
    internal sealed class SceneStatusOverlayPresenter : IMarkerOverlayPresenter
    {
        private GUIStyle? _sceneStatusStyle;

        public void Draw(
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            bool canCreateMarker)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string modeText = interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Edit
                ? "交互模式：编辑（单击选中 + 原生变换）"
                : "交互模式：拾取（自定义选中）";
            string createText = canCreateMarker
                ? "标记创建：可用（Shift + 右键）"
                : "标记创建：不可用（请打开 SceneBlueprint 窗口）";

            var style = GetSceneStatusStyle();
            var rect = new Rect(12f, 28f, 360f, 44f);

            Handles.BeginGUI();
            GUI.Label(rect, modeText + "\n" + createText, style);
            Handles.EndGUI();
        }

        private GUIStyle GetSceneStatusStyle()
        {
            if (_sceneStatusStyle != null)
                return _sceneStatusStyle;

            _sceneStatusStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                richText = false,
                padding = new RectOffset(8, 8, 4, 4)
            };

            return _sceneStatusStyle;
        }
    }
}
