#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// 默认的标记选中控制器。
    ///
    /// 职责：
    /// 1) Pick 模式：使用“强接管”拾取（AddDefaultControl + hotControl + evt.Use）。
    /// 2) Edit 模式：使用“自然单击选中”（不抢占 Unity 原生变换输入）。
    ///
    /// 说明：
    /// - 本类只处理输入仲裁与 Selection 提交，不负责渲染。
    /// - 命中计算由 <see cref="IMarkerHitTestService"/> 提供，便于后续替换命中策略。
    /// </summary>
    internal sealed class DefaultMarkerSelectionController : IMarkerSelectionController
    {
        // Pick 模式状态（强接管）
        private int _pickControlId;
        private bool _pendingPick;

        // Edit 模式状态（自然单击）
        private SceneMarker? _editClickCandidate;
        private bool _editClickPending;
        private Vector2 _editMouseDownPos;
        private const float EditClickMaxDragPixels = 4f;

        public void ResetState()
        {
            // 仅在我们自己接管的拾取流程中释放 hotControl，避免影响其他工具。
            if (_pendingPick && _pickControlId != 0 && GUIUtility.hotControl == _pickControlId)
                GUIUtility.hotControl = 0;

            _pendingPick = false;
            _pickControlId = 0;
            ClearEditClickState();
        }

        public void Handle(
            Event evt,
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            if (evt == null)
            {
                ResetState();
                return;
            }

            if (interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Pick)
            {
                if (CanHandlePick(evt))
                    HandleLegacyPickMode(evt, hitTestService, drawList, renderers);
                else if (evt.type == EventType.Ignore || evt.type == EventType.Used)
                    _pendingPick = false;

                return;
            }

            if (interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Edit)
            {
                if (CanHandleEditClickSelect(evt))
                    HandleEditClickSelect(evt, hitTestService, drawList, renderers);
                else if (evt.type == EventType.Ignore || evt.type == EventType.Used)
                    ClearEditClickState();

                return;
            }

            // 未知模式兜底：清理状态，避免残留。
            ResetState();
        }

        private static bool CanHandlePick(Event evt)
        {
            // Alt/视图工具用于导航相机，不应被拾取层拦截。
            if (evt.alt || Tools.viewToolActive)
                return false;

            return true;
        }

        private static bool CanHandleEditClickSelect(Event evt)
        {
            // 文本输入时（如 Inspector 文本框）不应触发场景选中。
            if (EditorGUIUtility.editingTextField)
                return false;

            return true;
        }

        /// <summary>
        /// Edit 模式下的“自然单击选中”流程。
        /// 不抢占 hotControl，也不消费事件，让位给 Unity 原生变换。
        /// </summary>
        private void HandleEditClickSelect(
            Event evt,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (!CanStartEditClickSelection(evt))
                    {
                        ClearEditClickState();
                        break;
                    }

                    _editClickPending = true;
                    _editMouseDownPos = evt.mousePosition;
                    _editClickCandidate = hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    break;

                case EventType.MouseDrag:
                    if (!_editClickPending)
                        break;

                    float sqrDrag = (evt.mousePosition - _editMouseDownPos).sqrMagnitude;
                    if (sqrDrag > EditClickMaxDragPixels * EditClickMaxDragPixels)
                        ClearEditClickState();
                    break;

                case EventType.MouseUp:
                    if (!_editClickPending || evt.button != 0)
                    {
                        ClearEditClickState();
                        break;
                    }

                    // 兜底：MouseDown 阶段未命中时，MouseUp 再试一次命中。
                    var resolved = _editClickCandidate
                        ?? hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    if (resolved != null)
                        Selection.activeGameObject = resolved.gameObject;

                    ClearEditClickState();
                    break;

                case EventType.Ignore:
                case EventType.Used:
                    ClearEditClickState();
                    break;
            }
        }

        private static bool CanStartEditClickSelection(Event evt)
        {
            if (evt.button != 0)
                return false;

            // Alt/视图工具用于相机导航，不应触发选中。
            if (evt.alt || Tools.viewToolActive)
                return false;

            // Ctrl/Shift 保留给框选/多选与项目内其他快捷交互。
            if (evt.control || evt.shift)
                return false;

            return true;
        }

        /// <summary>
        /// Pick 模式（强接管）。
        /// 通过 defaultControl/hotControl 获得稳定命中与点击反馈。
        /// </summary>
        private void HandleLegacyPickMode(
            Event evt,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            _pickControlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (evt.type)
            {
                case EventType.Layout:
                    if (GUIUtility.hotControl == 0)
                        HandleUtility.AddDefaultControl(_pickControlId);
                    break;

                case EventType.MouseDown:
                    if (evt.button != 0 || evt.shift || evt.control || evt.alt) break;
                    if (HandleUtility.nearestControl != _pickControlId) break;

                    var picked = hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    if (picked != null)
                    {
                        GUIUtility.hotControl = _pickControlId;
                        Selection.activeGameObject = picked.gameObject;
                        _pendingPick = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_pendingPick && evt.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        _pendingPick = false;
                        evt.Use();
                    }
                    break;
            }
        }

        private void ClearEditClickState()
        {
            _editClickCandidate = null;
            _editClickPending = false;
            _editMouseDownPos = Vector2.zero;
        }
    }
}
