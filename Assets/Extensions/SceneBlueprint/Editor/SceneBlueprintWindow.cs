#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.View;
using NodeGraph.Unity;
using NodeGraph.Serialization;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Export;
using SceneBlueprint.Editor.Export;
using GraphPort = NodeGraph.Core.Port;
using GraphPortDefinition = NodeGraph.Core.PortDefinition;
using CoreValidationSeverity = SceneBlueprint.Core.ValidationSeverity;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Preview;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 蓝图编辑器窗口。
    /// 作为 NodeGraph 框架的 Unity 宿主窗口，驱动 GraphViewModel 的
    /// ProcessInput → BuildFrame → Render 主循环。
    /// 布局：工具栏 | 画布区域 | Inspector 面板（可拖拽分栏）
    /// </summary>
    public class SceneBlueprintWindow : EditorWindow
    {
        // ── NodeGraph 核心组件 ──
        private GraphViewModel? _viewModel;
        private UnityGraphRenderer? _renderer;
        private UnityPlatformInput? _input;
        private UnityEditContext? _editContext;
        private CanvasCoordinateHelper? _coordinateHelper;
        private BlueprintProfile? _profile;

        // ── Inspector 面板 ──
        private InspectorPanel? _inspectorPanel;
        private ActionNodeInspectorDrawer? _inspectorDrawer;

        // ── 蓝图资产 ──
        [SerializeField] private BlueprintAsset? _currentAsset;

        // ── Domain Reload 恢复 ──
        [SerializeField] private string _graphJsonBeforeReload = "";

        // ── 场景绑定 ──
        private BindingContext? _bindingContext;

        // ── 布局参数 ──
        private const float ToolbarHeight = 22f;
        private const float MinInspectorWidth = 200f;
        private const float MaxInspectorWidth = 500f;
        private const float DefaultInspectorWidth = 280f;
        private const float MinWorkbenchWidth = 220f;
        private const float MaxWorkbenchWidth = 520f;
        private const float DefaultWorkbenchWidth = 300f;
        private const float SplitterWidth = 4f;
        private const float MinCanvasWidth = 260f;
        private const string WorkbenchVisiblePrefsKey = "SceneBlueprint.Workbench.Visible";
        private const string WorkbenchWidthPrefsKey = "SceneBlueprint.Workbench.Width";
        private float _inspectorWidth = DefaultInspectorWidth;
        private float _workbenchWidth = DefaultWorkbenchWidth;
        private bool _isDraggingSplitter;
        private bool _isDraggingWorkbenchSplitter;
        private bool _showWorkbench = true;
        private bool _useEditorToolSelectionInput = true;
        private Core.ActionRegistry? _actionRegistryCache;
        private IEditorSpatialModeDescriptor? _spatialModeDescriptor;
        private readonly SceneBlueprintToolContext _toolContext = new SceneBlueprintToolContext();
        private readonly ISceneBindingStore _sceneBindingStore = new SceneManagerBindingStore();
        private readonly HashSet<string> _dirtyPreviewNodeIds = new HashSet<string>();
        private bool _previewDirtyAll;
        private bool _previewFlushScheduled;
        private Vector2 _blackboardScrollPos;
        private readonly Dictionary<string, HashSet<string>> _previewMarkerToNodeIds =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, string> _previewNodeToMarkerId =
            new Dictionary<string, string>();
        private readonly HashSet<string> _previewObservedNodeIds = new HashSet<string>();
        private readonly Dictionary<string, int> _previewObservedMarkerSignatures =
            new Dictionary<string, int>();
        private int _previewObservedSubGraphFrameCount = -1;


        [MenuItem("SceneBlueprint/蓝图编辑器 &B")]
        public static void Open()
        {
            var window = GetWindow<SceneBlueprintWindow>();
            window.titleContent = new GUIContent("场景蓝图编辑器");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _spatialModeDescriptor = SpatialModeRegistry.GetProjectModeDescriptor();

            _showWorkbench = EditorPrefs.GetBool(WorkbenchVisiblePrefsKey, true);
            _workbenchWidth = EditorPrefs.GetFloat(WorkbenchWidthPrefsKey, DefaultWorkbenchWidth);
            _useEditorToolSelectionInput = MarkerSelectionInputRoutingSettings.LoadUseEditorTool();
            _toolContext.Attach(_useEditorToolSelectionInput);
            GizmoRenderPipeline.SetInteractionMode(GizmoRenderPipeline.MarkerInteractionMode.Edit);

            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            EditorApplication.projectChanged -= OnEditorProjectChanged;
            EditorApplication.projectChanged += OnEditorProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            Undo.postprocessModifications -= OnUndoPostprocessModifications;
            Undo.postprocessModifications += OnUndoPostprocessModifications;

            // Domain Reload 恢复：检测是否有之前保存的图数据
            if (!string.IsNullOrEmpty(_graphJsonBeforeReload))
            {
                TryRestoreAfterDomainReload();
            }
            else
            {
                InitializeIfNeeded();
            }

            if (_viewModel != null && _viewModel.Graph.Nodes.Count > 0)
                MarkPreviewDirtyAll("OnEnable");
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(WorkbenchVisiblePrefsKey, _showWorkbench);
            EditorPrefs.SetFloat(WorkbenchWidthPrefsKey, _workbenchWidth);
            MarkerSelectionInputRoutingSettings.SaveUseEditorTool(_useEditorToolSelectionInput);

            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            EditorApplication.projectChanged -= OnEditorProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.postprocessModifications -= OnUndoPostprocessModifications;
            EditorApplication.delayCall -= FlushDirtyPreviews;

            _toolContext.Detach();

            // 取消双向联动订阅
            if (_viewModel != null)
                _viewModel.Selection.OnSelectionChanged -= OnBlueprintSelectionChanged;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested -= OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested -= OnSceneMarkerDoubleClicked;
            SceneMarkerSelectionBridge.ClearHighlight();
            Selection.selectionChanged -= OnUnitySelectionChanged;

            // Domain Reload 前保存图数据（_currentAsset 由 [SerializeField] 自动保留）
            if (_viewModel != null)
            {
                try
                {
                    var serializer = CreateGraphSerializer();
                    _graphJsonBeforeReload = serializer.Serialize(_viewModel.Graph);
                }
                catch (System.Exception ex)
                {
                    SBLog.Error(SBLogTags.Blueprint, $"Domain Reload 前保存图数据失败: {ex.Message}");
                    _graphJsonBeforeReload = "";
                }
            }

            // 清理预览
            BlueprintPreviewManager.Instance.ClearAllPreviews();

            _viewModel = null;
            _renderer = null;
            _input = null;
            _editContext = null;
            _coordinateHelper = null;
            _profile = null;
            _inspectorPanel = null;
            _inspectorDrawer = null;
            // 注意：_currentAsset 不清空，由 [SerializeField] 在 Domain Reload 后保留
            _bindingContext = null;
            _actionRegistryCache = null;
            _dirtyPreviewNodeIds.Clear();
            _previewMarkerToNodeIds.Clear();
            _previewNodeToMarkerId.Clear();
            _previewDirtyAll = false;
            _previewFlushScheduled = false;
            ResetPreviewGraphShapeSnapshot();
        }

        /// <summary>
        /// Domain Reload 后恢复编辑中的蓝图。
        /// 从 _graphJsonBeforeReload 反序列化图数据，恢复到编辑状态。
        /// </summary>
        private void TryRestoreAfterDomainReload()
        {
            try
            {
                var serializer = CreateGraphSerializer();
                var graph = serializer.Deserialize(_graphJsonBeforeReload);
                _graphJsonBeforeReload = "";

                InitializeWithGraph(graph);
                RestoreBindingsFromScene();

                SBLog.Info(SBLogTags.Blueprint,
                    $"Domain Reload 后恢复蓝图成功（节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count}）");
            }
            catch (System.Exception ex)
            {
                SBLog.Error(SBLogTags.Blueprint, $"Domain Reload 后恢复蓝图失败: {ex.Message}");
                _graphJsonBeforeReload = "";
                InitializeIfNeeded();
            }
        }

        /// <summary>初始化所有 NodeGraph 组件（仅首次或丢失引用时）</summary>
        private void InitializeIfNeeded()
        {
            if (_viewModel != null) return;
            InitializeWithGraph(null);
        }

        /// <summary>
        /// 用指定的 Graph 初始化编辑器（传 null 则创建空白图）。
        /// 每次调用会重建所有组件。
        /// </summary>
        private void InitializeWithGraph(Graph? existingGraph)
        {
            _dirtyPreviewNodeIds.Clear();
            _previewDirtyAll = false;
            _previewFlushScheduled = false;
            _previewMarkerToNodeIds.Clear();
            _previewNodeToMarkerId.Clear();
            _previewObservedMarkerSignatures.Clear();
            EditorApplication.delayCall -= FlushDirtyPreviews;

            // 1. 确定使用哪个 GraphSettings
            //    加载已有图时，必须把 Action 类型注册到该图自己的 NodeTypeRegistry 中，
            //    否则 FrameBuilder 查不到 NodeTypeDef，节点颜色会 fallback 到灰色。
            var settings = existingGraph?.Settings
                ?? new GraphSettings { Topology = GraphTopologyPolicy.DAG };

            // 1b. 注入 SceneBlueprint 专用的连接策略（支持 Flow/Event/Data 端口验证）
            settings.ConnectionPolicy = new SceneBlueprintConnectionPolicy();

            // 2. 创建 Profile，将 Action 类型注册到 settings.NodeTypes 中
            var textMeasurer = new UnityTextMeasurer();
            _profile = SceneBlueprintProfile.Create(textMeasurer, settings.NodeTypes);

            // 3. 使用已有图或创建空白图
            var graph = existingGraph ?? new Graph(settings);
            SyncPreviewGraphShapeSnapshot(graph);

            // 4. 创建 ViewModel
            _viewModel = new GraphViewModel(graph)
            {
                FrameBuilder = _profile.FrameBuilder,
                Theme = _profile.Theme,
                EdgeLabelRenderer = _profile.EdgeLabelRenderer
            };

            // 将内容渲染器注册到 ViewModel
            foreach (var kvp in _profile.ContentRenderers)
            {
                _viewModel.ContentRenderers[kvp.Key] = kvp.Value;
            }

            // 5. 订阅上下文菜单事件（框架层 Handler 触发，宿主窗口响应）
            _viewModel.OnContextMenuRequested = OnCanvasContextMenu;
            _viewModel.OnNodeContextMenuRequested = OnNodeContextMenu;
            _viewModel.OnPortContextMenuRequested = OnPortContextMenu;

            // 6. 创建 Unity 适配层组件
            _input = new UnityPlatformInput();
            _editContext = new UnityEditContext();
            _coordinateHelper = new CanvasCoordinateHelper();
            _renderer = new UnityGraphRenderer(
                _viewModel.ContentRenderers,
                _viewModel.EdgeLabelRenderer
            );

            // 7. 创建 Inspector 面板 + 绑定上下文
            var actionRegistry = SceneBlueprintProfile.CreateActionRegistry();
            _inspectorDrawer = new ActionNodeInspectorDrawer(actionRegistry);
            _inspectorDrawer.OnPropertyChanged = OnNodePropertyChanged;
            _inspectorPanel = new InspectorPanel(_inspectorDrawer);

            if (_bindingContext == null)
                _bindingContext = new BindingContext();
            _inspectorDrawer.SetBindingContext(_bindingContext);
            _inspectorDrawer.SetGraph(_viewModel.Graph);
            _inspectorDrawer.SetVariableDeclarations(_currentAsset?.Variables);
            RebuildPreviewMarkerNodeIndex();

            // 8. 启用 Scene View 标记工具（P3：由 ToolContext 托管生命周期）
            _toolContext.EnableMarkerTool(actionRegistry, EnsureSpatialModeDescriptor());
            // 9. 双向联动：订阅蓝图选中变化 + 场景侧事件 + Unity 场景选中监听
            _viewModel.Selection.OnSelectionChanged -= OnBlueprintSelectionChanged;
            _viewModel.Selection.OnSelectionChanged += OnBlueprintSelectionChanged;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested -= OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested += OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested -= OnSceneMarkerDoubleClicked;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested += OnSceneMarkerDoubleClicked;
            Selection.selectionChanged -= OnUnitySelectionChanged;
            Selection.selectionChanged += OnUnitySelectionChanged;

            // 10. 更新窗口标题
            UpdateTitle();

            // 初始化完成后尝试刷新预览（支持未保存蓝图）
            if (graph.Nodes.Count > 0)
                MarkPreviewDirtyAll("InitializeWithGraph");
        }

        private JsonGraphSerializer CreateGraphSerializer()
        {
            return new JsonGraphSerializer(new ActionNodeDataSerializer());
        }

        private void UpdateTitle()
        {
            string name = _currentAsset != null && !string.IsNullOrEmpty(_currentAsset.BlueprintName)
                ? _currentAsset.BlueprintName
                : "未保存";
            titleContent = new GUIContent($"场景蓝图编辑器 - {name}");
        }

        private string GetPreviewContextId()
        {
            if (_currentAsset != null && !string.IsNullOrEmpty(_currentAsset.BlueprintId))
                return _currentAsset.BlueprintId!;

            return $"unsaved:{GetInstanceID()}";
        }

        /// <summary>
        /// 标记单节点预览为脏，并在当前编辑器循环末尾合并刷新。
        /// </summary>
        private void MarkPreviewDirtyForNode(string nodeId, string reason)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            MarkPreviewDirtyForNodes(new[] { nodeId }, reason);
        }

        /// <summary>
        /// 标记一组节点预览为脏，并合并到下一次刷新批次。
        /// </summary>
        private void MarkPreviewDirtyForNodes(IEnumerable<string> nodeIds, string reason)
        {
            if (_previewDirtyAll)
                return;

            int addedCount = 0;
            foreach (var nodeId in nodeIds)
            {
                if (string.IsNullOrEmpty(nodeId))
                    continue;

                if (_dirtyPreviewNodeIds.Add(nodeId))
                    addedCount++;
            }

            if (addedCount <= 0)
                return;

            SBLog.Debug(
                SBLogTags.Pipeline,
                "MarkPreviewDirtyForNodes: added={0}, reason={1}, pendingNodes={2}",
                addedCount,
                reason,
                _dirtyPreviewNodeIds.Count);

            SchedulePreviewFlush();
        }

        private static bool TryGetRandomAreaMarkerId(ActionNodeData nodeData, out string markerId)
        {
            markerId = "";
            if (nodeData.ActionTypeId != "Location.RandomArea")
                return false;

            markerId = nodeData.Properties.Get<string>("area") ?? "";
            return !string.IsNullOrEmpty(markerId);
        }

        private void RemovePreviewMarkerNodeIndexForNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            if (!_previewNodeToMarkerId.TryGetValue(nodeId, out string markerId))
                return;

            _previewNodeToMarkerId.Remove(nodeId);

            if (_previewMarkerToNodeIds.TryGetValue(markerId, out var nodeSet))
            {
                nodeSet.Remove(nodeId);
                if (nodeSet.Count == 0)
                    _previewMarkerToNodeIds.Remove(markerId);
            }
        }

        private void UpdatePreviewMarkerNodeIndexForNode(string nodeId, ActionNodeData? nodeData)
        {
            RemovePreviewMarkerNodeIndexForNode(nodeId);

            if (string.IsNullOrEmpty(nodeId) || nodeData == null)
                return;

            if (!TryGetRandomAreaMarkerId(nodeData, out string markerId))
                return;

            if (!_previewMarkerToNodeIds.TryGetValue(markerId, out var nodeSet))
            {
                nodeSet = new HashSet<string>();
                _previewMarkerToNodeIds[markerId] = nodeSet;
            }

            nodeSet.Add(nodeId);
            _previewNodeToMarkerId[nodeId] = markerId;
        }

        private void RebuildPreviewMarkerNodeIndex()
        {
            _previewMarkerToNodeIds.Clear();
            _previewNodeToMarkerId.Clear();

            if (_viewModel == null)
                return;

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is ActionNodeData nodeData)
                    UpdatePreviewMarkerNodeIndexForNode(node.Id, nodeData);
            }
        }

        /// <summary>
        /// 根据 Area MarkerId 定位并标记相关随机区域节点。
        /// </summary>
        private int MarkPreviewDirtyForNodesByAreaMarkerIds(IEnumerable<string> markerIds, string reason)
        {
            if (_viewModel == null)
                return 0;

            var markerIdSet = new HashSet<string>(
                markerIds.Where(id => !string.IsNullOrEmpty(id)));

            if (markerIdSet.Count == 0)
                return 0;

            var nodeIds = new HashSet<string>();
            foreach (var markerId in markerIdSet)
            {
                if (_previewMarkerToNodeIds.TryGetValue(markerId, out var indexedNodeIds))
                {
                    foreach (var nodeId in indexedNodeIds)
                        nodeIds.Add(nodeId);
                }
            }

            // 索引可能在异常路径下不一致，命中为空时重建一次再查询。
            if (nodeIds.Count == 0)
            {
                RebuildPreviewMarkerNodeIndex();
                foreach (var markerId in markerIdSet)
                {
                    if (_previewMarkerToNodeIds.TryGetValue(markerId, out var indexedNodeIds))
                    {
                        foreach (var nodeId in indexedNodeIds)
                            nodeIds.Add(nodeId);
                    }
                }
            }

            MarkPreviewDirtyForNodes(nodeIds, reason);
            return nodeIds.Count;
        }

        /// <summary>
        /// 标记所有 Location.RandomArea 节点（用于无法精准定位 marker 的兜底路径）。
        /// </summary>
        private int MarkPreviewDirtyForAllRandomAreaNodes(string reason)
        {
            if (_viewModel == null)
                return 0;

            var nodeIds = new List<string>();
            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is ActionNodeData nodeData
                    && nodeData.ActionTypeId == "Location.RandomArea")
                {
                    nodeIds.Add(node.Id);
                }
            }

            MarkPreviewDirtyForNodes(nodeIds, reason);
            return nodeIds.Count;
        }

        /// <summary>
        /// 标记当前尚未生成预览缓存的 RandomArea 节点。
        /// </summary>
        private int MarkPreviewDirtyForUncachedRandomAreaNodes(string reason)
        {
            if (_viewModel == null)
                return 0;

            var cachedNodeIdSet = new HashSet<string>(
                BlueprintPreviewManager.Instance
                    .GetCurrentBlueprintPreviews()
                    .Select(p => p.NodeId));

            var nodeIds = new List<string>();
            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData nodeData)
                    continue;

                if (nodeData.ActionTypeId != "Location.RandomArea")
                    continue;

                if (!cachedNodeIdSet.Contains(node.Id))
                    nodeIds.Add(node.Id);
            }

            MarkPreviewDirtyForNodes(nodeIds, reason);
            return nodeIds.Count;
        }

        private static Dictionary<string, SceneMarker> BuildMarkerLookupById()
        {
            var markerById = new Dictionary<string, SceneMarker>();
            foreach (var marker in MarkerCache.GetAll())
            {
                if (marker == null || string.IsNullOrEmpty(marker.MarkerId))
                    continue;

                markerById[marker.MarkerId] = marker;
            }

            return markerById;
        }

        private static int QuantizeFloat(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private static int ComputeVector3Signature(Vector3 value)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + QuantizeFloat(value.x);
                hash = hash * 31 + QuantizeFloat(value.y);
                hash = hash * 31 + QuantizeFloat(value.z);
                return hash;
            }
        }

        private static int ComputeQuaternionSignature(Quaternion value)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + QuantizeFloat(value.x);
                hash = hash * 31 + QuantizeFloat(value.y);
                hash = hash * 31 + QuantizeFloat(value.z);
                hash = hash * 31 + QuantizeFloat(value.w);
                return hash;
            }
        }

        private static int ComputeMarkerSignature(SceneMarker marker)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (marker.MarkerTypeId?.GetHashCode() ?? 0);
                hash = hash * 31 + ComputeVector3Signature(marker.transform.position);
                hash = hash * 31 + ComputeQuaternionSignature(marker.transform.rotation);
                hash = hash * 31 + ComputeVector3Signature(marker.transform.lossyScale);

                if (marker is AreaMarker areaMarker)
                {
                    hash = hash * 31 + (int)areaMarker.Shape;
                    hash = hash * 31 + ComputeVector3Signature(areaMarker.BoxSize);
                    hash = hash * 31 + QuantizeFloat(areaMarker.Height);

                    if (areaMarker.Vertices != null)
                    {
                        hash = hash * 31 + areaMarker.Vertices.Count;
                        foreach (var vertex in areaMarker.Vertices)
                            hash = hash * 31 + ComputeVector3Signature(vertex);
                    }
                    else
                    {
                        hash = hash * 31;
                    }
                }

                return hash;
            }
        }

        private List<string> CollectChangedPreviewMarkerIds(
            IReadOnlyCollection<string> previewMarkerIds,
            IReadOnlyDictionary<string, SceneMarker> markerById)
        {
            var changedMarkerIds = new List<string>();
            foreach (var markerId in previewMarkerIds)
            {
                int currentSignature = markerById.TryGetValue(markerId, out var marker)
                    ? ComputeMarkerSignature(marker)
                    : int.MinValue;

                if (!_previewObservedMarkerSignatures.TryGetValue(markerId, out int previousSignature)
                    || previousSignature != currentSignature)
                {
                    changedMarkerIds.Add(markerId);
                }
            }

            return changedMarkerIds;
        }

        private void SyncPreviewMarkerSignatureSnapshot()
        {
            _previewObservedMarkerSignatures.Clear();

            var previewMarkerIds = BlueprintPreviewManager.Instance.GetCurrentPreviewMarkerIds();
            if (previewMarkerIds.Count == 0)
                return;

            var markerById = BuildMarkerLookupById();
            foreach (var markerId in previewMarkerIds)
            {
                int signature = markerById.TryGetValue(markerId, out var marker)
                    ? ComputeMarkerSignature(marker)
                    : int.MinValue;

                _previewObservedMarkerSignatures[markerId] = signature;
            }
        }

        private void MarkPreviewDirtyForHierarchyChange()
        {
            if (_viewModel == null)
                return;

            var previewMarkerIds = BlueprintPreviewManager.Instance.GetCurrentPreviewMarkerIds();
            int markerMatchCount = 0;
            if (previewMarkerIds.Count > 0)
            {
                var markerById = BuildMarkerLookupById();
                var changedMarkerIds = CollectChangedPreviewMarkerIds(previewMarkerIds, markerById);

                if (changedMarkerIds.Count > 0)
                {
                    markerMatchCount = MarkPreviewDirtyForNodesByAreaMarkerIds(
                        changedMarkerIds,
                        "HierarchyChanged.MarkerChanged");

                    if (markerMatchCount == 0)
                    {
                        markerMatchCount = MarkPreviewDirtyForAllRandomAreaNodes(
                            "HierarchyChanged.MarkerChangedFallback");
                    }
                }
            }

            int uncachedCount = MarkPreviewDirtyForUncachedRandomAreaNodes(
                "HierarchyChanged.UncachedRandomArea");

            if (previewMarkerIds.Count == 0 && markerMatchCount == 0 && uncachedCount == 0)
                MarkPreviewDirtyForAllRandomAreaNodes("HierarchyChanged.RandomAreaFallback");
        }

        /// <summary>
        /// 标记当前图预览全量刷新。
        /// </summary>
        private void MarkPreviewDirtyAll(string reason)
        {
            _previewDirtyAll = true;
            _dirtyPreviewNodeIds.Clear();
            SBLog.Debug(SBLogTags.Pipeline, "MarkPreviewDirtyAll: reason={0}", reason);
            SchedulePreviewFlush();
        }

        private void SchedulePreviewFlush()
        {
            if (_previewFlushScheduled)
                return;

            _previewFlushScheduled = true;
            EditorApplication.delayCall -= FlushDirtyPreviews;
            EditorApplication.delayCall += FlushDirtyPreviews;
        }

        /// <summary>
        /// 合并执行预览刷新（全量优先于单节点）。
        /// </summary>
        private void FlushDirtyPreviews()
        {
            EditorApplication.delayCall -= FlushDirtyPreviews;
            _previewFlushScheduled = false;

            if (_viewModel == null)
            {
                _previewDirtyAll = false;
                _dirtyPreviewNodeIds.Clear();
                return;
            }

            bool refreshAll = _previewDirtyAll;
            var dirtyNodeIds = new List<string>(_dirtyPreviewNodeIds);
            _previewDirtyAll = false;
            _dirtyPreviewNodeIds.Clear();

            if (!refreshAll && dirtyNodeIds.Count == 0)
                return;

            var graph = _viewModel.Graph;
            string previewContextId = GetPreviewContextId();

            // 刷新前让 Marker 缓存失效，避免使用同帧旧数据。
            MarkerCache.SetDirty();

            if (refreshAll)
            {
                Preview.BlueprintPreviewManager.Instance.RefreshAllPreviews(previewContextId, graph);
                SyncPreviewMarkerSignatureSnapshot();
                SBLog.Debug(
                    SBLogTags.Pipeline,
                    "FlushDirtyPreviews: 全量刷新完成, nodeCount={0}, context={1}",
                    graph.Nodes.Count,
                    previewContextId);
                return;
            }

            int refreshedCount = 0;
            var removedNodeIds = new List<string>();
            foreach (var nodeId in dirtyNodeIds)
            {
                var node = graph.FindNode(nodeId);
                if (node?.UserData is ActionNodeData nodeData)
                {
                    Preview.BlueprintPreviewManager.Instance.RefreshPreviewForNode(previewContextId, nodeId, nodeData);
                    refreshedCount++;
                }
                else
                {
                    removedNodeIds.Add(nodeId);
                }
            }

            int removedCount = removedNodeIds.Count > 0
                ? Preview.BlueprintPreviewManager.Instance.RemovePreviews(removedNodeIds, repaint: false)
                : 0;

            if (refreshedCount > 0 || removedCount > 0)
            {
                SceneView.RepaintAll();
                SyncPreviewMarkerSignatureSnapshot();
            }

            if (refreshedCount > 0 || removedCount > 0)
            {
                SBLog.Debug(
                    SBLogTags.Pipeline,
                    "FlushDirtyPreviews: 单节点刷新完成, refreshed={0}, removed={1}, context={2}",
                    refreshedCount,
                    removedCount,
                    previewContextId);
            }
        }

        private void OnUndoRedoPerformed()
        {
            MarkPreviewDirtyAll("UndoRedo");
        }

        private UndoPropertyModification[] OnUndoPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            if (_viewModel == null || modifications == null || modifications.Length == 0)
                return modifications;

            var changedMarkerIds = new HashSet<string>();
            foreach (var modification in modifications)
                CollectChangedMarkerIds(modification, changedMarkerIds);

            if (changedMarkerIds.Count > 0)
            {
                int matchedCount = MarkPreviewDirtyForNodesByAreaMarkerIds(
                    changedMarkerIds,
                    "Undo.PostprocessMarker");

                if (matchedCount == 0)
                    MarkPreviewDirtyForUncachedRandomAreaNodes("Undo.PostprocessMarker.UncachedFallback");
            }

            return modifications;
        }

        private static void CollectChangedMarkerIds(
            UndoPropertyModification modification,
            ISet<string> markerIds)
        {
            var currentTarget = modification.currentValue.target;
            if (currentTarget is SceneMarker marker && !string.IsNullOrEmpty(marker.MarkerId))
            {
                markerIds.Add(marker.MarkerId);
            }
            else if (currentTarget is Transform transform)
            {
                var transformMarker = transform.GetComponent<SceneMarker>();
                if (transformMarker != null && !string.IsNullOrEmpty(transformMarker.MarkerId))
                    markerIds.Add(transformMarker.MarkerId);
            }

            // MarkerId 字段被修改时，补充旧值用于刷新引用旧ID的节点。
            if (modification.currentValue.target is SceneMarker
                && modification.currentValue.propertyPath == "_markerId")
            {
                string oldMarkerId = modification.previousValue.value ?? "";
                if (!string.IsNullOrEmpty(oldMarkerId))
                    markerIds.Add(oldMarkerId);

                string newMarkerId = modification.currentValue.value ?? "";
                if (!string.IsNullOrEmpty(newMarkerId))
                    markerIds.Add(newMarkerId);
            }
        }

        private void ResetPreviewGraphShapeSnapshot()
        {
            _previewObservedNodeIds.Clear();
            _previewObservedMarkerSignatures.Clear();
            _previewObservedSubGraphFrameCount = -1;
        }

        private void SyncPreviewGraphShapeSnapshot(Graph graph)
        {
            _previewObservedNodeIds.Clear();
            foreach (var node in graph.Nodes)
                _previewObservedNodeIds.Add(node.Id);

            _previewObservedSubGraphFrameCount = graph.SubGraphFrames.Count;
        }

        private void DetectPreviewGraphShapeChange()
        {
            if (_viewModel == null)
                return;

            var graph = _viewModel.Graph;

            if (_previewObservedSubGraphFrameCount < 0)
            {
                SyncPreviewGraphShapeSnapshot(graph);
                return;
            }

            bool subGraphChanged = graph.SubGraphFrames.Count != _previewObservedSubGraphFrameCount;
            int currentNodeCount = graph.Nodes.Count;
            if (!subGraphChanged && currentNodeCount == _previewObservedNodeIds.Count)
                return;

            var currentNodeIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
            var removedNodeIds = new List<string>();
            foreach (var oldNodeId in _previewObservedNodeIds)
            {
                if (!currentNodeIds.Contains(oldNodeId))
                    removedNodeIds.Add(oldNodeId);
            }

            var addedNodeIds = new List<string>();
            foreach (var currentNodeId in currentNodeIds)
            {
                if (!_previewObservedNodeIds.Contains(currentNodeId))
                    addedNodeIds.Add(currentNodeId);
            }

            bool changed = subGraphChanged || removedNodeIds.Count > 0 || addedNodeIds.Count > 0;

            if (!changed)
                return;

            SBLog.Debug(
                SBLogTags.Pipeline,
                "DetectPreviewGraphShapeChange: added={0}, removed={1}, subGraphCount {2}->{3}",
                addedNodeIds.Count,
                removedNodeIds.Count,
                _previewObservedSubGraphFrameCount,
                graph.SubGraphFrames.Count);

            if (addedNodeIds.Count > 0)
            {
                foreach (var addedNodeId in addedNodeIds)
                {
                    var addedNode = graph.FindNode(addedNodeId);
                    if (addedNode?.UserData is ActionNodeData addedNodeData)
                        UpdatePreviewMarkerNodeIndexForNode(addedNodeId, addedNodeData);
                }

                MarkPreviewDirtyForNodes(addedNodeIds, "GraphShapeChanged.NodeAdded");
            }

            if (removedNodeIds.Count > 0)
            {
                foreach (var removedNodeId in removedNodeIds)
                    RemovePreviewMarkerNodeIndexForNode(removedNodeId);

                MarkPreviewDirtyForNodes(removedNodeIds, "GraphShapeChanged.NodeRemoved");
            }

            SyncPreviewGraphShapeSnapshot(graph);
        }

        private void OnGUI()
        {
            InitializeIfNeeded();
            if (_viewModel == null || _input == null || _renderer == null
                || _editContext == null || _coordinateHelper == null
                || _inspectorPanel == null)
                return;

            var evt = Event.current;

            // ── 工具栏 ──
            DrawToolbar();

            // ── 计算分栏布局 ──
            float contentTop = ToolbarHeight;
            float contentHeight = position.height - contentTop;
            float splitterCount = _showWorkbench ? 2f : 1f;
            float workbenchWidth = 0f;
            if (_showWorkbench)
            {
                float maxWorkbenchWidth = Mathf.Min(
                    MaxWorkbenchWidth,
                    position.width - MinInspectorWidth - MinCanvasWidth - SplitterWidth * splitterCount);
                if (maxWorkbenchWidth < MinWorkbenchWidth)
                    maxWorkbenchWidth = MinWorkbenchWidth;
                workbenchWidth = Mathf.Clamp(_workbenchWidth, MinWorkbenchWidth, maxWorkbenchWidth);
                _workbenchWidth = workbenchWidth;
            }

            float maxInspectorWidth = Mathf.Min(
                MaxInspectorWidth,
                position.width - workbenchWidth - MinCanvasWidth - SplitterWidth * splitterCount);
            if (maxInspectorWidth < MinInspectorWidth)
                maxInspectorWidth = MinInspectorWidth;

            _inspectorWidth = Mathf.Clamp(_inspectorWidth, MinInspectorWidth, maxInspectorWidth);
            float canvasWidth = Mathf.Max(
                MinCanvasWidth,
                position.width - workbenchWidth - _inspectorWidth - SplitterWidth * splitterCount);

            var workbenchRect = new Rect(0, contentTop, workbenchWidth, contentHeight);
            var workbenchSplitterRect = new Rect(workbenchWidth, contentTop, SplitterWidth, contentHeight);
            float graphX = _showWorkbench ? workbenchRect.xMax + SplitterWidth : 0f;
            var graphRect = new Rect(graphX, contentTop, canvasWidth, contentHeight);
            var splitterRect = new Rect(graphRect.xMax, contentTop, SplitterWidth, contentHeight);
            var inspectorRect = new Rect(splitterRect.xMax, contentTop,
                _inspectorWidth, contentHeight);

            // ── 分栏拖拽 ──
            if (_showWorkbench)
                HandleWorkbenchSplitter(workbenchSplitterRect, evt);
            HandleSplitter(splitterRect, evt, workbenchWidth);

            // ── 左侧工作台 ──
            if (_showWorkbench)
                DrawWorkbenchPanel(workbenchRect);

            // ── 画布区域 ──
            _coordinateHelper.SetGraphAreaRect(graphRect);
            var viewport = new Rect2(0, 0, graphRect.width, graphRect.height);

            // 在 BeginClip 之前更新输入状态
            _input.Update(evt, _coordinateHelper);

            if (evt.type == EventType.Repaint)
            {
                if (_showWorkbench)
                {
                    var dividerRect = new Rect(workbenchRect.xMax - 1f, contentTop, 1f, contentHeight);
                    EditorGUI.DrawRect(dividerRect, new Color(0.13f, 0.13f, 0.13f, 1f));

                    // 工作台与画布之间的分栏条
                    EditorGUI.DrawRect(workbenchSplitterRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                }

                // 绘制分栏条
                EditorGUI.DrawRect(splitterRect, new Color(0.15f, 0.15f, 0.15f, 1f));

                // 画布渲染（BeginClip 裁剪）
                GUI.BeginClip(graphRect);
                {
                    var localRect = new Rect(0, 0, graphRect.width, graphRect.height);
                    _viewModel.Update(0.016f);
                    var frame = _viewModel.BuildFrame(viewport);
                    _renderer.Render(frame, _viewModel.Theme, localRect, _editContext,
                        Vector2.zero);
                }
                GUI.EndClip();
            }
            else if (evt.type == EventType.ContextClick && graphRect.Contains(evt.mousePosition))
            {
                evt.Use();
            }
            else if (IsInputEvent(evt) && graphRect.Contains(evt.mousePosition))
            {
                List<string>? deletingNodeIds = null;
                if (evt.type == EventType.KeyDown
                    && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
                {
                    deletingNodeIds = _viewModel.Selection.SelectedNodeIds.ToList();
                }

                // 输入事件：仅在画布区域内处理
                _viewModel.PreUpdateNodeSizes();
                _viewModel.ProcessInput(_input);

                // 仅在可能修改图数据的输入后标记为脏，避免纯导航/缩放导致关系与问题缓存失效
                if (evt.type == EventType.KeyDown || evt.type == EventType.MouseUp)

                // 键盘删除路径：按“即将删除的节点”精确标记预览脏数据。
                if (deletingNodeIds != null && deletingNodeIds.Count > 0)
                {
                    foreach (var deletingNodeId in deletingNodeIds)
                        RemovePreviewMarkerNodeIndexForNode(deletingNodeId);

                    MarkPreviewDirtyForNodes(deletingNodeIds, "Input.DeleteKey");
                }

                // 标记事件已消费，防止 Unity IMGUI 将事件继续传播到
                // Inspector 面板等其他控件，避免焦点抢夺和拖拽序列追踪失效
                evt.Use();
            }

            // ── Inspector 面板 ──
            _inspectorDrawer?.SetVariableDeclarations(BuildCombinedVariables());
            if (_inspectorPanel.Draw(inspectorRect, _viewModel))
            {
                // 属性被修改，刷新画布摘要显示
                _viewModel.RequestRepaint();
            }

            DetectPreviewGraphShapeChange();

            // 请求重绘
            if (_viewModel.NeedsRepaint)
                Repaint();
        }

        // ── 分栏拖拽 ──

        private void HandleWorkbenchSplitter(Rect splitterRect, Event evt)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition))
                    {
                        _isDraggingWorkbenchSplitter = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingWorkbenchSplitter)
                    {
                        float nextWidth = evt.mousePosition.x - SplitterWidth * 0.5f;
                        float maxWorkbenchWidth = Mathf.Min(
                            MaxWorkbenchWidth,
                            position.width - _inspectorWidth - MinCanvasWidth - SplitterWidth * 2f);
                        if (maxWorkbenchWidth < MinWorkbenchWidth)
                            maxWorkbenchWidth = MinWorkbenchWidth;

                        _workbenchWidth = Mathf.Clamp(nextWidth, MinWorkbenchWidth, maxWorkbenchWidth);
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingWorkbenchSplitter)
                    {
                        _isDraggingWorkbenchSplitter = false;
                        EditorPrefs.SetFloat(WorkbenchWidthPrefsKey, _workbenchWidth);
                        evt.Use();
                    }
                    break;
            }
        }

        private void HandleSplitter(Rect splitterRect, Event evt, float workbenchWidth)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition))
                    {
                        _isDraggingSplitter = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingSplitter)
                    {
                        float nextWidth = position.width - evt.mousePosition.x - SplitterWidth * 0.5f;
                        float splitterCount = _showWorkbench ? 2f : 1f;
                        float maxInspectorWidth = Mathf.Min(
                            MaxInspectorWidth,
                            position.width - workbenchWidth - MinCanvasWidth - SplitterWidth * splitterCount);
                        if (maxInspectorWidth < MinInspectorWidth)
                            maxInspectorWidth = MinInspectorWidth;

                        _inspectorWidth = Mathf.Clamp(nextWidth, MinInspectorWidth, maxInspectorWidth);
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingSplitter)
                    {
                        _isDraggingSplitter = false;
                        evt.Use();
                    }
                    break;
            }
        }

        // ── 工具栏 ──

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                NewGraph();
            }

            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SaveBlueprint();
            }

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                LoadBlueprint();
            }

            GUILayout.Space(6);

            bool wantVariables = GUILayout.Toggle(
                _showWorkbench,
                new GUIContent("变量", "显示/隐藏 Blackboard 变量面板"),
                EditorStyles.toolbarButton,
                GUILayout.Width(40));
            if (wantVariables != _showWorkbench)
            {
                _showWorkbench = wantVariables;
                EditorPrefs.SetBool(WorkbenchVisiblePrefsKey, _showWorkbench);
                Repaint();
            }

            GUILayout.Space(6);

            if (GUILayout.Button("+ 子蓝图", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                AddSubBlueprint();
            }

            if (GUILayout.Button("全部折叠", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                CollapseAllSubGraphs(true);
            }

            if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                CollapseAllSubGraphs(false);
            }

            GUILayout.FlexibleSpace();

            // 状态信息
            if (_viewModel != null)
            {
                int subGraphCount = _viewModel.Graph.SubGraphFrames.Count;
                int nodeCount = _viewModel.Graph.Nodes.Count;
                int edgeCount = _viewModel.Graph.Edges.Count;
                string bindingInfo = _bindingContext != null
                    ? $"绑定: {_bindingContext.BoundCount}/{_bindingContext.Count}"
                    : "";

                string statusText = subGraphCount > 0
                    ? $"子蓝图: {subGraphCount}  节点: {nodeCount}  连线: {edgeCount}  {bindingInfo}"
                    : $"节点: {nodeCount}  连线: {edgeCount}  {bindingInfo}";

                GUILayout.Label(statusText, EditorStyles.miniLabel);

            }

            GUILayout.Space(6);

            if (GUILayout.Button("同步到场景", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SyncToScene();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                ExportBlueprint();
            }

            GUILayout.Space(6);

            GUILayout.EndHorizontal();
        }

        // ── 工作台面板（Blackboard 变量）──

        private void DrawWorkbenchPanel(Rect panelRect)
        {
            GUILayout.BeginArea(panelRect);
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                {
                    DrawBlackboardPanel();
                }
                EditorGUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void OnEditorHierarchyChanged()
        {
            MarkPreviewDirtyForHierarchyChange();
        }

        private void OnEditorProjectChanged()
        {
            _actionRegistryCache = null;
        }


        // ── Blackboard 变量面板 ──

        private static readonly string[] _varTypeOptions  = { "Int", "Float", "Bool", "String" };
        private static readonly string[] _varScopeOptions = { "Local", "Global" };

        private void DrawBlackboardPanel()
        {
            if (_currentAsset == null)
            {
                EditorGUILayout.HelpBox("请先保存蓝图资产（BlueprintAsset）以使用变量面板。", MessageType.Info);
                return;
            }

            var vars = _currentAsset.Variables ?? new VariableDeclaration[0];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUILayout.LabelField($"变量声明 ({vars.Length})", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 添加", EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    Undo.RecordObject(_currentAsset, "Add Blackboard Variable");
                    var list = new List<VariableDeclaration>(vars);
                    int nextIndex = list.Count > 0 ? list.Max(v => v.Index) + 1 : 0;
                    list.Add(new VariableDeclaration
                    {
                        Index        = nextIndex,
                        Name         = $"var_{nextIndex}",
                        Type         = "Int",
                        Scope        = "Local",
                        InitialValue = "0"
                    });
                    _currentAsset.Variables = list.ToArray();
                    EditorUtility.SetDirty(_currentAsset);
                }
            }
            EditorGUILayout.EndHorizontal();

            _blackboardScrollPos = EditorGUILayout.BeginScrollView(_blackboardScrollPos);
            {
                if (vars.Length == 0)
                {
                    EditorGUILayout.HelpBox("暂无变量。点击\"添加\"声明第一个变量。", MessageType.None);
                }
                else
                {
                    int toRemove = -1;
                    for (int i = 0; i < vars.Length; i++)
                    {
                        if (DrawVariableEntry(vars[i]))
                            toRemove = i;
                    }
                    if (toRemove >= 0)
                    {
                        Undo.RecordObject(_currentAsset, "Remove Blackboard Variable");
                        var list = new List<VariableDeclaration>(_currentAsset.Variables);
                        list.RemoveAt(toRemove);
                        _currentAsset.Variables = list.ToArray();
                        EditorUtility.SetDirty(_currentAsset);
                        GUIUtility.ExitGUI();
                    }
                }

                // ── 节点产出变量（只读）──
                var nodeOutVars = CollectNodeOutputVariables();
                if (nodeOutVars.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("节点产出变量（只读）", EditorStyles.miniLabel);
                    foreach (var nov in nodeOutVars)
                        DrawReadonlyVariableEntry(nov);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private bool DrawVariableEntry(VariableDeclaration decl)
        {
            bool removed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // ── 第一行: [Index] + 名称 + [×] ──
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.color = new Color(0.7f, 0.9f, 1f);
                    GUILayout.Label($"[{decl.Index}]", EditorStyles.miniLabel, GUILayout.Width(24));
                    GUI.color = Color.white;

                    EditorGUILayout.LabelField("名称", GUILayout.Width(28));
                    string newName = EditorGUILayout.TextField(decl.Name, GUILayout.MinWidth(60));
                    if (newName != decl.Name)
                    {
                        Undo.RecordObject(_currentAsset!, "Rename Blackboard Variable");
                        decl.Name = newName;
                        EditorUtility.SetDirty(_currentAsset!);
                    }

                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                        removed = true;
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                // ── 第二行: 类型 + 作用域 + 初始值 ──
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("类型", GUILayout.Width(28));
                    int typeIdx = System.Array.IndexOf(_varTypeOptions, decl.Type);
                    int newTypeIdx = EditorGUILayout.Popup(
                        typeIdx < 0 ? 0 : typeIdx,
                        _varTypeOptions,
                        GUILayout.Width(60));
                    if (_varTypeOptions[newTypeIdx] != decl.Type)
                    {
                        Undo.RecordObject(_currentAsset!, "Edit Variable Type");
                        decl.Type = _varTypeOptions[newTypeIdx];
                        EditorUtility.SetDirty(_currentAsset!);
                    }

                    EditorGUILayout.LabelField("作用域", GUILayout.Width(36));
                    int scopeIdx = decl.Scope == "Global" ? 1 : 0;
                    int newScopeIdx = EditorGUILayout.Popup(scopeIdx, _varScopeOptions, GUILayout.Width(54));
                    if (newScopeIdx != scopeIdx)
                    {
                        Undo.RecordObject(_currentAsset!, "Edit Variable Scope");
                        decl.Scope = _varScopeOptions[newScopeIdx];
                        EditorUtility.SetDirty(_currentAsset!);
                    }

                    EditorGUILayout.LabelField("初始值", GUILayout.Width(36));
                    string newInit = EditorGUILayout.TextField(decl.InitialValue, GUILayout.MinWidth(40));
                    if (newInit != decl.InitialValue)
                    {
                        Undo.RecordObject(_currentAsset!, "Edit Variable InitialValue");
                        decl.InitialValue = newInit;
                        EditorUtility.SetDirty(_currentAsset!);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            return removed;
        }

        /// <summary>合并用户声明变量 + 节点产出变量，作为下拉列表的数据源。</summary>
        private VariableDeclaration[] BuildCombinedVariables()
        {
            var userVars = _currentAsset?.Variables ?? System.Array.Empty<VariableDeclaration>();
            var nodeVars = CollectNodeOutputVariables();
            if (nodeVars.Count == 0) return userVars;
            var combined = new List<VariableDeclaration>(userVars);
            combined.AddRange(nodeVars);
            return combined.ToArray();
        }

        /// <summary>
        /// 扫描图中所有节点的 OutputVariables，按名称去重，分配稳定的合成 Index。
        /// 合成 Index 范围：10000–19999（避免与用户声明的 0–9999 冲突）。
        /// </summary>
        private List<VariableDeclaration> CollectNodeOutputVariables()
        {
            var result = new List<VariableDeclaration>();
            if (_viewModel == null) return result;

            var registry = GetActionRegistry();
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var def)) continue;

                foreach (var outVar in def.OutputVariables)
                {
                    if (seen.Contains(outVar.Name)) continue;
                    seen.Add(outVar.Name);
                    result.Add(new VariableDeclaration
                    {
                        Index        = NodeOutputVarIndex(outVar.Name),
                        Name         = outVar.Name,
                        Type         = outVar.Type,
                        Scope        = outVar.Scope,
                        InitialValue = ""
                    });
                }
            }
            return result;
        }

        /// <summary>DJB2 hash of name → 10000–19999（稳定、无 Unity 依赖）。</summary>
        private static int NodeOutputVarIndex(string name)
        {
            uint h = 5381;
            foreach (char c in name) h = ((h << 5) + h) + c;
            return 10000 + (int)(h % 10000);
        }

        /// <summary>以灰色只读样式绘制一条节点产出变量条目。</summary>
        private static void DrawReadonlyVariableEntry(VariableDeclaration decl)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.color = new Color(0.6f, 0.8f, 1f);
                    GUILayout.Label($"[{decl.Index}]", EditorStyles.miniLabel, GUILayout.Width(44));
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    GUILayout.Label(decl.Name, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    GUI.color = new Color(0.7f, 0.9f, 0.7f);
                    GUILayout.Label($"{decl.Type}  {decl.Scope}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUI.color = prevColor;
        }

        private Core.ActionRegistry GetActionRegistry()
        {
            _actionRegistryCache ??= SceneBlueprintProfile.CreateActionRegistry();
            return _actionRegistryCache;
        }


        private bool HasSyncedSceneBindings()
        {
            if (_currentAsset == null)
                return false;

            return _sceneBindingStore.IsBoundToBlueprint(_currentAsset);
        }

        // ── 操作方法 ──

        private void NewGraph()
        {
            if (_viewModel == null || _profile == null) return;

            bool confirm = _viewModel.Graph.Nodes.Count == 0 ||
                EditorUtility.DisplayDialog("新建蓝图", "当前蓝图未保存，确定要新建吗？", "确定", "取消");

            if (confirm)
            {
                _currentAsset = null;
                _viewModel = null;
                _bindingContext?.Clear();
                InitializeIfNeeded();
                AddDefaultNodes();
                Repaint();
            }
        }

        /// <summary>新建蓝图时自动添加 Flow.Start 和 Flow.End 节点</summary>
        private void AddDefaultNodes()
        {
            if (_viewModel == null) return;

            var graph = _viewModel.Graph;
            var commands = _viewModel.Commands;

            // 添加 Start 节点
            var startPos = new Vec2(100, 100);
            commands.Execute(new AddNodeCommand("Flow.Start", startPos));

            // 添加 End 节点
            var endPos = new Vec2(400, 100);
            commands.Execute(new AddNodeCommand("Flow.End", endPos));

            _viewModel.RequestRepaint();
        }

        private void SaveBlueprint()
        {
            if (_viewModel == null) return;

            var serializer = CreateGraphSerializer();
            string graphJson = serializer.Serialize(_viewModel.Graph);

            if (_currentAsset != null)
            {
                // 覆盖保存到已有资产
                _currentAsset.GraphJson = graphJson;
                _currentAsset.Version++;
                EditorUtility.SetDirty(_currentAsset);
                AssetDatabase.SaveAssets();
                SBLog.Info(SBLogTags.Blueprint, $"已保存: {AssetDatabase.GetAssetPath(_currentAsset)}");
                UpdateTitle();
            }
            else
            {
                // 另存为新资产
                string path = EditorUtility.SaveFilePanelInProject(
                    "保存蓝图资产",
                    "NewBlueprint",
                    "asset",
                    "选择蓝图保存位置");

                if (!string.IsNullOrEmpty(path))
                {
                    var asset = ScriptableObject.CreateInstance<BlueprintAsset>();
                    asset.InitializeNew(System.IO.Path.GetFileNameWithoutExtension(path));
                    asset.GraphJson = graphJson;

                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();

                    _currentAsset = asset;
                    UpdateTitle();
                    SBLog.Info(SBLogTags.Blueprint, $"已创建: {path} (ID: {asset.BlueprintId})");
                }
            }
        }

        private void LoadBlueprint()
        {
            if (_viewModel == null) return;

            bool confirm = _viewModel.Graph.Nodes.Count == 0 ||
                EditorUtility.DisplayDialog("加载蓝图", "当前蓝图未保存，确定要加载吗？", "确定", "取消");

            if (!confirm) return;

            string path = EditorUtility.OpenFilePanel("加载蓝图资产", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            // 转换为相对路径
            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var asset = AssetDatabase.LoadAssetAtPath<BlueprintAsset>(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("加载失败", "选择的文件不是有效的蓝图资产。", "确定");
                return;
            }

            if (asset.IsEmpty)
            {
                EditorUtility.DisplayDialog("加载失败", "蓝图资产中没有图数据。", "确定");
                return;
            }

            try
            {
                var serializer = CreateGraphSerializer();
                var graph = serializer.Deserialize(asset.GraphJson);

                _currentAsset = asset;
                _viewModel = null;
                InitializeWithGraph(graph);
                RestoreBindingsFromScene();
                CenterView();
                Repaint();

                SBLog.Info(SBLogTags.Blueprint, $"已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();

                // 刷新预览
                MarkPreviewDirtyAll("LoadBlueprint");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");
                SBLog.Error(SBLogTags.Blueprint, $"加载失败: {ex}");
            }
        }

        /// <summary>
        /// 从外部直接加载指定的 BlueprintAsset（供自定义 Inspector 调用）。
        /// </summary>
        public void LoadFromAsset(BlueprintAsset asset)
        {
            if (asset == null || asset.IsEmpty) return;

            try
            {
                var serializer = CreateGraphSerializer();
                var graph = serializer.Deserialize(asset.GraphJson);

                _currentAsset = asset;
                _viewModel = null;
                InitializeWithGraph(graph);
                RestoreBindingsFromScene();
                CenterView();
                Repaint();

                SBLog.Info(SBLogTags.Blueprint, $"已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();

                // 刷新预览
                MarkPreviewDirtyAll("LoadFromAsset");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");
                SBLog.Error(SBLogTags.Blueprint, $"加载失败: {ex}");
            }
        }

        /// <summary>加载蓝图后运行标记绑定一致性验证</summary>
        private void RunBindingValidation()
        {
            if (_viewModel == null) return;

            var registry = GetActionRegistry();
            var report = MarkerBindingValidator.Validate(_viewModel.Graph, registry);
            MarkerBindingValidator.LogReport(report);
        }

        private void CenterView()
        {
            if (_viewModel == null) return;

            if (_viewModel.Graph.Nodes.Count == 0)
            {
                _viewModel.PanOffset = new Vec2(position.width / 2, position.height / 2);
                _viewModel.ZoomLevel = 1f;
            }
            else
            {
                // 计算所有节点的包围盒中心
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                foreach (var node in _viewModel.Graph.Nodes)
                {
                    if (node.Position.X < minX) minX = node.Position.X;
                    if (node.Position.Y < minY) minY = node.Position.Y;
                    if (node.Position.X + node.Size.X > maxX) maxX = node.Position.X + node.Size.X;
                    if (node.Position.Y + node.Size.Y > maxY) maxY = node.Position.Y + node.Size.Y;
                }

                float centerX = (minX + maxX) / 2f;
                float centerY = (minY + maxY) / 2f;
                _viewModel.PanOffset = new Vec2(
                    position.width / 2f - centerX * _viewModel.ZoomLevel,
                    position.height / 2f - centerY * _viewModel.ZoomLevel);
            }
            Repaint();
        }

        private void ExportBlueprint()
        {
            if (_viewModel == null) return;

            var registry = GetActionRegistry();

            // 从场景绑定存储收集绑定数据
            var sceneBindings = CollectSceneBindingsForExport();

            string bpName = _currentAsset != null ? _currentAsset.BlueprintName : "场景蓝图";
            string? bpId = _currentAsset != null ? _currentAsset.BlueprintId : null;

            var exportOptions = new BlueprintExporter.ExportOptions
            {
                AdapterType = GetCurrentAdapterType()
            };

            var result = BlueprintExporter.Export(
                _viewModel.Graph, registry, sceneBindings,
                blueprintId: bpId, blueprintName: bpName,
                options: exportOptions,
                variables: _currentAsset?.Variables);

            // 输出验证消息
            foreach (var msg in result.Messages)
            {
                switch (msg.Level)
                {
                    case ValidationLevel.Error:
                        SBLog.Error(SBLogTags.Export, msg.Message);
                        break;
                    case ValidationLevel.Warning:
                        SBLog.Warn(SBLogTags.Export, msg.Message);
                        break;
                    default:
                        SBLog.Info(SBLogTags.Export, msg.Message);
                        break;
                }
            }

            if (result.HasErrors)
            {
                EditorUtility.DisplayDialog("导出失败",
                    $"蓝图存在 {result.Messages.Count(m => m.Level == ValidationLevel.Error)} 个错误，请查看 Console 日志。",
                    "确定");
                return;
            }

            // 序列化为 JSON
            var json = BlueprintSerializer.ToJson(result.Data);

            // 选择保存路径
            string defaultName = string.IsNullOrEmpty(result.Data.BlueprintName)
                ? "blueprint" : result.Data.BlueprintName;
            string path = EditorUtility.SaveFilePanel(
                "导出蓝图 JSON",
                "Assets",
                defaultName,
                "json");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json, System.Text.Encoding.UTF8);

                // 统计绑定信息
                int totalBindings = 0;
                int boundBindings = 0;
                foreach (var a in result.Data.Actions)
                {
                    foreach (var sb in a.SceneBindings)
                    {
                        totalBindings++;
                        if (!string.IsNullOrEmpty(sb.StableObjectId)) boundBindings++;
                    }
                }

                SBLog.Info(SBLogTags.Export, $"蓝图已导出到: {path} " +
                    $"(行动数: {result.Data.Actions.Length}, " +
                    $"过渡数: {result.Data.Transitions.Length}, " +
                    $"绑定数: {boundBindings}/{totalBindings})");

                if (result.HasWarnings)
                {
                    EditorUtility.DisplayDialog("导出完成（有警告）",
                        $"蓝图已导出，但有 {result.Messages.Count(m => m.Level == ValidationLevel.Warning)} 条警告，请查看 Console。",
                        "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("导出成功",
                        $"蓝图已导出到:\n{path}\n\n行动数: {result.Data.Actions.Length}\n过渡数: {result.Data.Transitions.Length}",
                        "确定");
                }
            }
        }

        // ── 子蓝图操作 ──

        private void AddSubBlueprint()
        {
            if (_viewModel == null) return;

            // 弹出输入框让策划命名
            string title = EditorInputDialog.Show("新建子蓝图", "请输入子蓝图名称：", "新子蓝图");
            if (string.IsNullOrEmpty(title)) return;

            // 创建一个空的源图（子蓝图内容为空，策划后续在内部添加节点）
            var emptySource = new Graph(new GraphSettings());

            // 在画布中心插入
            var canvasCenter = new Vec2(
                (_viewModel.PanOffset.X * -1f + position.width / 2f) / _viewModel.ZoomLevel,
                (_viewModel.PanOffset.Y * -1f + position.height / 2f) / _viewModel.ZoomLevel);

            // 定义默认边界端口：一个输入（激活）、一个输出（完成）
            var boundaryPorts = new GraphPortDefinition[]
            {
                new GraphPortDefinition("激活", PortDirection.Input, PortKind.Control, "exec", PortCapacity.Single, 0),
                new GraphPortDefinition("完成", PortDirection.Output, PortKind.Control, "exec", PortCapacity.Single, 0),
            };

            _viewModel.Commands.Execute(
                new CreateSubGraphCommand(emptySource, title, canvasCenter, boundaryPorts));

            _viewModel.RequestRepaint();
            Repaint();

            SBLog.Info(SBLogTags.Blueprint, $"已创建子蓝图: {title}");
        }

        /// <summary>
        /// 从场景绑定存储恢复绑定数据到 BindingContext。
        /// 在加载蓝图后调用。
        /// </summary>
        private void RestoreBindingsFromScene()
        {
            if (_bindingContext == null || _currentAsset == null || _viewModel == null) return;

            _bindingContext.Clear();

            // 策略 1：从场景绑定存储恢复（正式流程）
            if (_sceneBindingStore.TryLoadBindingGroups(_currentAsset, out var bindingGroups))
            {
                foreach (var group in bindingGroups)
                {
                    foreach (var binding in group.Bindings)
                    {
                        if (!string.IsNullOrEmpty(binding.BindingKey) && binding.BoundObject != null)
                        {
                            _bindingContext.Set(binding.BindingKey, binding.BoundObject);
                        }
                    }
                }
            }

            // 策略 2：对于未恢复的绑定，用 PropertyBag 中的 MarkerId 回退查找
            var registry = GetActionRegistry();
            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (_bindingContext.Get(scopedBindingKey) != null) continue; // 已恢复，跳过

                    var storedId = data.Properties.Get<string>(prop.Key);
                    if (string.IsNullOrEmpty(storedId)) continue;

                    // 通过 MarkerId 在场景中查找对应的 SceneMarker
                    var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
                    if (marker != null)
                    {
                        _bindingContext.Set(scopedBindingKey, marker.gameObject);
                    }
                }
            }

            int restored = _bindingContext.BoundCount;
            if (restored > 0)
            {
                SBLog.Info(SBLogTags.Binding, $"已从场景恢复 {restored} 个绑定");
            }

        }

        private void CollapseAllSubGraphs(bool collapse)
        {
            if (_viewModel == null) return;

            foreach (var sgf in _viewModel.Graph.SubGraphFrames)
            {
                if (sgf.IsCollapsed != collapse)
                {
                    _viewModel.Commands.Execute(
                        new ToggleSubGraphCollapseCommand(sgf.Id));
                }
            }

            _viewModel.RequestRepaint();
            Repaint();
        }

        // ── 同步到场景 ──

        private void SyncToScene()
        {
            if (_viewModel == null || _bindingContext == null) return;

            var currentAsset = _currentAsset;

            // 1. 先保存蓝图（确保 SO 数据是最新的）
            if (currentAsset == null)
            {
                EditorUtility.DisplayDialog("同步失败", "请先保存蓝图资产后再同步到场景。", "确定");
                return;
            }

            SaveBlueprint();
            currentAsset = _currentAsset;
            if (currentAsset == null)
            {
                EditorUtility.DisplayDialog("同步失败", "蓝图资产保存失败，请重试。", "确定");
                return;
            }

            // 2. 按子蓝图分组构建绑定数据
            var graph = _viewModel.Graph;
            var registry = GetActionRegistry();
            var bindingGroups = new List<SubGraphBindingGroup>();

            foreach (var sgf in graph.SubGraphFrames)
            {
                var group = new SubGraphBindingGroup
                {
                    SubGraphFrameId = sgf.Id,
                    SubGraphTitle = sgf.Title
                };

                // 收集该子蓝图内的所有 SceneBinding 属性
                var seenKeys = new HashSet<string>();
                foreach (var nodeId in sgf.ContainedNodeIds)
                {
                    var node = graph.FindNode(nodeId);
                    if (node?.UserData is not Core.ActionNodeData actionData) continue;
                    if (!registry.TryGet(actionData.ActionTypeId, out var actionDef)) continue;

                    foreach (var prop in actionDef.Properties)
                    {
                        if (prop.Type != Core.PropertyType.SceneBinding) continue;
                        string scopedBindingKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                        if (seenKeys.Contains(scopedBindingKey)) continue;
                        seenKeys.Add(scopedBindingKey);

                        var slot = new SceneBindingSlot
                        {
                            BindingKey = scopedBindingKey,
                            BindingType = prop.SceneBindingType ?? Core.BindingType.Transform,
                            DisplayName = prop.DisplayName,
                            SourceActionTypeId = actionData.ActionTypeId,
                            BoundObject = _bindingContext.Get(scopedBindingKey)
                        };
                        group.Bindings.Add(slot);
                    }
                }

                if (group.Bindings.Count > 0)
                    bindingGroups.Add(group);
            }

            // 3. 收集顶层（非子蓝图内）节点的绑定
            var topLevelGroup = CollectTopLevelBindings(graph, registry);
            if (topLevelGroup != null && topLevelGroup.Bindings.Count > 0)
                bindingGroups.Add(topLevelGroup);

            // 4. 持久化绑定分组
            _sceneBindingStore.SaveBindingGroups(currentAsset, bindingGroups);

            int totalBindings = 0;
            int boundBindings = 0;
            foreach (var g in bindingGroups)
            {
                foreach (var b in g.Bindings)
                {
                    totalBindings++;
                    if (b.IsBound) boundBindings++;
                }
            }

            SBLog.Info(SBLogTags.Binding, $"已同步到场景: " +
                $"子蓝图分组: {bindingGroups.Count}, " +
                $"绑定: {boundBindings}/{totalBindings}");

        }

        /// <summary>从场景绑定存储或 BindingContext 收集绑定数据供导出使用</summary>
        private List<BlueprintExporter.SceneBindingData>? CollectSceneBindingsForExport()
        {
            var registry = GetActionRegistry();
            var currentAsset = _currentAsset;

            // 优先从场景绑定存储读取（持久化数据）
            if (currentAsset != null
                && _sceneBindingStore.TryLoadBindingGroups(currentAsset, out var bindingGroups)
                && bindingGroups.Count > 0)
            {
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var group in bindingGroups)
                {
                    foreach (var binding in group.Bindings)
                    {
                        EncodeBindingForExport(
                            binding.BoundObject,
                            binding.BindingType,
                            out var stableObjectId,
                            out var adapterType,
                            out var spatialPayloadJson);

                        list.Add(new BlueprintExporter.SceneBindingData
                        {
                            BindingKey = binding.BindingKey,
                            BindingType = binding.BindingType.ToString(),
                            StableObjectId = stableObjectId,
                            AdapterType = adapterType,
                            SpatialPayloadJson = spatialPayloadJson,
                            SourceSubGraph = group.SubGraphTitle,
                            SourceActionTypeId = binding.SourceActionTypeId
                        });
                    }
                }
                return list.Count > 0 ? list : null;
            }

            // 降级：从 BindingContext 读取（编辑器内存数据）
            if (_bindingContext != null && _bindingContext.Count > 0)
            {
                var bindingTypeMap = BuildBindingTypeMapFromGraph(registry);
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var kvp in _bindingContext.All)
                {
                    string resolvedBindingKey = ResolveBindingKeyForExport(kvp.Key, bindingTypeMap);
                    var bindingType = bindingTypeMap.TryGetValue(resolvedBindingKey, out var resolvedType)
                        ? resolvedType
                        : Core.BindingType.Transform;

                    EncodeBindingForExport(
                        kvp.Value,
                        bindingType,
                        out var stableObjectId,
                        out var adapterType,
                        out var spatialPayloadJson);

                    list.Add(new BlueprintExporter.SceneBindingData
                    {
                        BindingKey = resolvedBindingKey,
                        StableObjectId = stableObjectId,
                        AdapterType = adapterType,
                        SpatialPayloadJson = spatialPayloadJson
                    });
                }
                return list.Count > 0 ? list : null;
            }

            return null;
        }

        private Dictionary<string, Core.BindingType> BuildBindingTypeMapFromGraph(Core.ActionRegistry registry)
        {
            var map = new Dictionary<string, Core.BindingType>();
            if (_viewModel == null) return map;

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData actionData) continue;
                if (!registry.TryGet(actionData.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.Type != Core.PropertyType.SceneBinding) continue;
                    if (string.IsNullOrEmpty(prop.Key)) continue;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    map[scopedBindingKey] = prop.SceneBindingType ?? Core.BindingType.Transform;
                }
            }

            return map;
        }

        private static string ResolveBindingKeyForExport(
            string contextBindingKey,
            Dictionary<string, Core.BindingType> bindingTypeMap)
        {
            if (string.IsNullOrEmpty(contextBindingKey))
                return contextBindingKey;

            if (BindingScopeUtility.IsScopedKey(contextBindingKey))
                return contextBindingKey;

            string? matchedScopedKey = null;
            foreach (var scopedKey in bindingTypeMap.Keys)
            {
                if (BindingScopeUtility.ExtractRawBindingKey(scopedKey) != contextBindingKey)
                    continue;

                if (matchedScopedKey != null)
                {
                    // 同名 raw key 出现多个作用域时，不做猜测，保持原值并由上游提示修复。
                    return contextBindingKey;
                }

                matchedScopedKey = scopedKey;
            }

            return matchedScopedKey ?? contextBindingKey;
        }

        private string GetCurrentAdapterType()
        {
            return EnsureSpatialModeDescriptor().AdapterType;
        }

        private void EncodeBindingForExport(
            GameObject? sceneObject,
            Core.BindingType bindingType,
            out string stableObjectId,
            out string adapterType,
            out string spatialPayloadJson)
        {
            var descriptor = EnsureSpatialModeDescriptor();

            if (sceneObject == null)
            {
                stableObjectId = "";
                adapterType = descriptor.AdapterType;
                spatialPayloadJson = "{}";
                return;
            }

            var payload = descriptor.BindingCodec.Encode(sceneObject, bindingType);
            stableObjectId = payload.StableObjectId;
            adapterType = string.IsNullOrEmpty(payload.AdapterType)
                ? descriptor.AdapterType
                : payload.AdapterType;
            spatialPayloadJson = string.IsNullOrEmpty(payload.SerializedSpatialData)
                ? "{}"
                : payload.SerializedSpatialData;
        }

        private IEditorSpatialModeDescriptor EnsureSpatialModeDescriptor()
        {
            _spatialModeDescriptor ??= SpatialModeRegistry.GetProjectModeDescriptor();
            return _spatialModeDescriptor;
        }

        /// <summary>收集顶层（不在任何子蓝图内）节点的 SceneBinding</summary>
        private SubGraphBindingGroup? CollectTopLevelBindings(Graph graph, Core.ActionRegistry registry)
        {
            // 建立所有子蓝图内节点 ID 集合
            var containedNodeIds = new HashSet<string>();
            foreach (var sgf in graph.SubGraphFrames)
            {
                foreach (var nid in sgf.ContainedNodeIds)
                    containedNodeIds.Add(nid);
            }

            var group = new SubGraphBindingGroup
            {
                SubGraphFrameId = "__toplevel__",
                SubGraphTitle = "顶层节点"
            };

            var seenKeys = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (containedNodeIds.Contains(node.Id)) continue;
                if (node.UserData is not Core.ActionNodeData actionData) continue;
                if (!registry.TryGet(actionData.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.Type != Core.PropertyType.SceneBinding) continue;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (seenKeys.Contains(scopedBindingKey)) continue;
                    seenKeys.Add(scopedBindingKey);

                    var slot = new SceneBindingSlot
                    {
                        BindingKey = scopedBindingKey,
                        BindingType = prop.SceneBindingType ?? Core.BindingType.Transform,
                        DisplayName = prop.DisplayName,
                        SourceActionTypeId = actionData.ActionTypeId,
                        BoundObject = _bindingContext?.Get(scopedBindingKey)
                    };
                    group.Bindings.Add(slot);
                }
            }

            return group.Bindings.Count > 0 ? group : null;
        }

        // ── 上下文菜单回调（由框架层 ContextMenuHandler 触发）──

        /// <summary>
        /// 右键点击画布空白区域的回调。
        /// 使用 Unity GenericMenu 按分类显示所有已注册的 Action 节点类型。
        /// </summary>
        /// <param name="canvasPos">点击位置的画布坐标（节点将创建在此处）</param>
        private void OnCanvasContextMenu(Vec2 canvasPos)
        {
            if (_viewModel == null || _profile == null) return;

            var menu = new GenericMenu();

            // 按分类分组显示所有节点类型（按 CategorySO.SortOrder 排序，无 SO 时按字母排序）
            var grouped = _profile.NodeTypes.GetAll()
                .GroupBy(def => string.IsNullOrEmpty(def.Category) ? "未分类" : def.Category)
                .OrderBy(g => CategoryRegistry.GetSortOrder(g.Key))
                .ThenBy(g => g.Key);

            foreach (var group in grouped)
            {
                // 获取分类的中文显示名（优先 CategorySO.DisplayName，无 SO 时回退到 categoryId）
                string categoryDisplayName = CategoryRegistry.GetDisplayName(group.Key);
                
                foreach (var typeDef in group.OrderBy(d => d.DisplayName))
                {
                    string menuPath = $"{categoryDisplayName}/{typeDef.DisplayName}";
                    var capturedTypeId = typeDef.TypeId;
                    var capturedPos = canvasPos;

                    menu.AddItem(new GUIContent(menuPath), false, () =>
                    {
                        if (_viewModel == null) return;
                        var addNodeCmd = new AddNodeCommand(capturedTypeId, capturedPos);
                        _viewModel.Commands.Execute(addNodeCmd);
                        if (!string.IsNullOrEmpty(addNodeCmd.CreatedNodeId))
                            MarkPreviewDirtyForNode(addNodeCmd.CreatedNodeId, "CanvasContext.AddNode");
                        _viewModel.RequestRepaint();
                        Repaint();
                    });
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("(无可用节点类型) (No Available Node Types)"));
            }

            // 从模板创建子蓝图
            var templatesGrouped = Templates.BlueprintTemplateUtils.FindAllTemplatesGrouped();
            if (templatesGrouped.Count > 0)
            {
                menu.AddSeparator("");
                foreach (var kvp in templatesGrouped.OrderBy(k => k.Key))
                {
                    foreach (var tmpl in kvp.Value.OrderBy(t => t.DisplayName))
                    {
                        var capturedTemplate = tmpl;
                        var capturedPos = canvasPos;
                        string displayName = string.IsNullOrEmpty(tmpl.DisplayName) ? tmpl.name : tmpl.DisplayName;
                        string menuPath = $"从模板创建 (From Template)/{kvp.Key}/{displayName}";
                        menu.AddItem(new GUIContent(menuPath), false, () =>
                        {
                            if (_viewModel == null) return;
                            var result = Templates.BlueprintTemplateUtils.InstantiateTemplate(
                                _viewModel.Graph, capturedTemplate,
                                CreateGraphSerializer(), capturedPos);
                            if (result != null)
                            {
                                MarkPreviewDirtyForNodes(result.NodeIdMap.Values, "CanvasContext.InstantiateTemplate");
                                _viewModel.RequestRepaint();
                                Repaint();
                            }
                        });
                    }
                }
            }

            // 如果有多个节点被选中，添加"创建子蓝图"选项
            if (_viewModel.Selection.SelectedNodeIds.Count >= 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图 (Create SubGraph)"), false, () =>
                {
                    if (_viewModel == null) return;
                    var selectedIds = _viewModel.Selection.SelectedNodeIds.ToList();
                    if (selectedIds.Count == 0) return;

                    var name = EditorInputDialog.Show("创建子蓝图", "请输入子蓝图名称：", "新子蓝图");
                    if (string.IsNullOrWhiteSpace(name)) return;
                    _viewModel.Commands.Execute(new GroupNodesCommand(name, selectedIds));
                    _viewModel.Selection.ClearSelection();
                    _viewModel.RequestRepaint();
                    Repaint();
                    SBLog.Info(SBLogTags.Blueprint, $"已创建子蓝图: {name}（包含 {selectedIds.Count} 个节点）");
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// 右键点击节点的回调。显示节点操作菜单（删除、复制等）。
        /// </summary>
        private void OnNodeContextMenu(Node node, Vec2 canvasPos)
        {
            if (_viewModel == null) return;

            var menu = new GenericMenu();
            var capturedNodeId = node.Id;

            // 检查是否是 SubGraphFrame 的 RepresentativeNode
            var frame = _viewModel.Graph.FindContainerSubGraphFrame(capturedNodeId);
            bool isRepNode = frame != null && frame.RepresentativeNodeId == capturedNodeId;

            if (isRepNode && frame != null)
            {
                var capturedFrameId = frame.Id;
                var isCollapsed = frame.IsCollapsed;

                // 折叠/展开切换
                menu.AddItem(new GUIContent(isCollapsed ? "展开子蓝图 (Expand)" : "折叠子蓝图 (Collapse)"), false, () =>
                {
                    if (_viewModel == null) return;
                    _viewModel.Commands.Execute(new ToggleSubGraphCollapseCommand(capturedFrameId));
                    _viewModel.RequestRepaint();
                    Repaint();
                });

                menu.AddItem(new GUIContent("解散子蓝图 (Ungroup)"), false, () =>
                {
                    if (_viewModel == null) return;
                    _viewModel.Commands.Execute(new UngroupSubGraphCommand(capturedFrameId));
                    _viewModel.Selection.ClearSelection();
                    _viewModel.RequestRepaint();
                    Repaint();
                });

                menu.AddItem(new GUIContent("保存为模板 (Save as Template)..."), false, () =>
                {
                    if (_viewModel == null) return;
                    var targetFrame = _viewModel.Graph.SubGraphFrames
                        .FirstOrDefault(f => f.Id == capturedFrameId);
                    if (targetFrame == null) return;
                    Templates.BlueprintTemplateUtils.SaveAsTemplate(
                        _viewModel.Graph, targetFrame, CreateGraphSerializer());
                });

                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("删除节点 (Delete)"), false, () =>
            {
                if (_viewModel == null) return;
                var deletedNodeIds = _viewModel.Selection.SelectedNodeIds.ToList();
                _viewModel.DeleteSelected();
                if (deletedNodeIds.Count > 0)
                {
                    foreach (var deletedNodeId in deletedNodeIds)
                        RemovePreviewMarkerNodeIndexForNode(deletedNodeId);

                    MarkPreviewDirtyForNodes(deletedNodeIds, "NodeContext.DeleteSelected");
                }
                Repaint();
            });

            // 如果有多个节点选中，添加打组选项
            if (_viewModel.Selection.SelectedNodeIds.Count > 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图 (Create SubGraph)"), false, () =>
                {
                    if (_viewModel == null) return;
                    var selectedIds = _viewModel.Selection.SelectedNodeIds.ToList();
                    var name = EditorInputDialog.Show("创建子蓝图", "请输入子蓝图名称：", "新子蓝图");
                    if (string.IsNullOrWhiteSpace(name)) return;
                    _viewModel.Commands.Execute(new GroupNodesCommand(name, selectedIds));
                    _viewModel.Selection.ClearSelection();
                    _viewModel.RequestRepaint();
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// 右键点击端口的回调。显示该端口连线管理菜单。
        /// </summary>
        private void OnPortContextMenu(GraphPort port, Vec2 canvasPos)
        {
            if (_viewModel == null) return;

            var edges = _viewModel.Graph.GetEdgesForPort(port.Id).ToList();
            if (edges.Count == 0) return; // 没有连线，不显示菜单

            var menu = new GenericMenu();

            foreach (var edge in edges)
            {
                // 找到连线的另一端
                var otherPortId = edge.SourcePortId == port.Id ? edge.TargetPortId : edge.SourcePortId;
                var otherPort = _viewModel.Graph.FindPort(otherPortId);
                var otherNode = otherPort != null ? _viewModel.Graph.FindNode(otherPort.NodeId) : null;

                string label = otherNode != null
                    ? $"断开连线 (Disconnect): {otherNode.TypeId}.{otherPort!.Name}"
                    : $"断开连线 (Disconnect): {otherPortId}";

                var capturedEdgeId = edge.Id;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (_viewModel == null) return;
                    _viewModel.Commands.Execute(new DisconnectCommand(capturedEdgeId));
                    _viewModel.RequestRepaint();
                    Repaint();
                });
            }

            if (edges.Count > 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("断开所有连线 (Disconnect All)"), false, () =>
                {
                    if (_viewModel == null) return;
                    using (_viewModel.Commands.BeginCompound("断开所有连线"))
                    {
                        foreach (var e in edges)
                            _viewModel.Commands.Execute(new DisconnectCommand(e.Id));
                    }
                    _viewModel.RequestRepaint();
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        // ── 双向联动回调 ──

        /// <summary>
        /// 蓝图编辑器中选中节点变化时的回调。
        /// 收集选中节点关联的 MarkerId，通知 Scene View 高亮。
        /// </summary>
        private void OnBlueprintSelectionChanged()
        {
            if (_viewModel == null) return;

            var markerIds = new List<string>();
            var graph = _viewModel.Graph;
            var registry = GetActionRegistry();

            foreach (var nodeId in _viewModel.Selection.SelectedNodeIds)
            {
                var node = graph.FindNode(nodeId);
                if (node?.UserData is not Core.ActionNodeData data) continue;

                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;

                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);

                    // 策略 1：从 BindingContext 获取 GameObject 引用
                    GameObject? boundObj = _bindingContext?.Get(scopedBindingKey);

                    // 策略 2：BindingContext 为空时，用 PropertyBag 中的 MarkerId 回退查找
                    if (boundObj == null)
                    {
                        var storedId = data.Properties.Get<string>(prop.Key);
                        if (!string.IsNullOrEmpty(storedId))
                        {
                            var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
                            if (marker != null)
                            {
                                boundObj = marker.gameObject;
                                _bindingContext?.Set(scopedBindingKey, boundObj);
                            }
                        }
                    }

                    if (boundObj == null) continue;

                    var markerComp = boundObj.GetComponent<SceneMarker>();
                    if (markerComp != null && !string.IsNullOrEmpty(markerComp.MarkerId))
                        markerIds.Add(markerComp.MarkerId);
                }
            }

            SceneMarkerSelectionBridge.NotifyBlueprintSelectionChanged(markerIds);
        }

        /// <summary>
        /// 场景中选中标记时的回调——在蓝图中高亮引用该标记的节点。
        /// </summary>
        private void OnSceneMarkerSelected(string markerId)
        {
            if (_viewModel == null) return;

            var marker = SceneMarkerSelectionBridge.FindMarkerInScene(markerId);
            if (marker == null)
            {
                _viewModel.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                _viewModel.RequestRepaint();
                Repaint();
                return;
            }

            // M14：蓝图侧节点高亮同样受 Tag 过滤表达式约束，保持与 SceneView 可见性一致。
            if (MarkerLayerSystem.HasTagFilter
                && !Core.TagExpressionMatcher.Evaluate(MarkerLayerSystem.TagFilterExpression, marker.Tag))
            {
                _viewModel.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                _viewModel.RequestRepaint();
                Repaint();
                return;
            }

            var registry = GetActionRegistry();
            var nodeIds = new List<string>();

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;

                    // PropertyBag 中存储的是 MarkerId，直接比较即可
                    var storedId = data.Properties.Get<string>(prop.Key);
                    if (!string.IsNullOrEmpty(storedId) && storedId == markerId)
                    {
                        nodeIds.Add(node.Id);
                        break;
                    }
                }
            }

            if (nodeIds.Count > 0)
            {
                _viewModel.Selection.SelectMultiple(nodeIds);
            }
            else
            {
                // 未找到引用该标记的节点 → 清除蓝图选中
                _viewModel.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
            }
            _viewModel.RequestRepaint();
            Repaint();
        }

        /// <summary>
        /// Unity 编辑器中选中对象变化时的回调。
        /// 如果选中的是 SceneMarker，通知 Bridge 触发蓝图侧高亮。
        /// </summary>
        private void OnUnitySelectionChanged()
        {
            if (_viewModel == null) return;

            var go = Selection.activeGameObject;
            SBLog.Debug(SBLogTags.Selection, $"OnUnitySelectionChanged: activeGameObject={go?.name ?? "null"}");

            if (go == null)
            {
                // 取消选中 → 清除蓝图选中
                _viewModel.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                _viewModel.RequestRepaint();
                Repaint();
                return;
            }

            var marker = go.GetComponent<SceneMarker>();
            if (marker != null && !string.IsNullOrEmpty(marker.MarkerId))
            {
                // 选中了 SceneMarker → 联动到蓝图
                SBLog.Debug(SBLogTags.Selection, $"是 SceneMarker: {marker.MarkerId}");
                SceneMarkerSelectionBridge.NotifySceneMarkerSelected(marker.MarkerId);
            }
            else
            {
                // 选中了非标记对象 → 清除蓝图中的联动选中
                SBLog.Debug(SBLogTags.Selection, "非 SceneMarker，清除蓝图选中");
                _viewModel.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                _viewModel.RequestRepaint();
                Repaint();
            }
        }

        /// <summary>
        /// 节点属性修改时的回调——刷新预览。
        /// </summary>
        private void OnNodePropertyChanged(string nodeId, ActionNodeData nodeData)
        {
            if (_viewModel == null) return;

            string areaBindingId = nodeData.ActionTypeId == "Location.RandomArea"
                ? (nodeData.Properties.Get<string>("area") ?? "")
                : "";

            SBLog.Info(
                SBLogTags.Pipeline,
                "OnNodePropertyChanged: node={0}, action={1}, areaId='{2}', previewContext={3}",
                nodeId,
                nodeData.ActionTypeId,
                areaBindingId,
                GetPreviewContextId());

            UpdatePreviewMarkerNodeIndexForNode(nodeId, nodeData);
            MarkPreviewDirtyForNode(nodeId, "NodePropertyChanged");
        }

        /// <summary>
        /// 场景中双击标记时的回调——在蓝图中聚焦到引用该标记的节点。
        /// </summary>
        private void OnSceneMarkerDoubleClicked(string markerId)
        {
            if (_viewModel == null) return;

            var nodeIds = SceneMarkerSelectionBridge.FindNodesReferencingMarker(
                _viewModel.Graph, markerId);

            if (nodeIds.Count == 0) return;

            // 选中并聚焦到第一个节点
            _viewModel.Selection.Select(nodeIds[0]);
            var node = _viewModel.Graph.FindNode(nodeIds[0]);
            if (node != null)
            {
                // 将画布平移到节点位置
                _viewModel.PanOffset = new Vec2(
                    position.width / 2f - node.Position.X * _viewModel.ZoomLevel,
                    position.height / 2f - node.Position.Y * _viewModel.ZoomLevel);
            }

            _viewModel.RequestRepaint();
            Repaint();
        }

        // ── 辅助方法 ──

        private static bool IsInputEvent(Event evt)
        {
            return evt.type == EventType.MouseDown
                || evt.type == EventType.MouseUp
                || evt.type == EventType.MouseDrag
                || evt.type == EventType.ScrollWheel
                || evt.type == EventType.KeyDown
                || evt.type == EventType.KeyUp
                || evt.type == EventType.ContextClick;
        }
    }
}
