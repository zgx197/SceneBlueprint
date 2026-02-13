#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 绘制管线——统一调度所有标记的 Scene View 绘制。
    /// <para>
    /// 通过 <see cref="SceneView.duringSceneGui"/> 注册单一回调，
    /// 按 <see cref="DrawPhase"/> 顺序遍历所有可见标记，调用对应
    /// <see cref="IMarkerGizmoRenderer"/> 的 Phase 方法。
    /// </para>
    /// <para>
    /// 特性：
    /// - 严格的绘制顺序（Fill → Wireframe → Icon → Interactive → Highlight → Label → Pick）
    /// - 视锥裁剪（仅绘制摄像机可见的标记）
    /// - 标记缓存（通过 <see cref="MarkerCache"/>，不每帧 FindObjectsOfType）
    /// - 自动发现 Renderer（反射扫描所有 IMarkerGizmoRenderer 实现）
    /// - Interactive Phase 接管机制（选中时 Renderer 可替代 Fill/Wireframe）
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GizmoRenderPipeline
    {
        // ─── 渲染器注册表 ───
        private static readonly Dictionary<Type, IMarkerGizmoRenderer> _renderers = new();

        // ─── 每帧复用的列表（避免 GC）───
        private static readonly List<GizmoDrawContext> _drawList = new();
        private static readonly HashSet<SceneMarker> _interactiveSet = new();

        // ─── 拾取状态 ───
        private static int _pickControlId;
        private static bool _pendingPick;

        static GizmoRenderPipeline()
        {
            AutoDiscoverRenderers();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ─── 注册 ───

        /// <summary>手动注册 Renderer（供第三方扩展或测试）</summary>
        public static void RegisterRenderer(IMarkerGizmoRenderer renderer)
        {
            _renderers[renderer.TargetType] = renderer;
        }

        /// <summary>获取已注册的 Renderer 数量（调试用）</summary>
        public static int RendererCount => _renderers.Count;

        /// <summary>反射自动发现并注册当前程序集中所有 IMarkerGizmoRenderer 实现</summary>
        private static void AutoDiscoverRenderers()
        {
            _renderers.Clear();

            var interfaceType = typeof(IMarkerGizmoRenderer);
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!interfaceType.IsAssignableFrom(type)) continue;

                try
                {
                    var renderer = (IMarkerGizmoRenderer)Activator.CreateInstance(type);
                    _renderers[renderer.TargetType] = renderer;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GizmoPipeline] 无法实例化 Renderer {type.Name}: {ex.Message}");
                }
            }

            if (_renderers.Count > 0)
            {
                var names = string.Join(", ", _renderers.Values.Select(r => r.GetType().Name));
                Debug.Log($"[GizmoPipeline] 已注册 {_renderers.Count} 个 Renderer: {names}");
            }
        }

        // ─── 主循环 ───

        private static void OnSceneGUI(SceneView sceneView)
        {
            // 获取缓存的标记列表
            var allMarkers = MarkerCache.GetAll();
            if (allMarkers.Count == 0) return;
            if (_renderers.Count == 0) return;

            // 预计算公共时间和脉冲值
            float time = (float)EditorApplication.timeSinceStartup;
            float pulseScale = GizmoStyleConstants.CalcPulseScale(time);
            float pulseAlpha = GizmoStyleConstants.CalcPulseAlpha(time);

            // 视锥裁剪 planes
            var camera = sceneView.camera;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            // ── 构建绘制列表（过滤图层 + 视锥裁剪）───
            _drawList.Clear();
            foreach (var marker in allMarkers)
            {
                if (marker == null) continue;

                // 图层可见性过滤
                if (!MarkerLayerSystem.IsMarkerVisible(marker.GetLayerPrefix())) continue;

                // 视锥裁剪
                var bounds = GetMarkerBounds(marker);
                if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;

                // 构建绘制上下文
                _drawList.Add(BuildContext(marker, pulseScale, pulseAlpha));
            }

            if (_drawList.Count == 0) return;

            // ── Phase 3 Interactive 先行执行（记录接管标记集合）───
            _interactiveSet.Clear();
            ExecuteInteractivePhase();

            // ── 按 Phase 顺序绘制 ───
            ExecutePhase(DrawPhase.Fill);
            ExecutePhase(DrawPhase.Wireframe);
            ExecutePhase(DrawPhase.Icon);
            ExecutePhase(DrawPhase.Highlight);
            ExecutePhase(DrawPhase.Label);

            // ── 拾取处理 ───
            HandlePicking();
        }

        // ─── 上下文构建 ───

        private static GizmoDrawContext BuildContext(SceneMarker marker, float pulseScale, float pulseAlpha)
        {
            var transform = marker.transform;
            var pos = transform.position;

            bool isSelected = Selection.activeGameObject == marker.gameObject;
            bool isHighlighted = SceneMarkerSelectionBridge.IsMarkerHighlighted(marker.MarkerId);

            var baseColor = GizmoStyleConstants.GetLayerColor(marker);
            var effectiveColor = isHighlighted
                ? GizmoStyleConstants.GetHighlightColor(baseColor)
                : baseColor;

            return new GizmoDrawContext
            {
                Marker = marker,
                Transform = transform,
                IsSelected = isSelected,
                IsHighlighted = isHighlighted,
                BaseColor = baseColor,
                EffectiveColor = effectiveColor,
                FillColor = GizmoStyleConstants.GetFillColor(baseColor, isSelected),
                PulseScale = isHighlighted ? pulseScale : 1f,
                PulseAlpha = isHighlighted ? pulseAlpha : 1f,
                HandleSize = HandleUtility.GetHandleSize(pos),
            };
        }

        // ─── 阶段执行 ───

        private static void ExecuteInteractivePhase()
        {
            foreach (var ctx in _drawList)
            {
                if (!ctx.IsSelected) continue;
                if (!_renderers.TryGetValue(ctx.Marker.GetType(), out var renderer)) continue;

                if (renderer.DrawInteractive(in ctx))
                    _interactiveSet.Add(ctx.Marker);
            }
        }

        private static void ExecutePhase(DrawPhase phase)
        {
            foreach (var ctx in _drawList)
            {
                if (!_renderers.TryGetValue(ctx.Marker.GetType(), out var renderer))
                    continue;

                // Interactive 接管的标记跳过 Fill/Wireframe
                if (_interactiveSet.Contains(ctx.Marker)
                    && (phase == DrawPhase.Fill || phase == DrawPhase.Wireframe))
                    continue;

                switch (phase)
                {
                    case DrawPhase.Fill:
                        renderer.DrawFill(in ctx);
                        break;
                    case DrawPhase.Wireframe:
                        renderer.DrawWireframe(in ctx);
                        break;
                    case DrawPhase.Icon:
                        renderer.DrawIcon(in ctx);
                        break;
                    case DrawPhase.Highlight:
                        if (ctx.IsHighlighted) renderer.DrawHighlight(in ctx);
                        break;
                    case DrawPhase.Label:
                        renderer.DrawLabel(in ctx);
                        break;
                }
            }
        }

        // ─── 拾取 ───

        private static void HandlePicking()
        {
            _pickControlId = GUIUtility.GetControlID(FocusType.Passive);
            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.Layout:
                    if (GUIUtility.hotControl == 0)
                        HandleUtility.AddDefaultControl(_pickControlId);
                    break;

                case EventType.MouseDown:
                    if (evt.button != 0 || evt.shift || evt.control || evt.alt) break;

                    var picked = FindClosestMarker(evt.mousePosition);
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

        private static SceneMarker? FindClosestMarker(Vector2 mousePos)
        {
            SceneMarker? best = null;
            float bestDist = float.MaxValue;

            foreach (var ctx in _drawList)
            {
                if (!_renderers.TryGetValue(ctx.Marker.GetType(), out var renderer))
                    continue;

                var pickBounds = renderer.GetPickBounds(in ctx);
                float dist = HandleUtility.DistanceToCircle(pickBounds.Center, pickBounds.Radius);

                if (dist < GizmoStyleConstants.PickDistanceThreshold && dist < bestDist)
                {
                    bestDist = dist;
                    best = ctx.Marker;
                }
            }

            return best;
        }

        // ─── Bounds 计算 ───

        /// <summary>
        /// 计算标记的 AABB 包围盒，用于视锥裁剪。
        /// </summary>
        private static Bounds GetMarkerBounds(SceneMarker marker)
        {
            var pos = marker.transform.position;

            switch (marker)
            {
                case PointMarker pm:
                    return new Bounds(pos, Vector3.one * pm.GizmoRadius * 2f);

                case AreaMarker am:
                    if (am.Shape == AreaShape.Box)
                    {
                        var size = am.BoxSize;
                        // 考虑旋转后的包围盒
                        var rotatedSize = am.transform.rotation * size;
                        return new Bounds(pos, new Vector3(
                            Mathf.Abs(rotatedSize.x),
                            Mathf.Abs(rotatedSize.y),
                            Mathf.Abs(rotatedSize.z)));
                    }
                    else
                    {
                        // Polygon：遍历世界坐标顶点计算包围盒
                        var verts = am.GetWorldVertices();
                        if (verts.Count == 0)
                            return new Bounds(pos, Vector3.one * 2f);

                        var bounds = new Bounds(verts[0], Vector3.zero);
                        for (int i = 1; i < verts.Count; i++)
                            bounds.Encapsulate(verts[i]);
                        // 扩展高度
                        bounds.Encapsulate(bounds.center + Vector3.up * am.Height);
                        return bounds;
                    }

                case EntityMarker:
                    return new Bounds(pos, Vector3.one * 2f);

                default:
                    return new Bounds(pos, Vector3.one * 2f);
            }
        }
    }
}
