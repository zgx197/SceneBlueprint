#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.SpatialModes;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 标记组多步创建工具。
    /// <para>
    /// 流程：
    /// 1. 设计师触发创建 → 进入"放置模式"
    /// 2. 连续点击放置组成员点
    /// 3. 右键或 Escape 结束放置 → 创建 MarkerGroup
    /// </para>
    /// </summary>
    public static class MarkerGroupCreationTool
    {
        // ─── 状态 ───

        private static bool _isPlacing;
        private static MarkerGroup? _currentGroup;
        private static List<GameObject> _tempMembers = new();
        private static IEditorSpatialModeDescriptor? _spatialMode;

        /// <summary>当前是否处于放置模式</summary>
        public static bool IsPlacing => _isPlacing;

        /// <summary>标记组创建完成时的回调</summary>
        public static event System.Action<MarkerGroup>? OnGroupCreated;

        // ─── 公共接口 ───

        /// <summary>
        /// 设置空间模式描述器（用于获取鼠标点击位置）
        /// </summary>
        public static void SetSpatialMode(IEditorSpatialModeDescriptor? spatialMode)
        {
            _spatialMode = spatialMode;
        }

        /// <summary>
        /// 开始创建标记组（进入放置模式）
        /// </summary>
        public static void BeginCreateGroup(Vector3 startPosition, string groupName, string tag = "")
        {
            if (_isPlacing)
            {
                SBLog.Warn(SBLogTags.Marker, "已经在创建标记组中，请先完成当前组");
                return;
            }

            _isPlacing = true;
            _tempMembers.Clear();

            // 创建标记组 GameObject（但暂时不添加成员）
            var groupObj = new GameObject($"Group_{groupName}");
            Undo.RegisterCreatedObjectUndo(groupObj, $"创建标记组 {groupName}");

            var parent = MarkerHierarchyManager.GetOrCreateGroup(null);
            groupObj.transform.SetParent(parent);
            groupObj.transform.position = startPosition;

            _currentGroup = groupObj.AddComponent<MarkerGroup>();
            _currentGroup.MarkerName = groupName;
            _currentGroup.Tag = tag;
            _currentGroup.GroupType = MarkerGroupType.Point;

            SBLog.Info(SBLogTags.Marker, 
                $"开始创建标记组 '{groupName}'，点击放置成员点，右键或按 Escape 完成");

            // 添加 SceneView 事件监听
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// 完成创建（退出放置模式）
        /// </summary>
        public static void FinishCreateGroup()
        {
            if (!_isPlacing || _currentGroup == null)
                return;

            // 移除 SceneView 事件监听
            SceneView.duringSceneGui -= OnSceneGUI;

            // 至少需要 1 个成员
            if (_tempMembers.Count == 0)
            {
                SBLog.Warn(SBLogTags.Marker, "标记组至少需要 1 个成员，已取消创建");
                if (_currentGroup != null)
                {
                    Undo.DestroyObjectImmediate(_currentGroup.gameObject);
                }
                _isPlacing = false;
                _currentGroup = null;
                _tempMembers.Clear();
                return;
            }

            // 将临时成员添加到组
            foreach (var member in _tempMembers)
            {
                if (member != null)
                {
                    _currentGroup.AddMember(member.transform);
                }
            }

            SBLog.Info(SBLogTags.Marker, 
                $"完成创建标记组 '{_currentGroup.MarkerName}'，共 {_currentGroup.Members.Count} 个成员");

            // 触发回调
            var finishedGroup = _currentGroup;
            OnGroupCreated?.Invoke(finishedGroup);

            // 选中创建的组
            Selection.activeGameObject = finishedGroup.gameObject;

            // 清理状态
            _isPlacing = false;
            _currentGroup = null;
            _tempMembers.Clear();

            SceneView.RepaintAll();
        }

        /// <summary>
        /// 取消创建（删除已创建的内容）
        /// </summary>
        public static void CancelCreateGroup()
        {
            if (!_isPlacing)
                return;

            // 移除 SceneView 事件监听
            SceneView.duringSceneGui -= OnSceneGUI;

            // 删除临时成员和组对象
            foreach (var member in _tempMembers)
            {
                if (member != null)
                {
                    Undo.DestroyObjectImmediate(member);
                }
            }

            if (_currentGroup != null)
            {
                Undo.DestroyObjectImmediate(_currentGroup.gameObject);
            }

            SBLog.Info(SBLogTags.Marker, "已取消创建标记组");

            _isPlacing = false;
            _currentGroup = null;
            _tempMembers.Clear();

            SceneView.RepaintAll();
        }

        // ─── SceneView 事件处理 ───

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_isPlacing || _currentGroup == null || _spatialMode == null)
                return;

            var evt = Event.current;

            // 绘制放置提示
            DrawPlacementHint();

            // 处理输入
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                // 左键点击 → 放置成员点
                if (TryGetSceneViewPosition(evt.mousePosition, sceneView, out var worldPos))
                {
                    AddMemberPoint(worldPos);
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                // 右键 → 完成创建
                FinishCreateGroup();
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                // Escape → 取消创建
                CancelCreateGroup();
                evt.Use();
            }

            // 强制重绘以显示实时预览
            sceneView.Repaint();
        }

        /// <summary>
        /// 添加成员点
        /// </summary>
        private static void AddMemberPoint(Vector3 position)
        {
            if (_currentGroup == null)
                return;

            int index = _tempMembers.Count + 1;
            var memberObj = new GameObject($"Point_{index:D2}");
            Undo.RegisterCreatedObjectUndo(memberObj, $"添加组成员点 {index}");

            memberObj.transform.SetParent(_currentGroup.transform);
            memberObj.transform.position = position;

            _tempMembers.Add(memberObj);

            SBLog.Debug(SBLogTags.Marker, 
                $"添加成员点 {index}，位置: {position}，共 {_tempMembers.Count} 个点");
        }

        /// <summary>
        /// 绘制放置提示（在 SceneView 中显示）
        /// </summary>
        private static void DrawPlacementHint()
        {
            if (_currentGroup == null)
                return;

            Handles.BeginGUI();
            
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };

            var content = $"创建标记组: {_currentGroup.MarkerName}\n" +
                         $"成员数: {_tempMembers.Count}\n" +
                         $"━━━━━━━━━━━━━━━━━━\n" +
                         $"左键点击: 添加成员点\n" +
                         $"右键: 完成创建\n" +
                         $"Escape: 取消";

            var size = style.CalcSize(new GUIContent(content));
            var rect = new Rect(10, 10, size.x, size.y);

            GUI.Box(rect, content, style);
            
            Handles.EndGUI();
        }

        /// <summary>
        /// 从鼠标位置获取世界坐标
        /// </summary>
        private static bool TryGetSceneViewPosition(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            if (_spatialMode == null)
            {
                worldPos = Vector3.zero;
                return false;
            }

            return _spatialMode.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }
    }
}
