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
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;
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
        private BlueprintAsset? _currentAsset;

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
        private const int MaxIssueRenderCount = 300;
        private const int MaxRelationGroupRenderCount = 200;
        private const int MaxRelationUsageRenderCountPerGroup = 120;
        private const string WorkbenchVisiblePrefsKey = "SceneBlueprint.Workbench.Visible";
        private const string WorkbenchWidthPrefsKey = "SceneBlueprint.Workbench.Width";
        private const string WorkbenchTabPrefsKey = "SceneBlueprint.Workbench.Tab";
        private const string WorkbenchIssueSeverityFilterPrefsKey = "SceneBlueprint.Workbench.Issue.SeverityFilter";
        private const string WorkbenchIssueKindFilterPrefsKey = "SceneBlueprint.Workbench.Issue.KindFilter";
        private const string WorkbenchIssueGroupExpandedPrefsPrefix = "SceneBlueprint.Workbench.Issue.GroupExpanded.";
        private float _inspectorWidth = DefaultInspectorWidth;
        private float _workbenchWidth = DefaultWorkbenchWidth;
        private bool _isDraggingSplitter;
        private bool _isDraggingWorkbenchSplitter;
        private bool _showWorkbench = true;
        private Vector2 _guideScroll;
        private Vector2 _issueScroll;
        private Vector2 _relationScroll;
        private WorkbenchTab _workbenchTab = WorkbenchTab.Guide;
        private IssueSeverityFilter _issueSeverityFilter = IssueSeverityFilter.All;
        private IssueKindFilter _issueKindFilter = IssueKindFilter.All;
        private readonly List<WorkbenchIssue> _workbenchIssues = new List<WorkbenchIssue>();
        private readonly Dictionary<IssueSourceGroup, bool> _issueGroupExpandedState =
            new Dictionary<IssueSourceGroup, bool>();
        private readonly List<WorkbenchRelationGroup> _workbenchRelationGroups = new List<WorkbenchRelationGroup>();
        private bool _workbenchIssuesDirty = true;
        private bool _workbenchRelationsDirty = true;
        private bool _hasWorkbenchIssueScan;
        private bool _useEditorToolSelectionInput = true;
        private Core.ActionRegistry? _actionRegistryCache;
        private IEditorSpatialModeDescriptor? _spatialModeDescriptor;
        private readonly SceneBlueprintToolContext _toolContext = new SceneBlueprintToolContext();
        private readonly ISceneBindingStore _sceneBindingStore = new SceneManagerBindingStore();

        private enum WorkbenchTab
        {
            Guide,
            Issues,
            Relations
        }

        private enum WorkbenchIssueKind
        {
            MissingBinding,
            MissingRequiredProperty,
            BrokenReference,
            Other
        }

        private enum WorkbenchIssueSeverity
        {
            Info,
            Warning,
            Error
        }

        private enum IssueSeverityFilter
        {
            All,
            Error,
            Warning,
            Info
        }

        private enum IssueKindFilter
        {
            All,
            MissingBinding,
            MissingRequiredProperty,
            BrokenReference,
            Other
        }

        private enum IssueSourceGroup
        {
            MarkerValidator,
            Exporter,
            Rule,
            Other
        }

        private sealed class WorkbenchIssue
        {
            public WorkbenchIssueKind Kind;
            public WorkbenchIssueSeverity Severity;
            public string Message = "";
            public string Source = "";
            public string? NodeId;
            public string? MarkerId;
        }

        private sealed class WorkbenchRelationUsage
        {
            public string ActionTypeId = "";
            public string ActionDisplayName = "";
            public string BindingKey = "";
            public string? NodeId;
            public bool FromCurrentGraph;
        }

        private sealed class WorkbenchRelationGroup
        {
            public string PresetId = "";
            public string Title = "";
            public bool HasPresetAsset;
            public List<WorkbenchRelationUsage> Usages = new List<WorkbenchRelationUsage>();
        }

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
            _workbenchTab = ReadEnumPref(WorkbenchTabPrefsKey, WorkbenchTab.Guide);
            _issueSeverityFilter = ReadEnumPref(WorkbenchIssueSeverityFilterPrefsKey, IssueSeverityFilter.All);
            _issueKindFilter = ReadEnumPref(WorkbenchIssueKindFilterPrefsKey, IssueKindFilter.All);
            _useEditorToolSelectionInput = MarkerSelectionInputRoutingSettings.LoadUseEditorTool();
            _toolContext.Attach(_useEditorToolSelectionInput);
            GizmoRenderPipeline.SetInteractionMode(GizmoRenderPipeline.MarkerInteractionMode.Edit);
            _workbenchIssuesDirty = true;
            _workbenchRelationsDirty = true;

            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            EditorApplication.projectChanged -= OnEditorProjectChanged;
            EditorApplication.projectChanged += OnEditorProjectChanged;

            InitializeIfNeeded();
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(WorkbenchVisiblePrefsKey, _showWorkbench);
            EditorPrefs.SetFloat(WorkbenchWidthPrefsKey, _workbenchWidth);
            EditorPrefs.SetInt(WorkbenchTabPrefsKey, (int)_workbenchTab);
            EditorPrefs.SetInt(WorkbenchIssueSeverityFilterPrefsKey, (int)_issueSeverityFilter);
            EditorPrefs.SetInt(WorkbenchIssueKindFilterPrefsKey, (int)_issueKindFilter);
            MarkerSelectionInputRoutingSettings.SaveUseEditorTool(_useEditorToolSelectionInput);

            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            EditorApplication.projectChanged -= OnEditorProjectChanged;

            SceneViewMarkerTool.OnMarkerCreated -= OnMarkerCreated;
            MarkerGroupCreationTool.OnGroupCreated -= OnMarkerGroupCreated;
            _toolContext.Detach();

            // 取消双向联动订阅
            if (_viewModel != null)
                _viewModel.Selection.OnSelectionChanged -= OnBlueprintSelectionChanged;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested -= OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested -= OnSceneMarkerDoubleClicked;
            SceneMarkerSelectionBridge.ClearHighlight();
            Selection.selectionChanged -= OnUnitySelectionChanged;

            _viewModel = null;
            _renderer = null;
            _input = null;
            _editContext = null;
            _coordinateHelper = null;
            _profile = null;
            _inspectorPanel = null;
            _inspectorDrawer = null;
            _currentAsset = null;
            _bindingContext = null;
            _actionRegistryCache = null;
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
            _inspectorPanel = new InspectorPanel(_inspectorDrawer);

            if (_bindingContext == null)
                _bindingContext = new BindingContext();
            _inspectorDrawer.SetBindingContext(_bindingContext);
            _inspectorDrawer.SetGraph(_viewModel.Graph);

            // 8. 启用 Scene View 标记工具（P3：由 ToolContext 托管生命周期）
            _toolContext.EnableMarkerTool(actionRegistry, EnsureSpatialModeDescriptor());
            SceneViewMarkerTool.OnMarkerCreated -= OnMarkerCreated;
            SceneViewMarkerTool.OnMarkerCreated += OnMarkerCreated;
            MarkerGroupCreationTool.OnGroupCreated -= OnMarkerGroupCreated;
            MarkerGroupCreationTool.OnGroupCreated += OnMarkerGroupCreated;

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
                // 输入事件：仅在画布区域内处理
                _viewModel.PreUpdateNodeSizes();
                _viewModel.ProcessInput(_input);

                // 仅在可能修改图数据的输入后标记为脏，避免纯导航/缩放导致关系与问题缓存失效
                if (evt.type == EventType.KeyDown || evt.type == EventType.MouseUp)
                    MarkWorkbenchDataDirty();

                // 标记事件已消费，防止 Unity IMGUI 将事件继续传播到
                // Inspector 面板等其他控件，避免焦点抢夺和拖拽序列追踪失效
                evt.Use();
            }

            // ── Inspector 面板 ──
            if (_inspectorPanel.Draw(inspectorRect, _viewModel))
            {
                // 属性被修改，刷新画布摘要显示
                _viewModel.RequestRepaint();
                MarkWorkbenchDataDirty();
            }

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

        private void AppendBrokenReferenceIssues(
            List<WorkbenchIssue> issues,
            HashSet<string> dedupe,
            Core.ActionRegistry registry,
            Dictionary<string, MarkerPresetSO> presetById)
        {
            if (_viewModel == null)
                return;

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData actionData)
                    continue;
                if (!registry.TryGet(actionData.ActionTypeId, out var actionDef))
                    continue;

                foreach (var req in actionDef.SceneRequirements)
                {
                    if (string.IsNullOrEmpty(req.PresetId))
                        continue;

                    if (presetById.ContainsKey(req.PresetId))
                        continue;

                    string message =
                        $"节点 '{node.Id}' (TypeId: {actionData.ActionTypeId}) 引用了不存在的 PresetId: {req.PresetId} (BindingKey: {req.BindingKey})";
                    AddWorkbenchIssue(
                        issues,
                        dedupe,
                        WorkbenchIssueKind.BrokenReference,
                        WorkbenchIssueSeverity.Error,
                        message,
                        "PresetReference",
                        node.Id,
                        null);
                }
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

            bool showWorkbench = GUILayout.Toggle(
                _showWorkbench,
                new GUIContent("工作台", "显示 C6 工作台（向导 / 问题中心 / 关系面板）"),
                EditorStyles.toolbarButton,
                GUILayout.Width(52));
            if (showWorkbench != _showWorkbench)
            {
                _showWorkbench = showWorkbench;
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

            GUILayout.Space(6);

            // 显示已注册的节点类型数量
            int typeCount = _profile?.NodeTypes?.GetAll()?.Count() ?? 0;
            GUILayout.Label($"已注册 {typeCount} 种行动类型", EditorStyles.miniLabel);
            
            // 刷新节点按钮（解决程序集延迟加载导致的节点遗漏问题）
            if (GUILayout.Button(new GUIContent("刷新", "重新扫描并加载所有节点类型"), 
                EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                RefreshNodeTypes();
            }

            GUILayout.Space(8);

            bool useToolSelection = GUILayout.Toggle(
                _useEditorToolSelectionInput,
                new GUIContent("Tool选中", "启用 ToolContext 托管的标记选中/创建输入（P3）；关闭后回退兼容链路"),
                EditorStyles.toolbarButton,
                GUILayout.Width(68));
            if (useToolSelection != _useEditorToolSelectionInput)
                SetSelectionInputRouting(useToolSelection);

            GUILayout.Space(6);

            GUILayout.Label(
                _useEditorToolSelectionInput
                    ? "交互：Tool选中（P3）+ 原生变换"
                    : "交互：兼容回退（duringSceneGui）+ 原生变换",
                EditorStyles.miniLabel);

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

                if (_showWorkbench)
                {
                    int issueErrors = _workbenchIssues.Count(i => i.Severity == WorkbenchIssueSeverity.Error);
                    int issueWarnings = _workbenchIssues.Count(i => i.Severity == WorkbenchIssueSeverity.Warning);
                    string pending = _workbenchIssuesDirty ? "*" : "";
                    GUILayout.Label($"问题: {issueErrors}E/{issueWarnings}W{pending}", EditorStyles.miniLabel);
                }
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

            if (GUILayout.Button("居中", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                CenterView();
            }

            if (GUILayout.Button("帮助", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                ShowHelp();
            }

            GUILayout.Space(6);

            GUILayout.EndHorizontal();
        }

        // ── C6 工作台面板 ──

        private void DrawWorkbenchPanel(Rect panelRect)
        {
            if (_viewModel == null)
                return;

            GUILayout.BeginArea(panelRect);
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                {
                    DrawWorkbenchTabs();

                    switch (_workbenchTab)
                    {
                        case WorkbenchTab.Guide:
                            DrawWorkbenchGuide();
                            break;
                        case WorkbenchTab.Issues:
                            DrawWorkbenchIssues();
                            break;
                        case WorkbenchTab.Relations:
                            DrawWorkbenchRelations();
                            break;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void DrawWorkbenchTabs()
        {
            WorkbenchTab prevTab = _workbenchTab;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Toggle(_workbenchTab == WorkbenchTab.Guide, "向导", EditorStyles.toolbarButton))
                    _workbenchTab = WorkbenchTab.Guide;

                if (GUILayout.Toggle(_workbenchTab == WorkbenchTab.Issues, "问题中心", EditorStyles.toolbarButton))
                    _workbenchTab = WorkbenchTab.Issues;

                if (GUILayout.Toggle(_workbenchTab == WorkbenchTab.Relations, "关系面板", EditorStyles.toolbarButton))
                    _workbenchTab = WorkbenchTab.Relations;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    if (_workbenchTab == WorkbenchTab.Relations)
                        RefreshWorkbenchRelations();
                    else
                        RefreshWorkbenchIssues(includeExportValidation: false);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (prevTab != _workbenchTab)
            {
                EditorPrefs.SetInt(WorkbenchTabPrefsKey, (int)_workbenchTab);
            }
        }

        private void OnEditorHierarchyChanged()
        {
            MarkWorkbenchIssuesDirty();
        }

        private void OnEditorProjectChanged()
        {
            _actionRegistryCache = null;
            MarkWorkbenchDataDirty();
        }

        private void DrawWorkbenchGuide()
        {
            if (_viewModel == null) return;

            bool hasNodes = _viewModel.Graph.Nodes.Count > 0;
            bool hasSavedAsset = _currentAsset != null;
            bool hasSyncedScene = HasSyncedSceneBindings();
            int blockingIssues = _workbenchIssues.Count(i => i.Severity == WorkbenchIssueSeverity.Error);

            EditorGUILayout.HelpBox("按以下步骤可在单窗口完成“新建玩法 → 导出”闭环。", MessageType.Info);

            _guideScroll = EditorGUILayout.BeginScrollView(_guideScroll);
            {
                DrawGuideStep(
                    1,
                    "创建玩法骨架",
                    hasNodes,
                    hasNodes ? "图中已有节点，可继续配置。" : "建议先新建图并保留默认 Start/End。",
                    NewGraph,
                    hasNodes ? "重建" : "开始");

                DrawGuideStep(
                    2,
                    "保存蓝图资产",
                    hasSavedAsset,
                    hasSavedAsset ? "已保存到 BlueprintAsset。" : "未保存时无法同步到场景。",
                    SaveBlueprint,
                    "保存");

                DrawGuideStep(
                    3,
                    "同步到场景",
                    hasSyncedScene,
                    hasSyncedScene ? "场景绑定存储已关联当前蓝图。" : "将作用域绑定写入场景绑定存储。",
                    SyncToScene,
                    "同步");

                DrawGuideStep(
                    4,
                    "运行问题检查",
                    _hasWorkbenchIssueScan,
                    blockingIssues > 0
                        ? $"当前有 {blockingIssues} 个阻塞问题。"
                        : "当前无阻塞错误，可继续导出。",
                    () =>
                    {
                        RefreshWorkbenchIssues(includeExportValidation: true);
                        _workbenchTab = WorkbenchTab.Issues;
                    },
                    "检查");

                DrawGuideStep(
                    5,
                    "导出运行时 JSON",
                    hasSavedAsset && hasSyncedScene && blockingIssues == 0,
                    "导出前建议先清空问题中心中的 Error。",
                    ExportBlueprint,
                    "导出");
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawGuideStep(
            int index,
            string title,
            bool done,
            string detail,
            System.Action action,
            string actionLabel)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    string status = done ? "[x]" : "[ ]";
                    EditorGUILayout.LabelField($"{status} Step {index}: {title}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(actionLabel, GUILayout.Width(52)))
                        action();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawWorkbenchIssues()
        {
            int missingBinding = _workbenchIssues.Count(i => i.Kind == WorkbenchIssueKind.MissingBinding);
            int missingRequired = _workbenchIssues.Count(i => i.Kind == WorkbenchIssueKind.MissingRequiredProperty);
            int brokenReference = _workbenchIssues.Count(i => i.Kind == WorkbenchIssueKind.BrokenReference);

            EditorGUILayout.HelpBox(
                $"缺失绑定: {missingBinding}   必填缺失: {missingRequired}   引用失效: {brokenReference}",
                MessageType.None);

            if (_workbenchIssuesDirty)
            {
                EditorGUILayout.HelpBox(
                    "问题数据已变更。为避免切页卡顿，默认不自动重算，请点击“刷新问题”或“深度校验”。",
                    MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("刷新问题", GUILayout.Width(72)))
                    RefreshWorkbenchIssues(includeExportValidation: false);

                if (GUILayout.Button("深度校验", GUILayout.Width(72)))
                    RefreshWorkbenchIssues(includeExportValidation: true);

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();

            IssueSeverityFilter prevSeverityFilter = _issueSeverityFilter;
            IssueKindFilter prevKindFilter = _issueKindFilter;

            EditorGUILayout.BeginHorizontal();
            {
                _issueSeverityFilter = (IssueSeverityFilter)EditorGUILayout.EnumPopup(
                    "级别",
                    _issueSeverityFilter,
                    GUILayout.Width(150));

                _issueKindFilter = (IssueKindFilter)EditorGUILayout.EnumPopup(
                    "类型",
                    _issueKindFilter,
                    GUILayout.Width(220));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("清空筛选", GUILayout.Width(64)))
                {
                    _issueSeverityFilter = IssueSeverityFilter.All;
                    _issueKindFilter = IssueKindFilter.All;
                    EditorPrefs.SetInt(WorkbenchIssueSeverityFilterPrefsKey, (int)_issueSeverityFilter);
                    EditorPrefs.SetInt(WorkbenchIssueKindFilterPrefsKey, (int)_issueKindFilter);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (prevSeverityFilter != _issueSeverityFilter)
                EditorPrefs.SetInt(WorkbenchIssueSeverityFilterPrefsKey, (int)_issueSeverityFilter);
            if (prevKindFilter != _issueKindFilter)
                EditorPrefs.SetInt(WorkbenchIssueKindFilterPrefsKey, (int)_issueKindFilter);

            var filteredIssues = _workbenchIssues.Where(PassIssueFilters).ToList();
            var renderedIssues = filteredIssues.Count > MaxIssueRenderCount
                ? filteredIssues.Take(MaxIssueRenderCount).ToList()
                : filteredIssues;
            EditorGUILayout.LabelField(
                $"显示 {filteredIssues.Count}/{_workbenchIssues.Count} 条",
                EditorStyles.miniLabel);

            if (filteredIssues.Count > renderedIssues.Count)
            {
                EditorGUILayout.HelpBox(
                    $"问题较多，仅渲染前 {renderedIssues.Count} 条以提升性能。请使用筛选缩小范围。",
                    MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("仅错误", GUILayout.Width(56)))
                {
                    _issueSeverityFilter = IssueSeverityFilter.Error;
                    EditorPrefs.SetInt(WorkbenchIssueSeverityFilterPrefsKey, (int)_issueSeverityFilter);
                }

                if (GUILayout.Button("定位首个阻塞", GUILayout.Width(88)))
                {
                    FocusFirstBlockingIssue(filteredIssues);
                }

                if (GUILayout.Button("复制摘要", GUILayout.Width(64)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildIssueSummary(filteredIssues);
                }

                if (GUILayout.Button("展开全部", GUILayout.Width(64)))
                {
                    SetAllIssueGroupExpanded(true);
                }

                if (GUILayout.Button("折叠全部", GUILayout.Width(64)))
                {
                    SetAllIssueGroupExpanded(false);
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();

            var groupedIssues = renderedIssues
                .GroupBy(i => GetIssueSourceGroup(i.Source))
                .OrderBy(g => GetIssueSourceOrder(g.Key))
                .ToList();

            _issueScroll = EditorGUILayout.BeginScrollView(_issueScroll);
            {
                if (filteredIssues.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无问题。", MessageType.Info);
                }
                else
                {
                    foreach (var group in groupedIssues)
                    {
                        var issuesInGroup = group.ToList();
                        var nodeIds = issuesInGroup
                            .Select(i => i.NodeId)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .Select(id => id!)
                            .Distinct()
                            .ToList();
                        var markerIds = issuesInGroup
                            .Select(i => i.MarkerId)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .Select(id => id!)
                            .Distinct()
                            .ToList();
                        int errorCount = issuesInGroup.Count(i => i.Severity == WorkbenchIssueSeverity.Error);
                        int warningCount = issuesInGroup.Count(i => i.Severity == WorkbenchIssueSeverity.Warning);
                        bool expanded = GetIssueGroupExpanded(group.Key);

                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        {
                            expanded = EditorGUILayout.Foldout(
                                expanded,
                                $"来源组：{GetIssueSourceGroupLabel(group.Key)} ({issuesInGroup.Count})  E:{errorCount} W:{warningCount}",
                                true);
                            SetIssueGroupExpanded(group.Key, expanded);

                            GUILayout.FlexibleSpace();

                            if (nodeIds.Count > 0
                                && GUILayout.Button($"批量定位节点 ({nodeIds.Count})", GUILayout.Width(120)))
                            {
                                NavigateToNodesBatch(nodeIds);
                            }

                            if (markerIds.Count > 0
                                && GUILayout.Button($"批量定位标记 ({markerIds.Count})", GUILayout.Width(120)))
                            {
                                FocusMarkersBatch(markerIds);
                            }
                        }
                        EditorGUILayout.EndHorizontal();

                        if (!expanded)
                            continue;

                        foreach (var issue in issuesInGroup)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            {
                                EditorGUILayout.BeginHorizontal();
                                {
                                    string severity = issue.Severity switch
                                    {
                                        WorkbenchIssueSeverity.Error => "Error",
                                        WorkbenchIssueSeverity.Warning => "Warn",
                                        _ => "Info"
                                    };
                                    EditorGUILayout.LabelField(
                                        $"[{severity}] {GetIssueKindLabel(issue.Kind)}",
                                        EditorStyles.boldLabel);
                                    GUILayout.FlexibleSpace();

                                    if (!string.IsNullOrEmpty(issue.NodeId)
                                        && GUILayout.Button("定位节点", GUILayout.Width(60)))
                                    {
                                        NavigateToNode(issue.NodeId!);
                                    }

                                    if (!string.IsNullOrEmpty(issue.MarkerId)
                                        && GUILayout.Button("定位标记", GUILayout.Width(60)))
                                    {
                                        FocusMarker(issue.MarkerId!);
                                    }
                                }
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                                EditorGUILayout.LabelField($"来源: {issue.Source}", EditorStyles.miniLabel);
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawWorkbenchRelations()
        {
            if (_viewModel == null)
                return;

            if (_workbenchRelationsDirty)
            {
                EditorGUILayout.HelpBox(
                    "关系数据已变更。为避免切页卡顿，默认不自动重算，请点击顶部“刷新”。",
                    MessageType.Info);
            }

            _relationScroll = EditorGUILayout.BeginScrollView(_relationScroll);
            {
                if (_workbenchRelationGroups.Count == 0)
                {
                    EditorGUILayout.HelpBox("未找到 Preset 引用。", MessageType.Info);
                }
                else
                {
                    int renderedGroupCount = 0;
                    foreach (var group in _workbenchRelationGroups)
                    {
                        if (renderedGroupCount >= MaxRelationGroupRenderCount)
                            break;
                        renderedGroupCount++;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"PresetId: {group.PresetId}", EditorStyles.miniLabel);

                            if (!group.HasPresetAsset)
                                EditorGUILayout.HelpBox("该 PresetId 在项目中不存在。", MessageType.Error);

                            if (group.Usages.Count == 0)
                            {
                                EditorGUILayout.HelpBox("未被任何 Action 引用。", MessageType.Warning);
                            }
                            else
                            {
                                int usageRenderCount = 0;
                                foreach (var usage in group.Usages)
                                {
                                    if (usageRenderCount >= MaxRelationUsageRenderCountPerGroup)
                                        break;
                                    usageRenderCount++;

                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        string scopeLabel = usage.FromCurrentGraph ? "当前图" : "定义";
                                        EditorGUILayout.LabelField(
                                            $"- [{scopeLabel}] {usage.ActionDisplayName} ({usage.ActionTypeId}) · {usage.BindingKey}",
                                            EditorStyles.wordWrappedMiniLabel);

                                        if (!string.IsNullOrEmpty(usage.NodeId)
                                            && GUILayout.Button("定位节点", GUILayout.Width(60)))
                                        {
                                            NavigateToNode(usage.NodeId!);
                                        }
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }

                                if (group.Usages.Count > MaxRelationUsageRenderCountPerGroup)
                                {
                                    EditorGUILayout.LabelField(
                                        $"... 其余 {group.Usages.Count - MaxRelationUsageRenderCountPerGroup} 条引用已省略（性能保护）",
                                        EditorStyles.miniLabel);
                                }
                            }
                        }
                        EditorGUILayout.EndVertical();
                    }

                    if (_workbenchRelationGroups.Count > MaxRelationGroupRenderCount)
                    {
                        EditorGUILayout.HelpBox(
                            $"关系组较多，仅渲染前 {MaxRelationGroupRenderCount} 组（性能保护）。",
                            MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void RefreshWorkbenchRelations()
        {
            if (_viewModel == null)
                return;

            var registry = GetActionRegistry();
            var presets = MarkerPresetRegistry.GetAll();
            var presetById = BuildPresetLookup(presets);
            var relationMap = new Dictionary<string, List<WorkbenchRelationUsage>>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var actionDef in registry.GetAll())
            {
                foreach (var req in actionDef.SceneRequirements)
                {
                    if (string.IsNullOrEmpty(req.PresetId))
                        continue;

                    if (!relationMap.TryGetValue(req.PresetId, out var list))
                    {
                        list = new List<WorkbenchRelationUsage>();
                        relationMap[req.PresetId] = list;
                    }

                    list.Add(new WorkbenchRelationUsage
                    {
                        ActionTypeId = actionDef.TypeId,
                        ActionDisplayName = actionDef.DisplayName,
                        BindingKey = req.BindingKey,
                        NodeId = null,
                        FromCurrentGraph = false
                    });
                }
            }

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData actionData)
                    continue;
                if (!registry.TryGet(actionData.ActionTypeId, out var actionDef))
                    continue;

                foreach (var req in actionDef.SceneRequirements)
                {
                    if (string.IsNullOrEmpty(req.PresetId))
                        continue;

                    if (!relationMap.TryGetValue(req.PresetId, out var list))
                    {
                        list = new List<WorkbenchRelationUsage>();
                        relationMap[req.PresetId] = list;
                    }

                    list.Add(new WorkbenchRelationUsage
                    {
                        ActionTypeId = actionDef.TypeId,
                        ActionDisplayName = actionDef.DisplayName,
                        BindingKey = req.BindingKey,
                        NodeId = node.Id,
                        FromCurrentGraph = true
                    });
                }
            }

            var orderedPresetIds = relationMap.Keys
                .Concat(presetById.Keys)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id)
                .ToList();

            _workbenchRelationGroups.Clear();
            foreach (var presetId in orderedPresetIds)
            {
                relationMap.TryGetValue(presetId, out var refs);
                bool hasPresetAsset = presetById.TryGetValue(presetId, out var preset);

                var group = new WorkbenchRelationGroup
                {
                    PresetId = presetId,
                    Title = hasPresetAsset ? preset!.DisplayName : "(缺失预设资产)",
                    HasPresetAsset = hasPresetAsset,
                    Usages = refs == null
                        ? new List<WorkbenchRelationUsage>()
                        : refs
                            .OrderByDescending(v => v.FromCurrentGraph)
                            .ThenBy(v => v.ActionDisplayName)
                            .ThenBy(v => v.ActionTypeId)
                            .ThenBy(v => v.BindingKey)
                            .ToList()
                };

                _workbenchRelationGroups.Add(group);
            }

            _workbenchRelationsDirty = false;
        }

        private void RefreshWorkbenchIssues(bool includeExportValidation = false)
        {
            if (_viewModel == null)
                return;

            var nextIssues = new List<WorkbenchIssue>();
            var dedupe = new HashSet<string>();
            var registry = GetActionRegistry();
            var presetById = BuildPresetLookup(MarkerPresetRegistry.GetAll());

            var markerReport = MarkerBindingValidator.Validate(_viewModel.Graph, registry);
            foreach (var entry in markerReport.Entries)
            {
                var severity = entry.Level switch
                {
                    ValidationEntry.Severity.Error => WorkbenchIssueSeverity.Error,
                    ValidationEntry.Severity.Warning => WorkbenchIssueSeverity.Warning,
                    _ => WorkbenchIssueSeverity.Info
                };

                AddWorkbenchIssue(
                    nextIssues,
                    dedupe,
                    ClassifyIssue(entry.Message),
                    severity,
                    entry.Message,
                    "MarkerValidator",
                    entry.NodeId,
                    entry.MarkerId);
            }

            AppendRequiredPropertyIssues(nextIssues, dedupe);
            AppendBrokenReferenceIssues(nextIssues, dedupe, registry, presetById);

            if (includeExportValidation)
            {
                var exportOptions = new BlueprintExporter.ExportOptions
                {
                    AdapterType = GetCurrentAdapterType()
                };

                var exportResult = BlueprintExporter.Export(
                    _viewModel.Graph,
                    registry,
                    CollectSceneBindingsForExport(),
                    blueprintId: _currentAsset?.BlueprintId,
                    blueprintName: _currentAsset?.BlueprintName,
                    options: exportOptions);

                foreach (var msg in exportResult.Messages)
                {
                    if (msg.Level == ValidationLevel.Info)
                        continue;

                    var severity = msg.Level switch
                    {
                        ValidationLevel.Error => WorkbenchIssueSeverity.Error,
                        ValidationLevel.Warning => WorkbenchIssueSeverity.Warning,
                        _ => WorkbenchIssueSeverity.Info
                    };

                    AddWorkbenchIssue(
                        nextIssues,
                        dedupe,
                        ClassifyIssue(msg.Message),
                        severity,
                        msg.Message,
                        "BlueprintExporter",
                        TryExtractNodeIdFromMessage(msg.Message),
                        null);
                }
            }

            _workbenchIssues.Clear();
            _workbenchIssues.AddRange(nextIssues
                .OrderByDescending(i => i.Severity)
                .ThenBy(i => i.Kind)
                .ThenBy(i => i.Source));
            _workbenchIssuesDirty = false;
            _hasWorkbenchIssueScan = true;
        }

        private void MarkWorkbenchIssuesDirty()
        {
            _workbenchIssuesDirty = true;
            _hasWorkbenchIssueScan = false;
        }

        private void MarkWorkbenchRelationsDirty()
        {
            _workbenchRelationsDirty = true;
        }

        private void MarkWorkbenchDataDirty()
        {
            MarkWorkbenchIssuesDirty();
            MarkWorkbenchRelationsDirty();
        }

        private Core.ActionRegistry GetActionRegistry()
        {
            _actionRegistryCache ??= SceneBlueprintProfile.CreateActionRegistry();
            return _actionRegistryCache;
        }

        private void AppendRequiredPropertyIssues(List<WorkbenchIssue> issues, HashSet<string> dedupe)
        {
            if (_viewModel == null)
                return;

            var rules = ValidationRuleRegistry.GetEnabled()
                .Where(r => r.Type == ValidationType.PropertyRequired)
                .ToList();

            if (rules.Count == 0)
                return;

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData actionData)
                    continue;

                foreach (var rule in rules)
                {
                    if (!string.Equals(rule.TargetActionTypeId, actionData.ActionTypeId, System.StringComparison.Ordinal))
                        continue;

                    string key = rule.TargetPropertyKey;
                    if (string.IsNullOrEmpty(key))
                        continue;

                    object? raw = actionData.Properties.GetRaw(key);
                    bool missing = raw == null
                        || (raw is string s && string.IsNullOrWhiteSpace(s));

                    if (!missing)
                        continue;

                    string message =
                        $"节点 '{node.Id}' (TypeId: {actionData.ActionTypeId}) 缺少必填属性: {key}";
                    AddWorkbenchIssue(
                        issues,
                        dedupe,
                        WorkbenchIssueKind.MissingRequiredProperty,
                        rule.Severity == ValidationSeverity.Error
                            ? WorkbenchIssueSeverity.Error
                            : WorkbenchIssueSeverity.Warning,
                        message,
                        "ValidationRule",
                        node.Id,
                        null);
                }
            }
        }

        private static void AddWorkbenchIssue(
            List<WorkbenchIssue> issues,
            HashSet<string> dedupe,
            WorkbenchIssueKind kind,
            WorkbenchIssueSeverity severity,
            string message,
            string source,
            string? nodeId,
            string? markerId)
        {
            string key = $"{kind}|{severity}|{source}|{nodeId}|{markerId}|{message}";
            if (dedupe.Contains(key))
                return;

            dedupe.Add(key);
            issues.Add(new WorkbenchIssue
            {
                Kind = kind,
                Severity = severity,
                Message = message,
                Source = source,
                NodeId = nodeId,
                MarkerId = markerId
            });
        }

        private static string? TryExtractNodeIdFromMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            const string token = "节点 '";
            int idx = message.IndexOf(token, System.StringComparison.Ordinal);
            if (idx < 0)
                return null;

            int start = idx + token.Length;
            int end = message.IndexOf('\'', start);
            if (end <= start)
                return null;

            return message.Substring(start, end - start);
        }

        private static WorkbenchIssueKind ClassifyIssue(string message)
        {
            if (string.IsNullOrEmpty(message))
                return WorkbenchIssueKind.Other;

            if (message.IndexOf("缺少必需绑定", System.StringComparison.Ordinal) >= 0
                || message.IndexOf("未配置场景对象", System.StringComparison.Ordinal) >= 0)
            {
                return WorkbenchIssueKind.MissingBinding;
            }

            if (message.IndexOf("缺少必填属性", System.StringComparison.Ordinal) >= 0
                || message.IndexOf("不能为空", System.StringComparison.Ordinal) >= 0
                || message.IndexOf("PropertyRequired", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return WorkbenchIssueKind.MissingRequiredProperty;
            }

            if (message.IndexOf("不存在", System.StringComparison.Ordinal) >= 0
                || message.IndexOf("未找到", System.StringComparison.Ordinal) >= 0
                || message.IndexOf("失效", System.StringComparison.Ordinal) >= 0)
            {
                return WorkbenchIssueKind.BrokenReference;
            }

            return WorkbenchIssueKind.Other;
        }

        private static string GetIssueKindLabel(WorkbenchIssueKind kind)
        {
            return kind switch
            {
                WorkbenchIssueKind.MissingBinding => "缺失绑定",
                WorkbenchIssueKind.MissingRequiredProperty => "缺失必填属性",
                WorkbenchIssueKind.BrokenReference => "引用失效",
                _ => "其他"
            };
        }

        private static Dictionary<string, MarkerPresetSO> BuildPresetLookup(IReadOnlyList<MarkerPresetSO> presets)
        {
            var map = new Dictionary<string, MarkerPresetSO>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var preset in presets)
            {
                if (string.IsNullOrEmpty(preset.PresetId))
                    continue;

                if (!map.ContainsKey(preset.PresetId))
                    map[preset.PresetId] = preset;
            }

            return map;
        }

        private bool PassIssueFilters(WorkbenchIssue issue)
        {
            if (_issueSeverityFilter != IssueSeverityFilter.All)
            {
                if (_issueSeverityFilter == IssueSeverityFilter.Error
                    && issue.Severity != WorkbenchIssueSeverity.Error)
                    return false;
                if (_issueSeverityFilter == IssueSeverityFilter.Warning
                    && issue.Severity != WorkbenchIssueSeverity.Warning)
                    return false;
                if (_issueSeverityFilter == IssueSeverityFilter.Info
                    && issue.Severity != WorkbenchIssueSeverity.Info)
                    return false;
            }

            if (_issueKindFilter != IssueKindFilter.All)
            {
                if (_issueKindFilter == IssueKindFilter.MissingBinding
                    && issue.Kind != WorkbenchIssueKind.MissingBinding)
                    return false;
                if (_issueKindFilter == IssueKindFilter.MissingRequiredProperty
                    && issue.Kind != WorkbenchIssueKind.MissingRequiredProperty)
                    return false;
                if (_issueKindFilter == IssueKindFilter.BrokenReference
                    && issue.Kind != WorkbenchIssueKind.BrokenReference)
                    return false;
                if (_issueKindFilter == IssueKindFilter.Other
                    && issue.Kind != WorkbenchIssueKind.Other)
                    return false;
            }

            return true;
        }

        private static IssueSourceGroup GetIssueSourceGroup(string source)
        {
            if (string.Equals(source, "MarkerValidator", System.StringComparison.Ordinal))
                return IssueSourceGroup.MarkerValidator;

            if (string.Equals(source, "BlueprintExporter", System.StringComparison.Ordinal))
                return IssueSourceGroup.Exporter;

            if (string.Equals(source, "ValidationRule", System.StringComparison.Ordinal)
                || string.Equals(source, "PresetReference", System.StringComparison.Ordinal))
            {
                return IssueSourceGroup.Rule;
            }

            return IssueSourceGroup.Other;
        }

        private static int GetIssueSourceOrder(IssueSourceGroup group)
        {
            return group switch
            {
                IssueSourceGroup.MarkerValidator => 0,
                IssueSourceGroup.Exporter => 1,
                IssueSourceGroup.Rule => 2,
                _ => 3
            };
        }

        private static string GetIssueSourceGroupLabel(IssueSourceGroup group)
        {
            return group switch
            {
                IssueSourceGroup.MarkerValidator => "MarkerValidator",
                IssueSourceGroup.Exporter => "Exporter",
                IssueSourceGroup.Rule => "Rule",
                _ => "Other"
            };
        }

        private bool GetIssueGroupExpanded(IssueSourceGroup group)
        {
            if (_issueGroupExpandedState.TryGetValue(group, out bool expanded))
                return expanded;

            string prefKey = BuildIssueGroupExpandedPrefKey(group);
            expanded = EditorPrefs.GetBool(prefKey, true);
            _issueGroupExpandedState[group] = expanded;
            return expanded;
        }

        private void SetIssueGroupExpanded(IssueSourceGroup group, bool expanded)
        {
            _issueGroupExpandedState[group] = expanded;
            EditorPrefs.SetBool(BuildIssueGroupExpandedPrefKey(group), expanded);
        }

        private void SetAllIssueGroupExpanded(bool expanded)
        {
            foreach (IssueSourceGroup group in System.Enum.GetValues(typeof(IssueSourceGroup)))
            {
                SetIssueGroupExpanded(group, expanded);
            }
        }

        private static string BuildIssueGroupExpandedPrefKey(IssueSourceGroup group)
        {
            return WorkbenchIssueGroupExpandedPrefsPrefix + group;
        }

        private static TEnum ReadEnumPref<TEnum>(string key, TEnum fallback) where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum)
                return fallback;

            int raw = EditorPrefs.GetInt(key, System.Convert.ToInt32(fallback));
            if (!System.Enum.IsDefined(typeof(TEnum), raw))
                return fallback;

            return (TEnum)System.Enum.ToObject(typeof(TEnum), raw);
        }

        /// <summary>
        /// 设置标记选中输入路由。
        /// true  = ToolContext 托管（P3）。
        /// false = 回退到 GizmoRenderPipeline(duringSceneGui) 兼容链路（P0）。
        /// </summary>
        private void SetSelectionInputRouting(bool useEditorTool, bool persist = true)
        {
            _useEditorToolSelectionInput = useEditorTool;

            if (persist)
                MarkerSelectionInputRoutingSettings.SaveUseEditorTool(useEditorTool);

            _toolContext.SetSelectionInputRouting(useEditorTool);
        }

        private void FocusFirstBlockingIssue(IReadOnlyList<WorkbenchIssue> issues)
        {
            var firstError = issues.FirstOrDefault(i => i.Severity == WorkbenchIssueSeverity.Error);
            if (firstError == null)
                return;

            if (!string.IsNullOrEmpty(firstError.NodeId))
            {
                NavigateToNode(firstError.NodeId);
                return;
            }

            if (!string.IsNullOrEmpty(firstError.MarkerId))
                FocusMarker(firstError.MarkerId);
        }

        private string BuildIssueSummary(IReadOnlyList<WorkbenchIssue> issues)
        {
            if (issues.Count == 0)
                return "问题中心：暂无问题";

            int errors = issues.Count(i => i.Severity == WorkbenchIssueSeverity.Error);
            int warnings = issues.Count(i => i.Severity == WorkbenchIssueSeverity.Warning);
            int infos = issues.Count(i => i.Severity == WorkbenchIssueSeverity.Info);

            string groupSummary = string.Join("  ", issues
                .GroupBy(i => GetIssueSourceGroup(i.Source))
                .OrderBy(g => GetIssueSourceOrder(g.Key))
                .Select(g => $"{GetIssueSourceGroupLabel(g.Key)}:{g.Count()}"));

            return $"问题中心共 {issues.Count} 条（E:{errors} W:{warnings} I:{infos}）  {groupSummary}";
        }

        private void NavigateToNodesBatch(IReadOnlyList<string> nodeIds)
        {
            if (_viewModel == null || nodeIds.Count == 0)
                return;

            var validNodeIds = nodeIds
                .Where(id => !string.IsNullOrEmpty(id) && _viewModel.Graph.FindNode(id) != null)
                .Distinct()
                .ToList();
            if (validNodeIds.Count == 0)
                return;

            _viewModel.Selection.SelectMultiple(validNodeIds);
            _viewModel.FocusNodes(validNodeIds);
            _viewModel.RequestRepaint();
            Repaint();
        }

        private static void FocusMarkersBatch(IReadOnlyList<string> markerIds)
        {
            if (markerIds.Count == 0)
                return;

            var markerObjects = markerIds
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(SceneMarkerSelectionBridge.FindMarkerInScene)
                .Where(m => m != null)
                .Select(m => m!.gameObject)
                .Distinct()
                .Cast<Object>()
                .ToArray();
            if (markerObjects.Length == 0)
                return;

            Selection.objects = markerObjects;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void NavigateToNode(string nodeId)
        {
            if (_viewModel == null || string.IsNullOrEmpty(nodeId))
                return;

            _viewModel.Selection.Select(nodeId);
            _viewModel.FocusNodes(new[] { nodeId });
            _viewModel.RequestRepaint();
            Repaint();
        }

        private static void FocusMarker(string markerId)
        {
            if (string.IsNullOrEmpty(markerId))
                return;

            var marker = SceneMarkerSelectionBridge.FindMarkerInScene(markerId);
            if (marker == null)
                return;

            Selection.activeObject = marker.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
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
                MarkWorkbenchDataDirty();
                Repaint();
            }
        }

        /// <summary>新建蓝图时自动添加 Flow.Start 和 Flow.End 节点</summary>
        private void AddDefaultNodes()
        {
            if (_viewModel == null) return;

            var graph = _viewModel.Graph;

            // 检查是否已有 Start/End 节点（加载已有蓝图时不重复添加）
            bool hasStart = graph.Nodes.Any(n => n.TypeId == "Flow.Start");
            bool hasEnd = graph.Nodes.Any(n => n.TypeId == "Flow.End");

            if (!hasStart)
            {
                graph.AddNode("Flow.Start", new Vec2(-200, 0));
            }
            if (!hasEnd)
            {
                graph.AddNode("Flow.End", new Vec2(200, 0));
            }

            _viewModel.PreUpdateNodeSizes();
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
                MarkWorkbenchDataDirty();
                Repaint();

                SBLog.Info(SBLogTags.Blueprint, $"已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();
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
                MarkWorkbenchDataDirty();
                Repaint();

                SBLog.Info(SBLogTags.Blueprint, $"已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();
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

            var registry = SceneBlueprintProfile.CreateActionRegistry();
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

            var registry = SceneBlueprintProfile.CreateActionRegistry();

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
                options: exportOptions);

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
            var boundaryPorts = new PortDefinition[]
            {
                new PortDefinition("激活", PortDirection.Input, PortKind.Control, "exec", PortCapacity.Single, 0),
                new PortDefinition("完成", PortDirection.Output, PortKind.Control, "exec", PortCapacity.Single, 0),
            };

            _viewModel.Commands.Execute(
                new CreateSubGraphCommand(emptySource, title, canvasCenter, boundaryPorts));

            MarkWorkbenchDataDirty();
            _viewModel.RequestRepaint();
            Repaint();

            SBLog.Info(SBLogTags.Blueprint, $"已创建子蓝图: {title}");
        }

        /// <summary>
        /// 刷新节点类型——重新扫描并加载所有 ActionDefinition。
        /// 用于解决程序集延迟加载导致的节点遗漏问题。
        /// </summary>
        private void RefreshNodeTypes()
        {
            if (_viewModel == null) return;

            int oldCount = _profile?.NodeTypes?.GetAll()?.Count() ?? 0;

            UnityEngine.Debug.Log("=== 刷新节点开始 ===");
            UnityEngine.Debug.Log($"刷新前 NodeTypeRegistry 节点数: {oldCount}");

            // 保存当前图数据
            var serializer = CreateGraphSerializer();
            string graphJson = serializer.Serialize(_viewModel.Graph);

            // 重新初始化（会创建新的 NodeTypeRegistry 和 Profile）
            _viewModel = null;
            _profile = null;
            InitializeIfNeeded();

            // 恢复图数据
            try
            {
                var graph = serializer.Deserialize(graphJson);
                _viewModel = null;
                InitializeWithGraph(graph);
            }
            catch (System.Exception ex)
            {
                SBLog.Error(SBLogTags.Blueprint, $"刷新节点类型时恢复图数据失败: {ex}");
                InitializeIfNeeded();
            }

            // === 诊断步骤1：检查 ActionRegistry ===
            UnityEngine.Debug.Log("--- 步骤1：检查 ActionRegistry ---");
            var testActionRegistry = SceneBlueprintProfile.CreateActionRegistry();
            var allActions = testActionRegistry.GetAll();
            UnityEngine.Debug.Log($"ActionRegistry 总节点数: {allActions.Count}");
            bool hasVFXInActionRegistry = allActions.Any(a => a.Category == "VFX");
            UnityEngine.Debug.Log($"ActionRegistry 中是否有 VFX: {hasVFXInActionRegistry}");
            if (hasVFXInActionRegistry)
            {
                var vfxActions = allActions.Where(a => a.Category == "VFX").ToList();
                foreach (var vfx in vfxActions)
                {
                    UnityEngine.Debug.Log($"  • {vfx.TypeId} → \"{vfx.DisplayName}\"");
                }
            }

            int newCount = _profile?.NodeTypes?.GetAll()?.Count() ?? 0;

            // 诊断日志：输出 NodeTypeRegistry 的内容
            UnityEngine.Debug.Log("=== NodeTypeRegistry 诊断（刷新后）===");
            var allNodeTypes = _profile?.NodeTypes?.GetAll();
            if (allNodeTypes != null)
            {
                var groupedNodeTypes = allNodeTypes
                    .GroupBy(def => string.IsNullOrEmpty(def.Category) ? "未分类" : def.Category)
                    .OrderBy(g => g.Key);
                
                foreach (var group in groupedNodeTypes)
                {
                    UnityEngine.Debug.Log($"<color=yellow>【{group.Key}】</color> ({group.Count()} 个)");
                    foreach (var def in group.OrderBy(d => d.DisplayName))
                    {
                        UnityEngine.Debug.Log($"  • {def.TypeId} → \"{def.DisplayName}\"");
                    }
                }
            }
            UnityEngine.Debug.Log("=== 诊断结束 ===");

            if (newCount != oldCount)
            {
                SBLog.Info(SBLogTags.Blueprint, $"节点类型已刷新：{oldCount} → {newCount}");
                EditorUtility.DisplayDialog("刷新完成", 
                    $"已重新扫描节点类型\n\n旧: {oldCount} 种\n新: {newCount} 种\n\n右键菜单已更新。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("刷新完成", 
                    $"节点类型数量未变化（{newCount} 种）", "确定");
            }

            Repaint();
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
                        string scopedBindingKey = BindingScopeUtility.NormalizeManagerBindingKey(
                            binding.BindingKey,
                            group.SubGraphFrameId);

                        if (!string.IsNullOrEmpty(scopedBindingKey) && binding.BoundObject != null)
                        {
                            _bindingContext.Set(scopedBindingKey, binding.BoundObject);
                        }
                    }
                }
            }

            // 策略 2：对于未恢复的绑定，用 PropertyBag 中的 MarkerId 回退查找
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(_viewModel.Graph, node.Id, prop.Key);
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

            MarkWorkbenchIssuesDirty();
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
            var registry = SceneBlueprintProfile.CreateActionRegistry();
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
                        string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(graph, node.Id, prop.Key);
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

            MarkWorkbenchIssuesDirty();
        }

        /// <summary>从场景绑定存储或 BindingContext 收集绑定数据供导出使用</summary>
        private List<BlueprintExporter.SceneBindingData>? CollectSceneBindingsForExport()
        {
            var registry = SceneBlueprintProfile.CreateActionRegistry();
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
                        string scopedBindingKey = BindingScopeUtility.NormalizeManagerBindingKey(
                            binding.BindingKey,
                            group.SubGraphFrameId);

                        EncodeBindingForExport(
                            binding.BoundObject,
                            binding.BindingType,
                            out var stableObjectId,
                            out var adapterType,
                            out var spatialPayloadJson);

                        list.Add(new BlueprintExporter.SceneBindingData
                        {
                            BindingKey = scopedBindingKey,
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
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(
                        _viewModel.Graph,
                        node.Id,
                        prop.Key);
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
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(graph, node.Id, prop.Key);
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

        private void ShowHelp()
        {
            EditorUtility.DisplayDialog("场景蓝图编辑器 - 操作帮助",
                "基本操作：\n" +
                "• 右键空白区域 → 添加节点\n" +
                "• 左键拖拽端口 → 创建连线\n" +
                "• 中键拖拽 → 平移画布\n" +
                "• 滚轮 → 缩放\n" +
                "• Delete → 删除选中\n" +
                "• Ctrl+Z / Ctrl+Y → 撤销/重做\n" +
                "• F → 聚焦选中节点\n" +
                "• Space → 打开添加节点菜单\n" +
                "\n子蓝图：\n" +
                "• [+ 子蓝图] → 创建新子蓝图（带激活/完成端口）\n" +
                "• 点击子蓝图标题栏左侧折叠按钮 → 折叠/展开\n" +
                "• [全部折叠] / [全部展开] → 快速切换视图\n" +
                "\n场景绑定：\n" +
                "• 选中含 SceneBinding 的节点 → Inspector 显示 ObjectField\n" +
                "• 拖入场景对象 → 完成绑定\n" +
                "• [同步到场景] → 将绑定数据写入 Manager\n" +
                "\n快捷键：\n" +
                "• Alt+B → 打开此窗口",
                "知道了");
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
                        _viewModel.Commands.Execute(new AddNodeCommand(capturedTypeId, capturedPos));
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
                _viewModel.DeleteSelected();
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
        private void OnPortContextMenu(Port port, Vec2 canvasPos)
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

        // ── Scene View 标记创建回调 ──

        /// <summary>
        /// Scene View 中创建标记后的回调。
        /// 在蓝图中自动创建对应 Action 节点，并自动绑定创建的标记。
        /// </summary>
        private void OnMarkerCreated(MarkerCreationResult result)
        {
            if (_viewModel == null || _profile == null) return;

            // 在画布中心位置创建对应 Action 节点
            var graph = _viewModel.Graph;
            var nodeType = graph.Settings.NodeTypes.GetDefinition(result.ActionTypeId);
            if (nodeType == null)
            {
                SBLog.Warn(SBLogTags.Marker, $"未找到 Action 类型: {result.ActionTypeId}");
                return;
            }

            // 计算画布中心位置
            var canvasCenter = new Vec2(
                (-_viewModel.PanOffset.X + position.width / 2f) / _viewModel.ZoomLevel,
                (-_viewModel.PanOffset.Y + position.height / 2f) / _viewModel.ZoomLevel);

            var cmd = new AddNodeCommand(result.ActionTypeId, canvasCenter);
            _viewModel.Commands.Execute(cmd);

            // ── 自动绑定：将刚创建的标记写入 BindingContext + PropertyBag ──
            if (cmd.CreatedNodeId != null && _bindingContext != null)
            {
                var node = graph.FindNode(cmd.CreatedNodeId);
                if (node?.UserData is Core.ActionNodeData data)
                {
                    foreach (var entry in result.CreatedMarkers)
                    {
                        string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(graph, node.Id, entry.BindingKey);

                        // BindingContext 持有 GameObject 引用
                        _bindingContext.Set(scopedBindingKey, entry.MarkerGameObject);
                        // PropertyBag 存储 MarkerId（稳定唯一标识）
                        data.Properties.Set(entry.BindingKey, entry.MarkerId);
                    }

                    SBLog.Info(SBLogTags.Binding,
                        $"已自动绑定 {result.CreatedMarkers.Count} 个标记到节点 {result.ActionDisplayName}");
                }
            }

            SBLog.Info(SBLogTags.Marker, $"已为 {result.ActionDisplayName} 创建蓝图节点，" +
                $"关联 {result.CreatedMarkers.Count} 个标记");

            MarkWorkbenchDataDirty();
            _viewModel.RequestRepaint();
            Repaint();
        }

        /// <summary>
        /// MarkerGroup 创建完成后的回调。
        /// 在蓝图中自动创建对应的 SceneObjectProxy 节点，表示场景中的标记组。
        /// </summary>
        private void OnMarkerGroupCreated(Runtime.Markers.MarkerGroup group)
        {
            if (_viewModel == null || _profile == null || group == null) return;

            var graph = _viewModel.Graph;
            
            // 验证 ProxyNode 类型是否已注册
            string proxyTypeId = Core.SceneObjectProxyTypes.Group;
            var nodeType = graph.Settings.NodeTypes.GetDefinition(proxyTypeId);
            if (nodeType == null)
            {
                SBLog.Warn(SBLogTags.Marker, $"未找到 ProxyNode 类型: {proxyTypeId}");
                return;
            }

            // 计算画布中心位置（与标记创建节点类似）
            var canvasCenter = new Vec2(
                (-_viewModel.PanOffset.X + position.width / 2f) / _viewModel.ZoomLevel,
                (-_viewModel.PanOffset.Y + position.height / 2f) / _viewModel.ZoomLevel);

            // 创建 ProxyNode
            var cmd = new NodeGraph.Commands.AddNodeCommand(proxyTypeId, canvasCenter);
            _viewModel.Commands.Execute(cmd);

            // 设置 ProxyNode 的数据（关联场景对象）
            if (cmd.CreatedNodeId != null)
            {
                var node = graph.FindNode(cmd.CreatedNodeId);
                if (node != null)
                {
                    node.UserData = new Core.SceneObjectProxyData(
                        objectType: Core.MarkerTypeIds.Group,
                        sceneObjectId: group.MarkerId,
                        displayName: group.GetDisplayLabel());

                    SBLog.Info(SBLogTags.Marker, 
                        $"已为 MarkerGroup '{group.GetDisplayLabel()}' 创建 ProxyNode");
                }
            }

            MarkWorkbenchDataDirty();
            _viewModel.RequestRepaint();
            Repaint();
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
            var registry = SceneBlueprintProfile.CreateActionRegistry();

            foreach (var nodeId in _viewModel.Selection.SelectedNodeIds)
            {
                var node = graph.FindNode(nodeId);
                if (node?.UserData is not Core.ActionNodeData data) continue;

                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;

                    string scopedBindingKey = BindingScopeUtility.BuildScopedKeyForNode(graph, node.Id, prop.Key);

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
                                // 回填到 BindingContext 以便后续使用
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

            var registry = SceneBlueprintProfile.CreateActionRegistry();
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
