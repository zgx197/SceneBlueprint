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
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Markers;

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
        private const float SplitterWidth = 4f;
        private float _inspectorWidth = DefaultInspectorWidth;
        private bool _isDraggingSplitter;

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
            InitializeIfNeeded();
        }

        private void OnDisable()
        {
            SceneViewMarkerTool.OnMarkerCreated -= OnMarkerCreated;
            SceneViewMarkerTool.Disable();

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

            // 8. 启用 Scene View 标记工具
            SceneViewMarkerTool.Enable(actionRegistry);
            SceneViewMarkerTool.OnMarkerCreated -= OnMarkerCreated;
            SceneViewMarkerTool.OnMarkerCreated += OnMarkerCreated;

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
            float canvasWidth = position.width - _inspectorWidth - SplitterWidth;

            var graphRect = new Rect(0, contentTop, canvasWidth, contentHeight);
            var splitterRect = new Rect(canvasWidth, contentTop, SplitterWidth, contentHeight);
            var inspectorRect = new Rect(canvasWidth + SplitterWidth, contentTop,
                _inspectorWidth, contentHeight);

            // ── 分栏拖拽 ──
            HandleSplitter(splitterRect, evt);

            // ── 画布区域 ──
            _coordinateHelper.SetGraphAreaRect(graphRect);
            var viewport = new Rect2(0, 0, graphRect.width, graphRect.height);

            // 在 BeginClip 之前更新输入状态
            _input.Update(evt, _coordinateHelper);

            if (evt.type == EventType.Repaint)
            {
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

                // 标记事件已消费，防止 Unity IMGUI 将事件继续传播到
                // Inspector 面板等其他控件，避免焦点抢夺和拖拽序列追踪失效
                evt.Use();
            }

            // ── Inspector 面板 ──
            if (_inspectorPanel.Draw(inspectorRect, _viewModel))
            {
                // 属性被修改，刷新画布摘要显示
                _viewModel.RequestRepaint();
            }

            // 请求重绘
            if (_viewModel.NeedsRepaint)
                Repaint();
        }

        // ── 分栏拖拽 ──

        private void HandleSplitter(Rect splitterRect, Event evt)
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
                        _inspectorWidth = position.width - evt.mousePosition.x - SplitterWidth * 0.5f;
                        _inspectorWidth = Mathf.Clamp(_inspectorWidth, MinInspectorWidth, MaxInspectorWidth);
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

            if (GUILayout.Button("居中", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                CenterView();
            }

            if (GUILayout.Button("帮助", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                ShowHelp();
            }

            GUILayout.EndHorizontal();
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
                Debug.Log($"[蓝图] 已保存: {AssetDatabase.GetAssetPath(_currentAsset)}");
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
                    Debug.Log($"[蓝图] 已创建: {path} (ID: {asset.BlueprintId})");
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

                Debug.Log($"[蓝图] 已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");
                Debug.LogError($"[蓝图] 加载失败: {ex}");
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

                Debug.Log($"[蓝图] 已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                // 加载后运行绑定一致性验证
                RunBindingValidation();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");
                Debug.LogError($"[蓝图] 加载失败: {ex}");
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

            // 从场景 Manager 收集绑定数据
            var sceneBindings = CollectSceneBindingsForExport();

            string bpName = _currentAsset != null ? _currentAsset.BlueprintName : "场景蓝图";
            string? bpId = _currentAsset != null ? _currentAsset.BlueprintId : null;

            var result = BlueprintExporter.Export(
                _viewModel.Graph, registry, sceneBindings,
                blueprintId: bpId, blueprintName: bpName);

            // 输出验证消息
            foreach (var msg in result.Messages)
            {
                switch (msg.Level)
                {
                    case ValidationLevel.Error:
                        UnityEngine.Debug.LogError($"[导出] {msg.Message}");
                        break;
                    case ValidationLevel.Warning:
                        UnityEngine.Debug.LogWarning($"[导出] {msg.Message}");
                        break;
                    default:
                        UnityEngine.Debug.Log($"[导出] {msg.Message}");
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
                        if (!string.IsNullOrEmpty(sb.SceneObjectId)) boundBindings++;
                    }
                }

                UnityEngine.Debug.Log($"[导出] 蓝图已导出到: {path}\n" +
                    $"  行动数: {result.Data.Actions.Length}\n" +
                    $"  过渡数: {result.Data.Transitions.Length}\n" +
                    $"  绑定数: {boundBindings}/{totalBindings}");

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

            _viewModel.RequestRepaint();
            Repaint();

            Debug.Log($"[蓝图] 已创建子蓝图: {title}");
        }

        /// <summary>
        /// 从场景中的 SceneBlueprintManager 恢复绑定数据到 BindingContext。
        /// 在加载蓝图后调用。
        /// </summary>
        private void RestoreBindingsFromScene()
        {
            if (_bindingContext == null || _currentAsset == null || _viewModel == null) return;

            _bindingContext.Clear();

            // 策略 1：从 SceneBlueprintManager 恢复（正式流程）
            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager != null && manager.BlueprintAsset == _currentAsset)
            {
                foreach (var group in manager.BindingGroups)
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

            // 策略 2：对于未恢复的绑定，尝试用 PropertyBag 中的 GameObject 名称回退查找
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    if (_bindingContext.Get(prop.Key) != null) continue; // 已恢复，跳过

                    var objName = data.Properties.Get<string>(prop.Key);
                    if (string.IsNullOrEmpty(objName)) continue;

                    var go = GameObject.Find(objName);
                    if (go != null)
                    {
                        _bindingContext.Set(prop.Key, go);
                    }
                }
            }

            int restored = _bindingContext.BoundCount;
            if (restored > 0)
            {
                Debug.Log($"[蓝图] 已从场景恢复 {restored} 个绑定");
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

            // 1. 先保存蓝图（确保 SO 数据是最新的）
            if (_currentAsset == null)
            {
                EditorUtility.DisplayDialog("同步失败", "请先保存蓝图资产后再同步到场景。", "确定");
                return;
            }

            SaveBlueprint();

            // 2. 查找或创建场景中的 SceneBlueprintManager
            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager == null)
            {
                var go = new GameObject("SceneBlueprintManager");
                manager = go.AddComponent<SceneBlueprintManager>();
                Undo.RegisterCreatedObjectUndo(go, "创建场景蓝图管理器");
                Debug.Log("[蓝图] 已在场景中创建 SceneBlueprintManager");
            }

            // 3. 设置蓝图资产引用
            Undo.RecordObject(manager, "同步蓝图到场景");
            manager.BlueprintAsset = _currentAsset;

            // 4. 按子蓝图分组写入绑定数据
            var graph = _viewModel.Graph;
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            manager.BindingGroups.Clear();

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
                        if (seenKeys.Contains(prop.Key)) continue;
                        seenKeys.Add(prop.Key);

                        var slot = new SceneBindingSlot
                        {
                            BindingKey = prop.Key,
                            BindingType = prop.SceneBindingType ?? Core.BindingType.Transform,
                            DisplayName = prop.DisplayName,
                            SourceActionTypeId = actionData.ActionTypeId,
                            BoundObject = _bindingContext.Get(prop.Key)
                        };
                        group.Bindings.Add(slot);
                    }
                }

                if (group.Bindings.Count > 0)
                    manager.BindingGroups.Add(group);
            }

            // 5. 收集顶层（非子蓝图内）节点的绑定
            var topLevelGroup = CollectTopLevelBindings(graph, registry);
            if (topLevelGroup != null && topLevelGroup.Bindings.Count > 0)
                manager.BindingGroups.Add(topLevelGroup);

            EditorUtility.SetDirty(manager);

            int totalBindings = 0;
            int boundBindings = 0;
            foreach (var g in manager.BindingGroups)
            {
                foreach (var b in g.Bindings)
                {
                    totalBindings++;
                    if (b.IsBound) boundBindings++;
                }
            }

            Debug.Log($"[蓝图] 已同步到场景: " +
                $"子蓝图分组: {manager.BindingGroups.Count}, " +
                $"绑定: {boundBindings}/{totalBindings}");
        }

        /// <summary>从场景 Manager 或 BindingContext 收集绑定数据供导出使用</summary>
        private List<BlueprintExporter.SceneBindingData>? CollectSceneBindingsForExport()
        {
            // 优先从场景 Manager 读取（持久化数据）
            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager != null && manager.BlueprintAsset == _currentAsset && manager.BindingGroups.Count > 0)
            {
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var group in manager.BindingGroups)
                {
                    foreach (var binding in group.Bindings)
                    {
                        list.Add(new BlueprintExporter.SceneBindingData
                        {
                            BindingKey = binding.BindingKey,
                            BindingType = binding.BindingType.ToString(),
                            SceneObjectName = binding.BoundObject != null ? binding.BoundObject.name : "",
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
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var kvp in _bindingContext.All)
                {
                    list.Add(new BlueprintExporter.SceneBindingData
                    {
                        BindingKey = kvp.Key,
                        SceneObjectName = kvp.Value != null ? kvp.Value.name : ""
                    });
                }
                return list.Count > 0 ? list : null;
            }

            return null;
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
                    if (seenKeys.Contains(prop.Key)) continue;
                    seenKeys.Add(prop.Key);

                    var slot = new SceneBindingSlot
                    {
                        BindingKey = prop.Key,
                        BindingType = prop.SceneBindingType ?? Core.BindingType.Transform,
                        DisplayName = prop.DisplayName,
                        SourceActionTypeId = actionData.ActionTypeId,
                        BoundObject = _bindingContext?.Get(prop.Key)
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

            // 按分类分组显示所有节点类型
            var grouped = _profile.NodeTypes.GetAll()
                .GroupBy(def => string.IsNullOrEmpty(def.Category) ? "未分类" : def.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                foreach (var typeDef in group.OrderBy(d => d.DisplayName))
                {
                    string menuPath = $"{group.Key}/{typeDef.DisplayName}";
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
                menu.AddDisabledItem(new GUIContent("(无可用节点类型)"));
            }

            // 如果有多个节点被选中，添加"创建子蓝图"选项
            if (_viewModel.Selection.SelectedNodeIds.Count >= 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图（将选中节点打组）"), false, () =>
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
                    Debug.Log($"[蓝图] 已创建子蓝图: {name}（包含 {selectedIds.Count} 个节点）");
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
                menu.AddItem(new GUIContent(isCollapsed ? "展开子蓝图" : "折叠子蓝图"), false, () =>
                {
                    if (_viewModel == null) return;
                    _viewModel.Commands.Execute(new ToggleSubGraphCollapseCommand(capturedFrameId));
                    _viewModel.RequestRepaint();
                    Repaint();
                });

                menu.AddItem(new GUIContent("解散子蓝图"), false, () =>
                {
                    if (_viewModel == null) return;
                    _viewModel.Commands.Execute(new UngroupSubGraphCommand(capturedFrameId));
                    _viewModel.Selection.ClearSelection();
                    _viewModel.RequestRepaint();
                    Repaint();
                });
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("删除节点"), false, () =>
            {
                if (_viewModel == null) return;
                _viewModel.DeleteSelected();
                Repaint();
            });

            // 如果有多个节点选中，添加打组选项
            if (_viewModel.Selection.SelectedNodeIds.Count > 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图（将选中节点打组）"), false, () =>
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
                    ? $"断开连线: {otherNode.TypeId}.{otherPort!.Name}"
                    : $"断开连线: {otherPortId}";

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
                menu.AddItem(new GUIContent("断开所有连线"), false, () =>
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
        /// 在蓝图中自动创建对应 Action 节点（暂不自动绑定，后续接入）。
        /// </summary>
        private void OnMarkerCreated(MarkerCreationResult result)
        {
            if (_viewModel == null || _profile == null) return;

            // 在画布中心位置创建对应 Action 节点
            var graph = _viewModel.Graph;
            var nodeType = graph.Settings.NodeTypes.GetDefinition(result.ActionTypeId);
            if (nodeType == null)
            {
                Debug.LogWarning($"[SceneMarker] 未找到 Action 类型: {result.ActionTypeId}");
                return;
            }

            // 计算画布中心位置
            var canvasCenter = new Vec2(
                (-_viewModel.PanOffset.X + position.width / 2f) / _viewModel.ZoomLevel,
                (-_viewModel.PanOffset.Y + position.height / 2f) / _viewModel.ZoomLevel);

            var cmd = new AddNodeCommand(result.ActionTypeId, canvasCenter);
            _viewModel.Commands.Execute(cmd);

            Debug.Log($"[SceneMarker] 已为 {result.ActionDisplayName} 创建蓝图节点，" +
                $"关联 {result.CreatedMarkers.Count} 个标记");

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

                    // 策略 1：从 BindingContext 获取 GameObject 引用
                    GameObject? boundObj = _bindingContext?.Get(prop.Key);

                    // 策略 2：BindingContext 为空时，用 PropertyBag 中的名称回退查找
                    if (boundObj == null)
                    {
                        var objName = data.Properties.Get<string>(prop.Key);
                        if (!string.IsNullOrEmpty(objName))
                        {
                            var go = GameObject.Find(objName);
                            if (go != null)
                            {
                                boundObj = go;
                                // 同时回填到 BindingContext 以便后续使用
                                _bindingContext?.Set(prop.Key, go);
                            }
                        }
                    }

                    if (boundObj == null) continue;

                    var marker = boundObj.GetComponent<SceneMarker>();
                    if (marker != null && !string.IsNullOrEmpty(marker.MarkerId))
                        markerIds.Add(marker.MarkerId);
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

            // 查找场景中该 MarkerId 对应的 GameObject
            var selectedMarker = SceneMarkerSelectionBridge.FindMarkerInScene(markerId);
            if (selectedMarker == null) return;

            var selectedGO = selectedMarker.gameObject;
            var selectedName = selectedGO.name;
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            var nodeIds = new List<string>();

            foreach (var node in _viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;

                    // 策略 1：BindingContext 中的 GameObject 引用匹配
                    var boundObj = _bindingContext?.Get(prop.Key);
                    if (boundObj == selectedGO)
                    {
                        nodeIds.Add(node.Id);
                        break;
                    }

                    // 策略 2：PropertyBag 中的 GameObject 名称匹配
                    if (boundObj == null)
                    {
                        var objName = data.Properties.Get<string>(prop.Key);
                        if (!string.IsNullOrEmpty(objName) && objName == selectedName)
                        {
                            nodeIds.Add(node.Id);
                            break;
                        }
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
            Debug.Log($"[联动] OnUnitySelectionChanged: activeGameObject={go?.name ?? "null"}");

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
                Debug.Log($"[联动]   是 SceneMarker: {marker.MarkerId}");
                SceneMarkerSelectionBridge.NotifySceneMarkerSelected(marker.MarkerId);
            }
            else
            {
                // 选中了非标记对象 → 清除蓝图中的联动选中
                Debug.Log($"[联动]   非 SceneMarker，清除蓝图选中");
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
