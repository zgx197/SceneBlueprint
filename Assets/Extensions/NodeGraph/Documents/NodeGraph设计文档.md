# NodeGraph — 通用节点图编辑器框架设计文档

> **版本**: v2.5  
> **日期**: 2026-02-11  
> **目标**: 设计一个引擎无关、通用、健壮、灵活的节点图编辑器框架  
> **.NET 目标**: .NET Standard 2.1  
> **项目组织**: Unity asmdef（纯 C# 核心 + 引擎适配层）  
> **架构模式**: GraphFrame 渲染描述 + BlueprintProfile 蓝图配置

---

## 1. 概述

### 1.1 定位

NodeGraph 是一个**跨引擎**的通用节点图编辑器框架，提供节点、端口、连线、交互、渲染的完整抽象。业务层（如刷怪蓝图编辑器、技能编辑器）通过注册节点类型和实现内容渲染接口来构建自己的图编辑器。

### 1.2 设计目标

| 目标 | 说明 |
|------|------|
| **通用性** | 支持 DAG、有向图、无向图；支持控制流和数据流 |
| **跨引擎** | 核心逻辑零引擎依赖，通过适配层接入 Unity / Godot / Dear ImGui |
| **健壮性** | 类型安全（Nullable Reference Types）、命令模式（Undo/Redo）、连接验证 |
| **灵活性** | 动态端口、可扩展连接策略、自定义节点内容渲染 |
| **可复用** | 一套框架支撑：刷怪蓝图、技能编辑器、对话树、状态机、AI行为树等 |

### 1.3 目标应用场景

| 场景 | 拓扑 | 端口类型 | 连线数据 | 子图 |
|------|------|----------|---------|------|
| 刷怪蓝图 | DAG | Control | ConditionDescriptor（v2.2） | 子Plan（SubGraphFrame） |
| 技能编辑器 | DAG | Control + Data | 无 | 子技能（SubGraphFrame） |
| 对话树 | 有向图（可环） | Control | 对话选项/ConditionDescriptor | 子对话（SubGraphFrame） |
| AI行为树 | DAG（树） | Control | 无 | 子树（SubGraphFrame） |
| 状态机 | 有向图（可环） | Control | ConditionDescriptor（v2.2） | 嵌套状态机（SubGraphFrame） |

### 1.4 不包含的内容

- 运行时执行引擎（由业务层自行实现）
- 具体业务节点的逻辑（只提供注册和渲染接口）
- 3D 渲染能力（仅 2D 图元绘制）

---

## 2. 架构分层

### 2.0 架构总览（v2.0 GraphFrame 模式）

```
┌───────────────────────────────────────────────────┐
│  业务层（SpawnSystem / SkillEditor / ...）          │ ← 提供 BlueprintProfile
│  注册节点类型、实现 INodeContentRenderer            │
│  提供 IGraphFrameBuilder（可选，定制视觉风格）       │
├───────────────────────────────────────────────────┤
│  引擎渲染层（各引擎完全独立实现）                     │ ← 消费 GraphFrame
│  Unity: UnityGraphRenderer (纯矢量 IMGUI/Handles)   │
│  Godot: GodotGraphRenderer (StyleBoxFlat/CanvasItem)│
│  ImGui: ImGuiGraphRenderer (ImDrawList)             │
├───────────────────────────────────────────────────┤
│  引擎适配层                                         │ ← 输入/编辑/持久化
│  实现 IPlatformInput / IEditContext / IGraphPersistence │
├───────────────────────────────────────────────────┤
│  NodeGraph 核心（纯 C#，零引擎依赖）                  │ ← 输出 GraphFrame
│  Core / Commands / View / Layout / Serialization    │
│  GraphViewModel.BuildFrame() → GraphFrame           │
└───────────────────────────────────────────────────┘
```

**核心数据流**：
```
引擎宿主窗口 OnGUI / _Draw
  │
  ├── input.Update(engineEvent)
  ├── viewModel.ProcessInput(input)       ← 交互处理（纯 C#）
  ├── viewModel.Update(deltaTime)         ← 状态更新（纯 C#）
  ├── GraphFrame frame = viewModel.BuildFrame(viewport)  ← 构建渲染描述（纯 C#）
  └── engineRenderer.Render(frame)        ← 引擎原生绘制（引擎专有）
```

### 2.1 层级职责

| 层 | 输入 | 输出 | 知道什么 | 不知道什么 |
|----|------|------|----------|-----------|
| **NodeGraph 核心** | Graph + 交互事件 | GraphFrame（渲染描述） | 节点/端口/连线/布局/状态 | 任何引擎 API |
| **引擎渲染层** | GraphFrame | 屏幕像素 | 引擎最优绘制技术 | 业务逻辑、图算法 |
| **引擎适配层** | 引擎原生事件 | IPlatformInput / IEditContext | 引擎输入/控件系统 | 图数据结构 |
| **业务层** | 业务数据 | BlueprintProfile + ContentRenderers | SpawnTask、技能配置 | 图怎么画、交互怎么处理 |

### 2.2 GraphFrame 渲染描述模式

**设计动机**：v1.x 通过 `IDrawContext` 将所有引擎退化到最基础图元（矩形、线、圆），无法利用引擎原生能力（Unity 纯矢量 Handles 绘制、Godot StyleBoxFlat 原生圆角/阴影等）。

**解决方案**：纯 C# 层不再直接绘制，而是输出结构化的 **GraphFrame**（渲染描述），引擎层自由选择最优技术渲染：

```csharp
// 纯 C# 层输出
public class GraphFrame
{
    public BackgroundFrame Background { get; set; }
    public List<NodeFrame> Nodes { get; }
    public List<EdgeFrame> Edges { get; }
    public List<OverlayFrame> Overlays { get; }
    public MiniMapFrame? MiniMap { get; set; }
}

public class NodeFrame
{
    public string NodeId { get; set; }
    public Rect2 Bounds { get; set; }              // 节点矩形
    public Color4 TitleColor { get; set; }         // 标题栏颜色
    public string TitleText { get; set; }          // 标题文字
    public bool Selected { get; set; }
    public bool IsPrimary { get; set; }
    public List<PortFrame> Ports { get; }
    public NodeContentInfo? Content { get; set; }  // 内容区信息
}

public class PortFrame
{
    public string PortId { get; set; }
    public Vec2 Position { get; set; }
    public Color4 Color { get; set; }
    public bool Connected { get; set; }
    public string Name { get; set; }
    public PortDirection Direction { get; set; }
    public int ConnectedEdgeCount { get; set; }   // 已连接的边数
    /// <summary>Multiple 端口的总槽位数（含已连接 + 空位 + "+"，由 FrameBuilder 计算）</summary>
    public int TotalSlots { get; set; }
}

public class EdgeFrame
{
    public string EdgeId { get; set; }
    public Vec2 Start { get; set; }
    public Vec2 End { get; set; }
    public Vec2 TangentA { get; set; }
    public Vec2 TangentB { get; set; }
    public Color4 Color { get; set; }
    public float Width { get; set; }
    public bool Selected { get; set; }
}
```

**引擎层各自最优实现**：

| GraphFrame 元素 | Unity 渲染技术 | Godot 渲染技术 | Dear ImGui |
|----------------|---------------|---------------|------------|
| NodeFrame | 矢量圆角矩形（DrawRect+DrawSolidDisc） | draw_style_box(StyleBoxFlat) | ImDrawList.AddRectRounded |
| 节点阴影 | 矢量圆角矩形多层叠加（指数衰减 alpha） | StyleBoxFlat.shadow_* | AddRectFilled + offset |
| EdgeFrame | Handles.DrawBezier | draw_polyline | AddBezierCubic |
| PortFrame | Handles.DrawSolidDisc | draw_circle | AddCircleFilled |
| 选中发光 | 矢量圆角边框多层叠加 | StyleBoxFlat.border_color | AddRect + alpha layers |

### 2.3 BlueprintProfile 蓝图配置

不同蓝图类型（刷怪、技能、行为树、状态机）通过 `BlueprintProfile` 定制行为：

```csharp
public class BlueprintProfile
{
    public IGraphFrameBuilder FrameBuilder { get; set; }    // 渲染描述构建器
    public NodeVisualTheme Theme { get; set; }              // 视觉主题
    public GraphTopologyPolicy Topology { get; set; }       // 图拓扑
    public LayoutDirection DefaultLayoutDirection { get; set; } // 默认布局方向
    public NodeTypeRegistry NodeTypes { get; set; }         // 节点类型
    public Dictionary<string, INodeContentRenderer> ContentRenderers { get; }
    public IEdgeLabelRenderer? EdgeLabelRenderer { get; set; }
    public IConnectionPolicy? ConnectionPolicy { get; set; }
    public BlueprintFeatureFlags Features { get; set; }     // 功能开关
}

public enum LayoutDirection { Horizontal, Vertical }

[Flags]
public enum BlueprintFeatureFlags
{
    None = 0,
    MiniMap = 1,
    Search = 2,
    AutoLayout = 4,
    SubGraph = 8,
    DebugOverlay = 16,
    All = MiniMap | Search | AutoLayout | SubGraph
}
```

**蓝图类型差异由 `IGraphFrameBuilder` 在纯 C# 层解决**：

```csharp
public interface IGraphFrameBuilder
{
    /// <summary>计算节点尺寸</summary>
    Vec2 ComputeNodeSize(Node node, GraphViewModel viewModel);
    
    /// <summary>计算端口在画布中的位置</summary>
    Vec2 GetPortPosition(Port port, Node node, Rect2 nodeBounds,
        NodeVisualTheme theme, GraphViewModel viewModel);
    
    /// <summary>
    /// 获取连线在目标端口上的具体槽位位置。
    /// Multiple Input 端口按边顺序分配槽位，非 Multiple 端口返回端口中心。
    /// </summary>
    Vec2 GetEdgeTargetPosition(Edge edge, Port targetPort, Node targetNode,
        Rect2 bounds, NodeVisualTheme theme, GraphViewModel viewModel);
    
    /// <summary>
    /// 计算端口占用的视觉槽位数（仅 Input+Multiple 需要多槽位，其余一律 1）。
    /// 公式：Max(用户目标槽位数, edgeCount + 1, 2)
    /// </summary>
    int GetPortSlotCount(Port port, GraphViewModel viewModel);
    
    /// <summary>构建完整的渲染帧</summary>
    GraphFrame BuildFrame(GraphViewModel viewModel, Rect2 viewport);
}
```

| 蓝图类型 | FrameBuilder | 端口方向 | 连线风格 | 节点形状 |
|----------|-------------|---------|---------|---------|
| 刷怪蓝图 | DefaultFrameBuilder | 左进右出 | 水平贝塞尔 | 矩形 |
| 技能蓝图 | DefaultFrameBuilder | 左进右出 | 水平贝塞尔 | 矩形 |
| 行为树 | BehaviorTreeFrameBuilder | 上进下出 | 垂直折线 | 按类型变形 |
| 状态机 | StateMachineFrameBuilder | 四周均可 | 贝塞尔+箭头 | 圆角椭圆 |

---

## 3. 核心数据模型（NodeGraph.Core）

### 3.0 ID 生成策略

所有图元素（节点、端口、连线、分组、注释）使用完整 **GUID** 作为唯一标识符，确保多人协作时零冲突：

```csharp
public static class IdGenerator
{
    public static string NewId() => Guid.NewGuid().ToString("D");
    // 格式示例: "3a7f2b1c-e4d8-4a5f-b9c2-1d3e5f7a8b0c"
}
```

**选择 GUID 的理由**：
- 多人离线编辑同一图时不会产生 ID 冲突
- 复制粘贴、跨图导入时无需重新映射 ID
- Git 合并时冲突概率极低

**兆底**：`Graph.AddNode` 内部做重复检测，万一冲突则重新生成：

```csharp
public Node AddNode(string typeId, Vec2 position)
{
    string id;
    do { id = IdGenerator.NewId(); } while (_nodeMap.ContainsKey(id));
    // ...
}
```

### 3.1 Graph

```csharp
public class Graph
{
    public string Id { get; }
    public GraphSettings Settings { get; }
    public GraphEvents Events { get; }
    
    // 元素集合
    public IReadOnlyList<Node> Nodes { get; }
    public IReadOnlyList<Edge> Edges { get; }
    public IReadOnlyList<NodeGroup> Groups { get; }
    public IReadOnlyList<GraphComment> Comments { get; }
    
    // 节点操作
    public Node AddNode(string typeId, Vec2 position);
    public void RemoveNode(string nodeId);
    public Node? FindNode(string nodeId);
    
    // 连线操作
    public Edge? Connect(string sourcePortId, string targetPortId);
    public void Disconnect(string edgeId);
    
    // 查询
    public IEnumerable<Edge> GetEdgesForNode(string nodeId);
    public IEnumerable<Edge> GetEdgesForPort(string portId);
    public IEnumerable<Node> GetSuccessors(string nodeId);
    public IEnumerable<Node> GetPredecessors(string nodeId);
    
    // 装饰元素
    public NodeGroup CreateGroup(string title, IEnumerable<string> nodeIds);
    public void RemoveGroup(string groupId);
    public GraphComment CreateComment(string text, Vec2 position);
    public void RemoveComment(string commentId);
}
```

### 3.2 GraphSettings

```csharp
public class GraphSettings
{
    /// <summary>图拓扑策略</summary>
    public GraphTopologyPolicy Topology { get; set; } = GraphTopologyPolicy.DAG;
    
    /// <summary>连接策略（可替换）</summary>
    public IConnectionPolicy ConnectionPolicy { get; set; }
    
    /// <summary>类型兼容性注册表</summary>
    public TypeCompatibilityRegistry TypeCompatibility { get; }
    
    /// <summary>节点类型注册表</summary>
    public NodeTypeRegistry NodeTypes { get; }
}

public enum GraphTopologyPolicy
{
    DAG,            // 有向无环图（刷怪蓝图、技能编辑器）
    DirectedGraph,  // 有向图，允许环（状态机、对话树）
    Undirected      // 无向图（关系图）
}
```

### 3.3 Node

```csharp
public class Node
{
    public string Id { get; }                    // GUID
    public string TypeId { get; }               // 节点类型标识
    public Vec2 Position { get; set; }           // 画布坐标
    public Vec2 Size { get; set; }               // 节点尺寸
    public NodeDisplayMode DisplayMode { get; set; } = NodeDisplayMode.Expanded;
    public NodeState State { get; set; } = NodeState.Normal;
    public INodeData? UserData { get; set; }     // 业务层附加数据
    
    // 端口
    public IReadOnlyList<Port> Ports { get; }
    public bool AllowDynamicPorts { get; set; }
    public Port AddPort(PortDefinition definition);
    public void RemovePort(string portId);
    public Port? FindPort(string portId);
    public IEnumerable<Port> GetInputPorts();
    public IEnumerable<Port> GetOutputPorts();
    
    // 端口事件
    public event Action<Port>? OnPortAdded;
    public event Action<Port>? OnPortRemoved;
}

public enum NodeDisplayMode
{
    Expanded,    // 完整：标题 + 端口 + 内容/编辑器
    Collapsed,   // 摘要：标题 + 端口 + 一行摘要
    Minimized    // 最小：单行，仅标题和端口
}

public enum NodeState
{
    Normal,
    Selected,
    Highlighted,
    Error,
    Running      // 用于调试/预览执行状态
}
```

### 3.4 Port

```csharp
public class Port
{
    public string Id { get; }
    public string NodeId { get; }                // 所属节点
    public string Name { get; set; }             // 显示名称
    public PortDirection Direction { get; }       // Input / Output
    public PortKind Kind { get; }                 // Control / Data
    public string DataType { get; }               // 数据类型标识
    public PortCapacity Capacity { get; }         // Single / Multiple
    public int SortOrder { get; set; }            // 端口排序（行为树中子节点顺序）
}

public enum PortDirection { Input, Output }
public enum PortKind { Control, Data }
public enum PortCapacity
{
    Single,     // 只能连一条线
    Multiple    // 可以连多条线
}
```

### 3.5 Edge

```csharp
public class Edge
{
    public string Id { get; }
    public string SourcePortId { get; }
    public string TargetPortId { get; }
    public IEdgeData? UserData { get; set; }     // 业务层附加数据
}
```

### 3.6 业务数据接口

```csharp
/// <summary>节点业务数据标记接口</summary>
public interface INodeData { }

/// <summary>连线业务数据标记接口</summary>
public interface IEdgeData { }
```

业务层实现示例：

```csharp
// 刷怪蓝图
public class SpawnTaskData : INodeData
{
    public string TemplateName;
    public int WaveCount;
    public float Interval;
}

public class TransitionEdgeData : IEdgeData
{
    /// <summary>条件描述（v2.2+），null 表示无条件（Immediate）</summary>
    public ConditionDescriptor? Condition;
}
```

---

## 4. 节点类型系统

### 4.1 NodeTypeDefinition

```csharp
public class NodeTypeDefinition
{
    public string TypeId { get; }                  // 唯一标识
    public string DisplayName { get; }             // 显示名
    public string Category { get; }                // 分类路径（如 "Spawn/Task"）
    public Color4 Color { get; }                   // 节点颜色
    public PortDefinition[] DefaultPorts { get; }  // 默认端口模板
    public bool AllowMultiple { get; }             // 图中是否允许多个实例
    public bool AllowDynamicPorts { get; }         // 是否允许动态增减端口
    
    public Func<INodeData>? CreateDefaultData;     // 创建默认业务数据
    public INodeContentRenderer? ContentRenderer;  // 内容渲染器
}

public class PortDefinition
{
    public string Name { get; }
    public PortDirection Direction { get; }
    public PortKind Kind { get; }
    public string DataType { get; }
    public PortCapacity Capacity { get; }
}
```

### 4.2 NodeTypeRegistry

```csharp
public class NodeTypeRegistry
{
    public void Register(NodeTypeDefinition definition);
    public void Unregister(string typeId);
    public NodeTypeDefinition? GetDefinition(string typeId);
    public IEnumerable<NodeTypeDefinition> GetAll();
    public IEnumerable<NodeTypeDefinition> Search(string keyword);
    public IEnumerable<string> GetCategories();
}
```

---

## 5. 端口类型兼容性

### 5.1 TypeCompatibilityRegistry

```csharp
public class TypeCompatibilityRegistry
{
    /// <summary>注册隐式转换：fromType 可以连到 toType</summary>
    public void RegisterImplicitConversion(string fromType, string toType);
    
    /// <summary>查询两个类型是否兼容</summary>
    public bool IsCompatible(string sourceType, string targetType);
    
    /// <summary>获取指定类型可连接的所有类型</summary>
    public IEnumerable<string> GetCompatibleTypes(string type);
}
```

### 5.2 内置规则

- `"any"` 类型与任何类型兼容（通配符）
- `"exec"` 类型只能连 `"exec"`（控制流不允许隐式转换）
- 相同类型总是兼容的

### 5.3 使用示例

```csharp
var types = new TypeCompatibilityRegistry();
types.RegisterImplicitConversion("int", "float");
types.RegisterImplicitConversion("float", "double");
types.RegisterImplicitConversion("entity", "any");
```

---

## 6. 连接策略

### 6.1 IConnectionPolicy

```csharp
public interface IConnectionPolicy
{
    ConnectionResult CanConnect(Graph graph, Port source, Port target);
}

public enum ConnectionResult
{
    Success,
    SameNode,               // 不能连自己
    SameDirection,          // 两个都是 Input 或都是 Output
    KindMismatch,           // Control 连 Data
    DataTypeMismatch,       // 类型不兼容
    CapacityExceeded,       // 端口已满
    CycleDetected,          // 会形成环（仅 DAG 模式）
    CustomRejected          // 业务层自定义拒绝
}
```

### 6.2 DefaultConnectionPolicy

内置默认策略，按顺序检查：

1. 同一节点 → `SameNode`
2. 同方向 → `SameDirection`
3. Kind 不匹配 → `KindMismatch`
4. Data 类型不兼容 → `DataTypeMismatch`（使用 TypeCompatibilityRegistry）
5. 容量超限 → `CapacityExceeded`
6. DAG 模式下环检测 → `CycleDetected`
7. 全部通过 → `Success`

### 6.3 业务层扩展

业务层可以继承 `DefaultConnectionPolicy` 添加自定义规则：

```csharp
public class SpawnPlanConnectionPolicy : DefaultConnectionPolicy
{
    public override ConnectionResult CanConnect(Graph graph, Port source, Port target)
    {
        var baseResult = base.CanConnect(graph, source, target);
        if (baseResult != ConnectionResult.Success) return baseResult;
        
        // 自定义规则：入口节点不能有入边
        var targetNode = graph.FindNode(target.NodeId);
        if (targetNode?.TypeId == "PlanEntry")
            return ConnectionResult.CustomRejected;
            
        return ConnectionResult.Success;
    }
}
```

---

## 7. 图算法（GraphAlgorithms）

```csharp
public static class GraphAlgorithms
{
    /// <summary>检测添加边后是否会形成环</summary>
    public static bool WouldCreateCycle(Graph graph, string fromNodeId, string toNodeId);
    
    /// <summary>拓扑排序（仅 DAG）</summary>
    public static List<Node>? TopologicalSort(Graph graph);
    
    /// <summary>获取所有根节点（无入边的节点）</summary>
    public static IEnumerable<Node> GetRootNodes(Graph graph);
    
    /// <summary>获取所有叶子节点（无出边的节点）</summary>
    public static IEnumerable<Node> GetLeafNodes(Graph graph);
    
    /// <summary>获取从指定节点可达的所有节点</summary>
    public static HashSet<string> GetReachableNodes(Graph graph, string startNodeId);
    
    /// <summary>检测图中是否存在环</summary>
    public static bool HasCycle(Graph graph);
    
    /// <summary>获取图中所有连通分量</summary>
    public static List<HashSet<string>> GetConnectedComponents(Graph graph);
}
```

---

## 8. 装饰元素（v2.3 GraphContainer 层次结构）

### 8.1 类型层次

```
GraphDecoration (abstract)      ← 画布上的非拓扑元素基类
├── GraphContainer (abstract)   ← 包含节点的容器基类
│   ├── NodeGroup                ← 纯视觉分组（Color）
│   └── SubGraphFrame            ← 增强容器（边界端口、折叠、来源追溯）
└── GraphComment                 ← 文本注释
```

**设计理念**：
- **GraphDecoration** = 画布上有位置和大小、但不参与图拓扑（连接逻辑）的元素
- **GraphContainer** = 语义明确的"节点容器"，管理一组节点的归属关系
- **GraphComment** = 纯视觉标注，不包含节点

### 8.2 GraphDecoration（基类）

```csharp
public abstract class GraphDecoration
{
    public string Id { get; }
    
    /// <summary>画布上的边界矩形（位置 + 尺寸）</summary>
    public Rect2 Bounds { get; set; }
}
```

> **v2.3 变更**：将 `Position + Size`（两个 Vec2）统一为 `Rect2 Bounds`，语义更直接。

### 8.3 GraphContainer（容器基类）

```csharp
/// <summary>
/// 节点容器基类。管理一组节点的归属关系。
/// NodeGroup 和 SubGraphFrame 共享此公共契约。
/// </summary>
public abstract class GraphContainer : GraphDecoration
{
    /// <summary>容器标题</summary>
    public string Title { get; set; }
    
    /// <summary>包含的节点 ID 集合（HashSet 保证 O(1) 查找）</summary>
    public HashSet<string> ContainedNodeIds { get; }
    
    /// <summary>根据包含的节点自动计算边界</summary>
    public void AutoFit(Graph graph, float padding = 20f);
}
```

> **v2.3 变更**：`ContainedNodeIds` 从 `List<string>` 升级为 `HashSet<string>`。
> 容器内节点查找在 FrameBuilder 中频繁调用（折叠判断），O(1) vs O(n) 差异显著。

### 8.4 NodeGroup（分组框）

```csharp
public class NodeGroup : GraphContainer
{
    /// <summary>分组颜色</summary>
    public Color4 Color { get; set; }
}
```

**交互行为**：
- 拖动 Group 标题栏 → 整个 Group + 内部节点一起移动
- 将节点拖入 Group 边界 → 自动加入 Group
- 将节点拖出 Group 边界 → 自动移出 Group
- Group 大小可手动调整（拖拽边缘），也可 AutoFit

### 8.5 GraphComment（注释块）

```csharp
public class GraphComment : GraphDecoration
{
    public string Text { get; set; }
    public float FontSize { get; set; } = 14f;
    public Color4 TextColor { get; set; }
    public Color4 BackgroundColor { get; set; }
}
```

### 8.6 Graph 中的管理 API

```csharp
// 分别存储，类型安全访问
public IReadOnlyList<NodeGroup> Groups { get; }
public IReadOnlyList<SubGraphFrame> SubGraphFrames { get; }
public IReadOnlyList<GraphComment> Comments { get; }

// 统一的容器迭代（FrameBuilder 用于生成 DecorationFrame）
public IEnumerable<GraphContainer> AllContainers { get; }
```

### 8.7 绘制层级

> 详见 **9.4 渲染层级**（含 SubGraphFrame 层级定义）。

---

## 9. 子图（SubGraph）— v2.3 扁平化内联框 + 代表节点方案

### 9.1 设计理念

子图采用 **扁平化内联框** 方案，而非"双击导航进入"方案：

- **所有节点都在同一张 Graph 中**，SubGraphFrame 只是视觉容器
- **拷贝模式**：从子图资产实例化时做深拷贝，不影响原始资产
- **不需要递归**：一次 BuildFrame、一次渲染、一个 Undo 栈
- **可折叠**：展开时显示内部节点，折叠时表现为紧凑节点
- **代表节点**：每个 SubGraphFrame 拥有一个真实 Node 作为边界端口载体

```
展开状态：
┌─────────────────────────────────────────┐
│ 📂 子Plan: 精英怪阶段              [▼] │  ← DecorationFrame 背景
│                                         │
│ ● In   [Spawn精英A] → [等待] → [强化]  │  ← 边界端口渲染到框边缘
│                                  Out ●  │
└─────────────────────────────────────────┘
  RepresentativeNode 自身隐藏，
  但其端口由 FrameBuilder 重新定位到框边缘

折叠状态：
┌──────────────────────────────┐
│ 📁 子Plan: 精英怪阶段   [▶] │  ← RepresentativeNode 正常渲染
│ ● In                  Out ● │  ← 边界端口就是 RepresentativeNode 的端口
└──────────────────────────────┘
```

### 9.2 SubGraphFrame（继承 GraphContainer）

```csharp
/// <summary>
/// 子图框。继承 GraphContainer，在节点容器能力之上增加：
/// - 代表节点（RepresentativeNode）：承载边界端口，折叠时作为可连线的普通节点
/// - 折叠/展开状态
/// - 来源资产追溯
/// </summary>
public class SubGraphFrame : GraphContainer
{
    /// <summary>折叠状态</summary>
    public bool IsCollapsed { get; set; }
    
    /// <summary>
    /// 代表节点 ID。指向 Graph 中的一个真实 Node，该节点拥有所有边界端口。
    /// 折叠时：RepresentativeNode 正常渲染为紧凑节点（标题 + 边界端口）
    /// 展开时：RepresentativeNode 自身隐藏，其端口由 FrameBuilder 重新定位到框边缘
    /// </summary>
    public string RepresentativeNodeId { get; }
    
    /// <summary>来源资产引用（可选，用于追溯拷贝来源）</summary>
    public string? SourceAssetId { get; set; }
}
```

### 9.3 代表节点（RepresentativeNode）设计

**核心洞察**：折叠的 SubGraphFrame 在行为上与普通 Node 完全一致——有端口、可连线、可选择、可拖拽。
因此让 SubGraphFrame 拥有一个**真实的 Node**，通过复用而非重写来获得这些能力。

```
SubGraphFrame
├── RepresentativeNode（真实 Node，在 Graph.Nodes 中）
│   ├── Input 端口 = SubGraphFrame 的输入边界端口
│   └── Output 端口 = SubGraphFrame 的输出边界端口
├── ContainedNodeIds（继承自 GraphContainer）
└── IsCollapsed / SourceAssetId
```

**RepresentativeNode 的特征**：
- 使用特殊的 NodeTypeId（如 `"__SubGraphBoundary"`），由 FrameBuilder 识别
- 折叠时：FrameBuilder 为其生成普通 NodeFrame（显示 SubGraphFrame 标题 + 所有端口）
- 展开时：FrameBuilder 不为其生成独立的 NodeFrame，而是将其端口渲染到 DecorationFrame 的框边缘

**为什么不修改 Port 模型？**
- Port.NodeId 始终指向真实 Node（RepresentativeNode），所有端口查找、连线逻辑零改动
- Edge 连接到边界端口 = Edge 连接到 RepresentativeNode 的端口，现有 Edge 系统完全兼容
- FrameBuilder 只需在渲染时调整端口的视觉位置，不影响数据层

### 9.4 渲染架构

#### 9.4.1 DecorationFrame（渲染帧新增类型）

```csharp
/// <summary>
/// 装饰层渲染帧。描述 NodeGroup / SubGraphFrame / GraphComment 的视觉信息。
/// </summary>
public class DecorationFrame
{
    public DecorationKind Kind { get; set; }     // Group / SubGraph / Comment
    public string Id { get; set; }               // 对应数据模型的 ID
    public Rect2 Bounds { get; set; }
    public string? Title { get; set; }
    public Color4 BackgroundColor { get; set; }
    public Color4 BorderColor { get; set; }
    public float TitleBarHeight { get; set; }
    
    // SubGraph 专用
    public bool ShowCollapseButton { get; set; }
    public bool IsCollapsed { get; set; }
    
    // SubGraph 展开时的边界端口（由 FrameBuilder 重新定位到框边缘）
    public List<PortFrame>? BoundaryPorts { get; set; }
    
    // Comment 专用
    public string? Text { get; set; }
    public float FontSize { get; set; }
    public Color4 TextColor { get; set; }
}

public enum DecorationKind
{
    Group,       // NodeGroup
    SubGraph,    // SubGraphFrame
    Comment      // GraphComment
}
```

#### 9.4.2 GraphFrame 扩展

```csharp
public class GraphFrame
{
    public BackgroundFrame Background { get; set; }
    public List<DecorationFrame> Decorations { get; }   // ← 新增
    public List<EdgeFrame> Edges { get; }
    public List<NodeFrame> Nodes { get; }
    public List<OverlayFrame> Overlays { get; }
    public MiniMapFrame? MiniMap { get; set; }
}
```

#### 9.4.3 渲染层级

```
Layer 0: Background（背景网格）
Layer 1: Decorations — Comment（注释，最底层装饰）
Layer 2: Decorations — Group / SubGraph（分组框/子图框，在节点下方）
Layer 3: Edge（连线）
Layer 4: Node（节点，含展开状态下的子图框内节点）
Layer 5: Overlays（拖拽连线、框选等临时最上层）
Layer 6: MiniMap / UI（小地图、搜索框等）
```

#### 9.4.4 FrameBuilder 的渲染决策

```
BuildFrame() 流程中对 SubGraphFrame 的处理：

1. 遍历 Graph.AllContainers → 为每个容器生成 DecorationFrame
2. 遍历 Graph.Nodes：
   a. 如果节点是某个折叠 SubGraphFrame 的 ContainedNodeId → 跳过（不生成 NodeFrame）
   b. 如果节点是某个折叠 SubGraphFrame 的 RepresentativeNode → 正常生成 NodeFrame（显示为紧凑节点）
   c. 如果节点是某个展开 SubGraphFrame 的 RepresentativeNode → 跳过（端口已在 DecorationFrame 中）
   d. 其他节点 → 正常生成 NodeFrame
3. 遍历 Graph.Edges：
   a. 如果边两端的端口所属节点都在折叠框内 → 跳过（内部边不渲染）
   b. 其他 → 正常生成 EdgeFrame（边界端口相关的边照常渲染，位置由 PortFrame 决定）
```

### 9.5 关键行为

| 行为 | 说明 |
|------|------|
| **实例化** | 从子图资产深拷贝节点和边到父 Graph，创建 RepresentativeNode + SubGraphFrame 包裹 |
| **折叠** | ContainedNodeIds 中的节点不参与渲染和命中检测，RepresentativeNode 渲染为紧凑节点 |
| **展开** | 框内节点正常渲染和编辑，RepresentativeNode 隐藏，其端口渲染到框边缘 |
| **连线** | 框外 → 边界端口（RepresentativeNode 的端口），框内 → 内部节点端口或边界端口 |
| **编辑** | 展开时框内节点可直接编辑（选择、拖拽、连线等） |
| **移动** | 拖拽框标题可移动整个框（ContainedNodeIds + RepresentativeNode 一起平移） |
| **Undo** | 与父图共享同一个 CommandHistory |

### 9.6 与旧方案的对比

| 维度 | 旧方案（导航进入） | 新方案（内联框 + 代表节点） |
|------|-------------------|--------------------------|
| 展示 | 双击进入独立视图 | 在当前画布展开/折叠 |
| 数据 | Graph 嵌套 Graph | 所有节点在同一 Graph |
| 边界端口 | 无（子图无对外端口） | RepresentativeNode 的端口，零侵入 Port 模型 |
| 渲染 | 递归 FrameBuilder + 递归渲染 | 一次 BuildFrame + 一次渲染 |
| 命中检测 | 多层检测 | 统一检测 |
| Undo | 可能需要多栈 | 单一 CommandHistory |
| 上下文 | 进入子图后丢失父图上下文 | 始终可见父图和子图关系 |

> **旧代码已清理**：`SubGraphNode.cs`、`_graphStack`、`EnterSubGraph()`、`ExitSubGraph()` 等旧导航代码
> 已在 Phase 10 后的代码清理中移除。

---

## 9b. 结构化条件描述系统（v2.2）

### 9b.1 设计原则

- **条件只在 Edge 上**：框架层通过 `IEdgeData` 承载 `ConditionDescriptor`
- **Node 无框架级条件**：BT Decorator 等"条件节点"由业务层自定义节点类型实现
- **框架层只管组合结构**：AND/OR/NOT 组合，不知道具体条件语义
- **业务层定义具体语义**：通过 `IConditionTypeRegistry` 注册条件类型及参数

### 9b.2 ConditionDescriptor（条件描述树）

```csharp
/// <summary>
/// 条件描述基类。可序列化的条件树结构，框架层只管组合，不知道业务语义。
/// </summary>
[Serializable]
public abstract class ConditionDescriptor { }

/// <summary>叶子条件。具体语义由业务层通过 TypeId 定义。</summary>
[Serializable]
public class LeafCondition : ConditionDescriptor
{
    /// <summary>条件类型标识（如 "Delay", "CompareInt", "HasTarget"）</summary>
    public string TypeId { get; set; }
    
    /// <summary>参数键值对（由业务层根据 TypeId 解释）</summary>
    public Dictionary<string, string> Parameters { get; set; }
}

/// <summary>逻辑与组合</summary>
[Serializable]
public class AndCondition : ConditionDescriptor
{
    public List<ConditionDescriptor> Children { get; set; }
}

/// <summary>逻辑或组合</summary>
[Serializable]
public class OrCondition : ConditionDescriptor
{
    public List<ConditionDescriptor> Children { get; set; }
}

/// <summary>逻辑非</summary>
[Serializable]
public class NotCondition : ConditionDescriptor
{
    public ConditionDescriptor Inner { get; set; }
}
```

### 9b.3 条件类型注册

```csharp
/// <summary>条件类型定义（由业务层注册）</summary>
public class ConditionTypeDef
{
    public string TypeId { get; set; }
    public string DisplayName { get; set; }
    public List<ConditionParamDef> Parameters { get; set; }
}

public class ConditionParamDef
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public ConditionParamType ParamType { get; set; }  // String, Int, Float, Bool, Enum
    public string? DefaultValue { get; set; }
}

/// <summary>条件类型注册表</summary>
public interface IConditionTypeRegistry
{
    void Register(ConditionTypeDef definition);
    ConditionTypeDef? GetDefinition(string typeId);
    IEnumerable<ConditionTypeDef> AllDefinitions { get; }
}
```

### 9b.4 条件放置位置

| 放置位置 | 用途 | 说明 |
|----------|------|------|
| `IEdgeData` 中包含 `ConditionDescriptor` | 连线跳转条件 | HFSM Transition / 刷怪蓝图过渡 / 对话分支 |
| `INodeData` 中自定义条件字段 | 节点守卫条件 | BT Decorator 等，框架层不参与 |

**原则**：框架层只在 Edge 上提供条件支持。Node 上的条件逻辑是业务层自定义节点类型的内部实现，
与框架无关，不会造成"Edge 条件 vs Node 条件"的语义冲突。

### 9b.5 与现有 TransitionEdgeData 的关系

```
当前（v2.1）：
    TransitionEdgeData { TransitionType, DelaySeconds, Condition(string) }

升级后（v2.2+）：
    TransitionEdgeData { ConditionDescriptor? Condition }
    
    Immediate  → Condition = null
    Delay 3s   → LeafCondition { TypeId="Delay", Parameters={"Duration":"3.0"} }
    OnComplete → LeafCondition { TypeId="OnComplete" }
    组合条件    → AndCondition { Children = [ LeafCondition, LeafCondition, ... ] }
```

### 9b.6 编辑器侧条件求值（脚本化扩展）

条件的"求值逻辑"分为三个层次，框架层通过 `IConditionEvaluator` 统一抽象：

```csharp
/// <summary>条件求值接口（编辑器侧 + 运行时侧共用接口定义）</summary>
public interface IConditionEvaluator
{
    bool Evaluate(ConditionDescriptor condition, IConditionContext context);
}

/// <summary>条件上下文（业务层实现，提供可查询的变量和事件）</summary>
public interface IConditionContext
{
    object? GetVariable(string name);
    IReadOnlySet<string> TriggeredEvents { get; }
}
```

**三种求值实现方式**：

| 层次 | 方式 | 适用场景 | 说明 |
|------|------|---------|------|
| 硬编码 C# 类 | 每种条件类型写一个 C# class | 程序员写复杂条件 | 编译时绑定，类型安全，性能最好 |
| 表达式求值 | Inspector 中写 `HP < 30 AND HasTarget` | 策划写简单条件 | 轻量级表达式解析器，无需 Roslyn |
| 嵌入式脚本 | Lua/Python 脚本片段 | 需要完整编程能力 | **仅限编辑器侧预览**，运行时不可用 |

**核心原则**：脚本引擎不是框架基础设施，而是一种 `LeafCondition` 类型。

```csharp
// 表达式条件 — 注册为一种 LeafCondition 类型
registry.Register(new ConditionTypeDef {
    TypeId = "Expression",
    DisplayName = "表达式",
    Parameters = { new ConditionParamDef { Key = "Expr", ParamType = String } }
});

// Lua 脚本条件 — 同样注册为一种 LeafCondition 类型
registry.Register(new ConditionTypeDef {
    TypeId = "LuaScript",
    DisplayName = "Lua 脚本",
    Parameters = { new ConditionParamDef { Key = "Script", ParamType = String } }
});
```

> **Quantum 限制**：Photon Quantum 确定性帧同步不允许非确定性代码（Lua/Python 的 GC 不可控），
> 因此脚本引擎只能用于编辑器侧预览/测试，运行时条件求值必须是纯 C#。

### 9b.7 运行时条件求值架构

编辑器中的 `ConditionDescriptor`（多态树、字典参数）不适合运行时高频求值。
需要在导出时"编译"为运行时格式。

#### 编辑时 vs 运行时

| 维度 | 编辑时 (ConditionDescriptor) | 运行时 (CompiledCondition) |
|------|---------------------------|--------------------------|
| 数据结构 | 多态树（class 继承） | 扁平连续内存 |
| 参数 | `Dictionary<string, string>` | 强类型整数索引 |
| 变量引用 | 字符串名称 | 整数索引（编译期映射） |
| GC | 不在乎 | **零 GC** |
| 序列化 | JSON（可读） | 二进制（紧凑） |

#### 导出流程

```
编辑器                    编译器                       运行时资产
ConditionDescriptor  ──→  ConditionCompiler  ──→  CompiledCondition
（树形、多态、字典）        （Editor-only）          （扁平、强类型、零GC）

编译器职责：
1. 遍历 ConditionDescriptor 树
2. 提取布尔条件 → 位掩码（RequiredTrue / RequiredFalse）
3. 剩余复杂条件 → 展平为 ConditionNode[] 数组
4. 变量名 → 整数索引（通过 ConditionVariableRegistry）
5. 序列化为二进制资产
```

#### 推荐方案：D+B 混合（位掩码 + 扁平数组）

```csharp
/// <summary>编译后的运行时条件（混合模式，零GC）</summary>
public struct CompiledCondition
{
    // ── 快速路径：位掩码（2-3 条 CPU 指令）──
    public ulong RequiredTrue;       // 这些位必须为 1
    public ulong RequiredFalse;      // 这些位必须为 0
    public bool HasBitmaskOnly;      // true = 只用位掩码，跳过慢速路径
    
    // ── 慢速路径：扁平数组（数值比较等复杂逻辑）──
    public ConditionNode[] Nodes;    // 扁平化的条件节点数组
    public int NodeCount;
}

/// <summary>条件指令操作类型</summary>
public enum RuntimeConditionType : byte
{
    // 组合
    And, Or, Not,
    // 整数比较
    CmpIntLt, CmpIntLe, CmpIntEq, CmpIntNe, CmpIntGt, CmpIntGe,
    // 定点数比较（Quantum FP）
    CmpFPLt, CmpFPLe, CmpFPEq, CmpFPNe, CmpFPGt, CmpFPGe,
    // 布尔 / 事件
    CheckBool, CheckEvent,
    // 常量
    AlwaysTrue, AlwaysFalse,
}

/// <summary>单个条件节点（值类型，16字节，cache 对齐）</summary>
public struct ConditionNode
{
    public RuntimeConditionType Type;     // 1 byte
    public byte ChildCount;               // 1 byte
    public short FirstChildIndex;         // 2 bytes
    public int OperandA;                  // 4 bytes（变量索引）
    public int OperandB;                  // 4 bytes（比较值 / 立即数）
    // padding 4 bytes → 总 16 bytes
}
```

**求值器（快速路径 + 慢速回退）**：

```csharp
public static bool Evaluate(ref CompiledCondition cond, ulong flags, IRuntimeConditionContext ctx)
{
    // ── 快速路径：位掩码检查 ──
    if ((flags & cond.RequiredTrue) != cond.RequiredTrue) return false;
    if ((flags & cond.RequiredFalse) != 0) return false;
    if (cond.HasBitmaskOnly) return true;
    
    // ── 慢速路径：扁平数组求值 ──
    return EvaluateNode(cond.Nodes, 0, ctx);
}

static bool EvaluateNode(ConditionNode[] nodes, int index, IRuntimeConditionContext ctx)
{
    ref var n = ref nodes[index];
    switch (n.Type)
    {
        case RuntimeConditionType.And:
            for (int i = 0; i < n.ChildCount; i++)
                if (!EvaluateNode(nodes, n.FirstChildIndex + i, ctx)) return false;
            return true;
        case RuntimeConditionType.Or:
            for (int i = 0; i < n.ChildCount; i++)
                if (EvaluateNode(nodes, n.FirstChildIndex + i, ctx)) return true;
            return false;
        case RuntimeConditionType.CmpIntLt:
            return ctx.GetInt(n.OperandA) < n.OperandB;
        case RuntimeConditionType.CheckBool:
            return ctx.GetBool(n.OperandA);
        // ...
    }
    return false;
}
```

**性能特征**：

```
场景 1（~80%）：只有布尔条件 → 纯位掩码，2-3 条 CPU 指令
场景 2（~15%）：布尔 + 数值混合 → 位掩码快速否定 + 少量数组求值
场景 3（~5%） ：纯数值条件 → 位掩码全通过，走扁平数组
```

#### 变量注册表（编译期名称 → 索引）

```csharp
/// <summary>变量注册表（编辑器侧维护，导出时生成索引映射）</summary>
public class ConditionVariableRegistry
{
    public int Register(string varName, ConditionVarType type);
    public ConditionVarDef[] ExportDefinitions();
}

/// <summary>运行时变量定义</summary>
public struct ConditionVarDef
{
    public int Index;
    public ConditionVarType Type;  // Int, FP, Bool
    public string Name;            // 仅调试用
}

/// <summary>运行时条件上下文（按索引访问，零字符串）</summary>
public interface IRuntimeConditionContext
{
    int GetInt(int varIndex);
    long GetFP(int varIndex);      // Quantum 定点数
    bool GetBool(int varIndex);
    bool HasEvent(int eventIndex);
}
```

### 9b.8 异步条件支持

"等待任务完成"等异步条件**不需要特殊的条件求值机制**。

#### 异步的本质

| 条件类型 | 本质 | 求值方式 |
|----------|------|---------|
| `HP < 30` | 瞬时查询（当前帧的值） | 单次求值 |
| `WaitForTaskComplete(A)` | 轮询查询（每帧检查状态变化） | 重复求值直到 true |
| `Delay(3s)` | 时间查询（比较已过时间） | 重复求值直到 true |

**条件求值器本身始终是无状态的纯函数。异步由图执行器（GraphExecutor）的"每 Tick 轮询"天然支持**：

```
图执行器（StateMachineRunner / GraphExecutor）
│
├── 当前激活节点: NodeA
├── 每 Tick:
│   ├── 遍历 NodeA 的所有出边
│   ├── 对每条边调用 Evaluate(edge.Condition, ctx)
│   ├── 如果某条边返回 true → 切换到目标节点
│   └── 如果都是 false → 停留在 NodeA，下一 Tick 再查
```

`WaitForTaskComplete(TaskA)` 在条件层面仅是：
```
CheckBool(varIndex = TaskA_Complete)   // 每帧读一下 flags，完成了就是 true
```

#### 三态返回值（为 BT 预留）

当前 HFSM 式跳转只需两态（true/false）。如果未来需要支持 BT，可扩展为三态：

```csharp
public enum ConditionResult : byte
{
    False   = 0,  // 不满足
    True    = 1,  // 满足
    Running = 2,  // 还在等待（BT Decorator 用）
}
```

对性能无影响（byte 比较），向后兼容（True/False 语义不变）。

### 9b.9 备选运行时方案参考

以下方案作为参考记录，在特定场景下可能有价值。

#### 方案 A：指令流（栈式虚拟机）

将条件树编译为后缀表达式指令流，运行时用 `stackalloc` 栈机求值。

```
条件树 AND(HP<30, OR(HasTarget, Distance<5))
编译为：
    [0] LOAD_INT var=HP    [1] PUSH_INT 30    [2] CMP_LT_INT
    [3] LOAD_BOOL var=HasTarget
    [4] LOAD_FP var=Distance    [5] PUSH_FP 5.0    [6] CMP_LT_FP
    [7] OR    [8] AND
```

```csharp
public struct InstructionCondition
{
    public ConditionOpCode[] OpCodes;
    public int[] Operands;
    public int Length;
}

// 栈机求值，stackalloc 零 GC
public static bool Evaluate(ref InstructionCondition cond, IRuntimeConditionContext ctx)
{
    Span<int> stack = stackalloc int[16];
    int sp = 0;
    for (int i = 0; i < cond.Length; i++)
    {
        switch (cond.OpCodes[i])
        {
            case PushInt:   stack[sp++] = cond.Operands[i]; break;
            case LoadInt:   stack[sp++] = ctx.GetInt(cond.Operands[i]); break;
            case CmpLtInt:  sp--; stack[sp-1] = stack[sp-1] < stack[sp] ? 1 : 0; break;
            case And:       sp--; stack[sp-1] &= stack[sp]; break;
            case Or:        sp--; stack[sp-1] |= stack[sp]; break;
            case Not:       stack[sp-1] = 1 - stack[sp-1]; break;
        }
    }
    return sp > 0 && stack[0] != 0;
}
```

| 优点 | 缺点 |
|------|------|
| 线性遍历，cache 友好 | 比扁平数组复杂 |
| `stackalloc` 零 GC | 不支持短路求值（AND 的左侧 false 仍会计算右侧） |
| 适合非常深的条件树 | 调试需反汇编工具 |

**适用场景**：条件树极深（>10 层），递归求值可能爆栈时。

#### 方案 C：编译期 C# 代码生成

导出时直接生成 C# 源码，编译为原生方法。

```csharp
// 自动生成：Conditions_SpawnPlan_001.cs
public static class SpawnPlan001Conditions
{
    public static bool Edge_001(Frame frame, EntityRef entity)
    {
        var hp = frame.Get<Health>(entity);
        return hp.Current < 30 && frame.Has<TargetComponent>(entity);
    }
}
```

| 优点 | 缺点 |
|------|------|
| 原生编译优化，性能最好 | 修改条件需重新生成 + 编译 |
| IDE 断点调试 | 需要代码生成模板 |
| 完全类型安全 | 与 Quantum API 强耦合 |

**适用场景**：性能分析确认条件求值是瓶颈后的极端优化手段。

#### 方案 E1：SIMD 批量求值

对同一条件同时在 N 个实体上求值（如 100 个 AI 怪物检查同一转换条件）：

```csharp
// 100 个实体的 flags 打包为 Vector<ulong>，一次 SIMD AND 指令完成
// Unity Burst 编译器可自动向量化循环
```

**适用场景**：大规模 AI（数百实体同时检查相同条件），需要 ECS 数据布局 + Burst。

#### 方案 E2：事件驱动（替代轮询）

不每帧检查条件，而是在依赖变量变化时主动触发通知：

```
OnHPChanged → if (newHP < 30) → 标记 transition 可用
```

| 优点 | 缺点 |
|------|------|
| 最快（不求值=零开销） | Quantum Tick 模型天然是轮询，事件驱动架构复杂 |
| 状态变化少时极省 CPU | 维护依赖关系图复杂度高 |

**适用场景**：纯事件驱动架构（如 UI 系统、对话树），不适合确定性帧同步。

#### 方案对比总览

| 方案 | 性能 | 实现复杂度 | 异步支持 | 确定性 | 推荐度 |
|------|------|----------|---------|--------|-------|
| **D+B 混合** ⭐ | ⭐⭐⭐⭐⭐ | 中 | ✅ | ✅ | **首选** |
| B 扁平数组 | ⭐⭐⭐⭐ | 低 | ✅ | ✅ | 备选（不需要位掩码优化时） |
| A 指令流 | ⭐⭐⭐⭐ | 中 | ✅ | ✅ | 参考（极深条件树） |
| C 代码生成 | ⭐⭐⭐⭐⭐ | 高 | ✅ | ✅ | 参考（极端性能优化） |
| D 纯位掩码 | ⭐⭐⭐⭐⭐⭐ | 极低 | ✅ | ✅ | D+B 的子集 |
| E1 SIMD | ⭐⭐⭐⭐⭐⭐ | 很高 | ✅ | ✅ | 参考（大规模 AI） |
| E2 事件驱动 | ∞ | 很高 | ✅ | ⚠️ | 参考（纯事件架构） |

### 9b.10 职责划分

| 模块 | 位置 | 说明 |
|------|------|------|
| `ConditionDescriptor` + 组合类型 | NodeGraph.Core | 框架层，纯 C# |
| `IConditionTypeRegistry` | NodeGraph.Core | 框架层接口 |
| `IConditionEvaluator` / `IConditionContext` | NodeGraph.Core | 编辑器侧求值接口 |
| `ConditionCompiler` | NodeGraph.Core 或业务层 | 描述树 → CompiledCondition |
| `CompiledCondition` + `ConditionNode` | 运行时共享库 | 编辑器和运行时都引用 |
| `IRuntimeConditionContext` | 运行时业务层 | 对接 Quantum Frame/Entity |
| `ConditionVariableRegistry` | 编辑器业务层 | 每种蓝图类型自定义可用变量 |

### 9b.11 整体架构图

```
┌──────────────────────────────────────────────────────────┐
│                      编辑器侧                             │
│                                                          │
│  ConditionDescriptor（树形、多态、Dictionary）               │
│       │                                                  │
│       ├─→ IConditionEvaluator（编辑器预览/测试）            │
│       │     ├── C# 硬编码条件类                            │
│       │     ├── 表达式求值器（"HP < 30"）                   │
│       │     └── Lua/Python 脚本（仅编辑器，非确定性）        │
│       │                                                  │
│       └─→ ConditionCompiler（导出时一次性编译）              │
│             ├── ConditionVariableRegistry（名称→索引）       │
│             ├── 提取布尔条件 → RequiredTrue/RequiredFalse   │
│             └── 复杂条件 → ConditionNode[] 扁平数组         │
│                    ↓                                     │
│             CompiledCondition（二进制资产）                  │
└──────────────────────────────────────────────────────────┘
                         ↓ 导出
┌──────────────────────────────────────────────────────────┐
│                      运行时侧                             │
│                                                          │
│  加载 CompiledCondition                                   │
│       ↓                                                  │
│  Evaluate(cond, flags, ctx)                               │
│       ├── 快速路径：位掩码 AND/NOT（2-3 条 CPU 指令）        │
│       └── 慢速路径：扁平数组递归求值（短路优化）              │
│       ↓                                                  │
│  IRuntimeConditionContext（按索引查询，零GC，零字符串）       │
│       ↓                                                  │
│  返回 bool（或 ConditionResult 三态，为 BT 预留）           │
│                                                          │
│  图执行器每 Tick 轮询出边条件 → 天然支持异步/等待            │
└──────────────────────────────────────────────────────────┘
```

---

## 10. 命令系统（Undo/Redo）

### 10.1 ICommand

```csharp
public interface ICommand
{
    string Description { get; }
    void Execute(Graph graph);
    void Undo(Graph graph);
}
```

### 10.2 CommandHistory

```csharp
public class CommandHistory
{
    public void Execute(ICommand command);
    public void Undo();
    public void Redo();
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public int UndoCount { get; }
    public int RedoCount { get; }
    public event Action? OnHistoryChanged;
    
    /// <summary>开始一个复合命令（多个操作合并为一次 Undo）</summary>
    public IDisposable BeginCompound(string description);
}
```

### 10.3 内置命令

| 命令 | 说明 |
|------|------|
| `AddNodeCommand` | 添加节点 |
| `RemoveNodeCommand` | 删除节点（自动断开相关连线） |
| `MoveNodeCommand` | 移动节点 |
| `ConnectCommand` | 连线 |
| `DisconnectCommand` | 断线 |
| `AddPortCommand` | 添加动态端口 |
| `RemovePortCommand` | 移除动态端口 |
| `ChangeNodeDataCommand` | 修改节点业务数据 |
| `ChangeEdgeDataCommand` | 修改连线业务数据 |
| `CreateGroupCommand` | 创建分组 |
| `CreateCommentCommand` | 创建注释 |
| `PasteCommand` | 粘贴（从剪贴板反序列化子图） |
| `ChangeDisplayModeCommand` | 切换节点折叠/展开 |
| `ToggleSubGraphCollapseCommand` | 切换子图框折叠/展开状态（v2.3） |
| `CreateSubGraphCommand` | 从源图资产创建内联子图框（v2.3） |

---

## 11. 事件系统

```csharp
public class GraphEvents
{
    // 节点事件
    public event Action<Node>? OnNodeAdded;
    public event Action<Node>? OnNodeRemoved;
    public event Action<Node, Vec2, Vec2>? OnNodeMoved;         // node, oldPos, newPos
    public event Action<Node>? OnNodeSelected;
    public event Action<Node>? OnNodeDeselected;
    public event Action<Node>? OnNodeDoubleClicked;              // 业务层自定义响应（如展开/折叠子图框）
    public event Action<Node, NodeDisplayMode>? OnNodeDisplayModeChanged;
    
    // 连线事件
    public event Action<Edge>? OnEdgeAdded;
    public event Action<Edge>? OnEdgeRemoved;
    public event Action<Edge>? OnEdgeSelected;
    
    // 端口事件
    public event Action<Port>? OnPortAdded;
    public event Action<Port>? OnPortRemoved;
    public event Action<Port, Port>? OnConnectionAttempt;        // 尝试连线时
    
    // 装饰元素事件
    public event Action<NodeGroup>? OnGroupCreated;
    public event Action<NodeGroup>? OnGroupRemoved;
    public event Action<GraphComment>? OnCommentCreated;
    
    // 通用事件
    public event Action? OnGraphChanged;                          // 任何变化
    public event Action<ICommand>? OnCommandExecuted;             // 命令执行后
}
```

---

## 12. 渲染架构

### 12.1 渲染模式演进

| 版本 | 模式 | 优点 | 缺点 |
|------|------|------|------|
| v1.x | IDrawContext 图元代理 | 简单统一 | 引擎能力退化为最小公约数（**已移除**） |
| **v2.0** | **GraphFrame 渲染描述** | **引擎100%自由** | 每个引擎需实现 Renderer |
| v2.1 | Zero-Matrix 坐标模式 | 消除 Handles+缩放偏移 | 所有坐标需手动 C2W() |
| v2.2 | 条件描述 + SubGraph 扁平化 | 支持丰富的条件逻辑和子图 | 条件系统待实现 |

### 12.2 引擎原生渲染器接口

每个引擎实现自己的 Renderer，直接消费 GraphFrame：

```csharp
// Unity 示例
public class UnityGraphRenderer
{
    public void Render(GraphFrame frame, NodeVisualTheme theme, Rect viewport)
    {
        DrawBackground(frame.Background, viewport);
        foreach (var node in frame.Nodes) DrawNode(node, theme);
        foreach (var edge in frame.Edges) DrawEdge(edge, theme);
        foreach (var overlay in frame.Overlays) DrawOverlay(overlay);
        if (frame.MiniMap != null) DrawMiniMap(frame.MiniMap, theme);
    }
    
    private void DrawNode(NodeFrame node, NodeVisualTheme theme)
    {
        // 纯矢量绘制：DrawFilledRoundedRect + DrawSolidDisc + DrawAAPolyLine
    }
}
```

### 12.3 IEditContext（编辑控件接口）

用于节点内嵌编辑。IMGUI 风格（传入当前值，返回修改后的值）。

```csharp
public interface IEditContext
{
    // ── 基础控件 ──
    float FloatField(string label, float value);
    int IntField(string label, int value);
    string TextField(string label, string value);
    bool Toggle(string label, bool value);
    float Slider(string label, float value, float min, float max);
    int Popup(string label, int selectedIndex, string[] options);
    Color4 ColorField(string label, Color4 value);
    
    // ── 布局辅助 ──
    void Label(string text);
    void Space(float pixels);
    void BeginHorizontal();
    void EndHorizontal();
    bool Foldout(string label, bool expanded);
    void Separator();
    
    // ── 状态查询 ──
    bool HasChanged { get; }
    
    // ── 可用区域 ──
    Rect2 AvailableRect { get; }
}
```

### 12.3 各引擎适配映射

**GraphFrame 渲染映射**（v2.1 Zero-Matrix 模式）：

| GraphFrame 元素 | Unity 渲染技术 (Zero-Matrix) | Godot 渲染技术 | Dear ImGui |
|----------------|-------------------------------|--------------------------|------------|
| NodeFrame 背景 | `EditorGUI.DrawRect` + C2WRect | `draw_style_box(StyleBoxFlat)` | `AddRectFilled(..., rounding)` |
| NodeFrame 阴影 | `EditorGUI.DrawRect` + 偏移 | `StyleBoxFlat.shadow_*` | `AddRectFilled` + offset |
| EdgeFrame | `Handles.DrawBezier` + C2W | `DrawPolyline()` 分段模拟 | `AddBezierCubic()` |
| PortFrame | `Handles.DrawSolidDisc` + C2W + S() | `draw_circle` | `AddCircleFilled` |
| 文字 | `GUI.Label` + C2WRect + ScaledFontSize | `DrawString()` | `AddText()` |
| 视口裁剪 | 手动 `_visibleCanvasRect` 判断 | `DrawSetClipRect` | `PushClipRect()` |

**IEditContext 映射**：

| IEditContext 方法 | Unity | Godot 4 | Dear ImGui |
|------------------|-------|---------|------------|
| `FloatField` | `EditorGUI.FloatField` | SpinBox 控件池 | `ImGui.DragFloat` |
| `TextField` | `EditorGUI.TextField` | LineEdit 控件池 | `ImGui.InputText` |
| `Toggle` | `EditorGUI.Toggle` | CheckBox 控件池 | `ImGui.Checkbox` |
| `Popup` | `EditorGUI.Popup` | OptionButton 控件池 | `ImGui.Combo` |
| `Slider` | `EditorGUI.Slider` | HSlider 控件池 | `ImGui.SliderFloat` |

---

## 13. 节点内容渲染

### 13.1 INodeContentRenderer

```csharp
public interface INodeContentRenderer
{
    /// <summary>是否支持内嵌编辑</summary>
    bool SupportsInlineEdit { get; }
    
    /// <summary>计算摘要视图尺寸</summary>
    Vec2 GetSummarySize(Node node);
    
    /// <summary>绘制摘要视图（只读，Collapsed 和 Expanded 模式均可显示）</summary>
    NodeContentInfo GetSummaryInfo(Node node, Rect2 rect);
    
    /// <summary>获取折叠模式下的一行文字摘要</summary>
    string GetOneLiner(Node node);
    
    /// <summary>计算编辑视图尺寸</summary>
    Vec2 GetEditorSize(Node node, IEditContext ctx);
    
    /// <summary>绘制编辑视图（可交互，仅 Expanded + 选中时调用）</summary>
    void DrawEditor(Node node, Rect2 rect, IEditContext ctx);
}
```

### 13.2 节点绘制流程

框架控制节点的整体绘制流程，业务层只负责"内容区域"：

```
框架绘制：
┌──────────────────────────────────┐
│ [标题栏] TypeName    ▼ ✕        │  ← 框架绘制（颜色 + 名称 + 折叠/关闭按钮）
├──────────────────────────────────┤
│ ● In                             │  ← 框架绘制（端口图标 + 名称）
│                                  │
│   ┌─ 内容区域 ────────────────┐  │
│   │  业务层绘制               │  │  ← INodeContentRenderer
│   │  (DrawSummary 或          │  │
│   │   DrawEditor)             │  │
│   └───────────────────────────┘  │
│                                  │
│                         Out ●    │  ← 框架绘制
├──────────────────────────────────┤
│ [状态栏] Error: ...（可选）      │  ← 框架绘制（错误信息）
└──────────────────────────────────┘
```

**模式切换逻辑**：

| 状态 | 显示内容 |
|------|----------|
| Expanded + 未选中 | 标题 + 端口 + DrawSummary |
| Expanded + 选中 + SupportsInlineEdit | 标题 + 端口 + DrawEditor |
| Collapsed | 标题 + 端口（紧凑排列）+ GetOneLiner |
| Minimized | 单行：端口 + 标题 + 端口 |

### 13.3 IEdgeLabelRenderer（连线标签）

```csharp
public interface IEdgeLabelRenderer
{
    Vec2 GetLabelSize(Edge edge);
    EdgeLabelInfo GetLabelInfo(Edge edge, Vec2 midpoint);
    bool HandleLabelClick(Edge edge, Rect2 labelRect);
}
```

用于在连线中点绘制标签（如 TransitionCondition 的 "AllKilled" / "Delay 3s"）。

---

## 14. 视图层（NodeGraph.View）

### 14.1 GraphViewModel

```csharp
public class GraphViewModel
{
    public Graph Graph { get; }
    
    // 视口状态
    public Vec2 PanOffset { get; set; }          // 画布平移
    public float ZoomLevel { get; set; }         // 缩放级别
    public float MinZoom { get; set; } = 0.1f;
    public float MaxZoom { get; set; } = 3.0f;
    
    // 选中状态
    public SelectionManager Selection { get; }
    
    // 命令历史
    public CommandHistory Commands { get; }
    
    // 坐标转换
    public Vec2 ScreenToCanvas(Vec2 screenPos);
    public Vec2 CanvasToScreen(Vec2 canvasPos);
    public Rect2 GetVisibleCanvasRect();
    
    // 主循环入口（由引擎宿主窗口驱动）
    public void ProcessInput(IPlatformInput input);      // 处理输入
    public void Update(float deltaTime);                 // 更新状态（动画等）
    public GraphFrame BuildFrame(Rect2 viewport);    // 构建渲染描述
    public bool NeedsRepaint { get; }                    // 是否需要重绘
}
```

### 14.1a 双层 API 设计

```
高层 API（经过 CommandHistory，可 Undo）—— 所有用户交互走这里：
  viewModel.Commands.Execute(new AddNodeCommand(...))

低层 API（直接操作，不可 Undo）—— 供 Command 内部和反序列化使用：
  graph.AddNode(...)
```

业务层正常使用时只接触 `GraphViewModel`，所有操作自动进入 Undo 栈。`Graph` 的低层 API 仅供框架内部使用。

### 14.1b 渲染主循环（v2.0）

框架不拥有主循环，由引擎宿主窗口驱动。v2.0 中 `Render` 替换为 `BuildFrame`。

**v2.1 事件分流优化**：Unity IMGUI 每帧调用 `OnGUI` 多次（Layout、Repaint、各种输入事件），
为避免每次调用都执行完整流水线，按事件类型分流处理：

```
引擎宿主窗口 OnGUI（每帧被调用 2~3+ 次）
    │
    ├── input.Update(engineEvent)                    ← 每次都更新输入状态
    │
    ├── [输入事件] (MouseDown/Drag/Up/Key/Scroll)
    │   ├── viewModel.PreUpdateNodeSizes()           ← 确保命中检测准确
    │   └── viewModel.ProcessInput(input)            ← 交互处理（纯 C#）
    │
    ├── [Repaint 事件] （每帧仅一次）
    │   ├── viewModel.Update(deltaTime)              ← 更新状态（动画等）
    │   ├── frame = viewModel.BuildFrame(viewport)   ← 构建渲染描述（纯 C#）
    │   └── engineRenderer.Render(frame, theme)      ← 引擎原生绘制
    │
    ├── [Layout 事件]
    │   └── （画布无需处理，仅供 IMGUI 布局控件使用）
    │
    └── if (viewModel.NeedsRepaint) Repaint()        ← 请求下一帧重绘
```

**NeedsRepaint 生命周期**（v2.1 修复）：
- `ProcessInput` **开头**重置 `NeedsRepaint = false`
- 处理器在 `HandleInput` 中按需调用 `RequestRepaint()` 设置为 `true`
- `BuildFrame` **不再**重置 `NeedsRepaint`，确保标记存活到窗口代码检查

> ⚠️ v2.0 原设计中 `BuildFrame` 开头重置 `NeedsRepaint = false`，导致处理器的重绘请求
> 被吞掉，`Repaint()` 永远不会被调用。窗口只能依赖系统事件被动重绘，表现为"帧率低"。

`BuildFrame()` 内部委托给 `BlueprintProfile.FrameBuilder`：
- 遍历可见节点/连线
- 计算端口位置、节点尺寸、连线贝塞尔
- 解析选中状态、颜色、发光层
- 输出 `GraphFrame`（纯数据，无绘制调用）


### 14.2 SelectionManager

```csharp
public class SelectionManager
{
    public IReadOnlyList<string> SelectedNodeIds { get; }
    public IReadOnlyList<string> SelectedEdgeIds { get; }
    public string? PrimarySelectedNodeId { get; }  // 主选中节点
    
    // 操作
    public void Select(string nodeId);                          // 单选
    public void AddToSelection(string nodeId);                  // Shift + 点击追加
    public void RemoveFromSelection(string nodeId);             // Ctrl + 点击取消
    public void SelectMultiple(IEnumerable<string> nodeIds);    // 框选
    public void ClearSelection();
    public bool IsSelected(string nodeId);
    
    // 事件
    public event Action? OnSelectionChanged;
}
```

### 14.3 交互处理器

框架内置以下交互处理器，通过 `IPlatformInput` 接收输入：

| 处理器 | 职责 |
|--------|------|
| `PanZoomController` | 鼠标中键拖拽平移、滚轮缩放 |
| `NodeDragHandler` | 拖拽节点移动 |
| `ConnectionDragHandler` | 从端口拖出连线 |
| `MarqueeSelectionHandler` | 框选（左键拖拽空白区域） |
| `ContextMenuHandler` | 右键菜单 |
| `NodeInteractionHandler` | 节点点击/双击/折叠 |
| `GroupDragHandler` | 拖拽分组（内部节点跟随） |

### 14.4 IPlatformInput

```csharp
public interface IPlatformInput
{
    Vec2 MousePosition { get; }
    Vec2 MouseDelta { get; }
    float ScrollDelta { get; }
    
    bool IsMouseDown(MouseButton button);
    bool IsMouseUp(MouseButton button);
    bool IsMouseDrag(MouseButton button);
    bool IsDoubleClick(MouseButton button);
    
    bool IsKeyDown(string keyName);
    bool IsKeyHeld(string keyName);
    
    // 修饰键
    bool IsShiftHeld { get; }
    bool IsCtrlHeld { get; }
    bool IsAltHeld { get; }
    
    // 剪贴板
    string GetClipboardText();
    void SetClipboardText(string text);
}

public enum MouseButton { Left, Right, Middle }
```

### 14.5 框选行为

```
左键在空白区域拖拽 → 显示框选矩形
  - 无修饰键：清空已有选择，选中框内节点
  - Shift：追加框内节点到选择
  - Ctrl：从选择中移除框内节点（取消选中）
释放鼠标 → 完成选择
```

---

## 15. 快捷键系统

### 15.1 可配置快捷键

```csharp
public class KeyBinding
{
    public string ActionId { get; }       // 动作标识
    public string DisplayName { get; }    // 显示名
    public KeyCombination DefaultKey { get; }
    public KeyCombination CurrentKey { get; set; }
}

public struct KeyCombination
{
    public string Key { get; }            // 主键名（"Delete", "D", "F", ...）
    public bool Ctrl { get; }
    public bool Shift { get; }
    public bool Alt { get; }
}

public class KeyBindingManager
{
    public void Register(KeyBinding binding);
    public void SetBinding(string actionId, KeyCombination key);
    public KeyCombination GetBinding(string actionId);
    public IEnumerable<KeyBinding> GetAllBindings();
    
    /// <summary>检查当前帧是否触发了指定动作</summary>
    public bool IsActionTriggered(string actionId, IPlatformInput input);
    
    /// <summary>从 JSON 加载用户自定义快捷键</summary>
    public void LoadFromJson(string json);
    public string SaveToJson();
}
```

### 15.2 内置动作和默认快捷键

| 动作ID | 显示名 | 默认快捷键 |
|--------|--------|-----------|
| `delete` | 删除选中 | Delete |
| `duplicate` | 复制选中 | Ctrl+D |
| `copy` | 复制 | Ctrl+C |
| `paste` | 粘贴 | Ctrl+V |
| `cut` | 剪切 | Ctrl+X |
| `undo` | 撤销 | Ctrl+Z |
| `redo` | 重做 | Ctrl+Y |
| `select_all` | 全选 | Ctrl+A |
| `focus_selected` | 聚焦选中节点 | F |
| `focus_all` | 聚焦全部节点 | A |
| `collapse` | 折叠/展开选中节点 | H |
| `minimize` | 最小化选中节点 | Shift+H |
| `create_group` | 将选中节点创建为组 | Ctrl+G |
| `search` | 打开搜索 | Ctrl+F |
| `add_node` | 打开添加节点菜单 | Space / Tab |
| `back` | 返回上级 / 取消当前操作 | Backspace |

---

## 16. 搜索与过滤

### 16.1 SearchMenuModel（添加节点菜单）

右键空白区域或按 Space 打开：

```csharp
public class SearchMenuModel
{
    public string SearchText { get; set; }
    public Vec2 Position { get; set; }          // 菜单在画布上的位置
    public bool IsOpen { get; set; }
    
    /// <summary>根据搜索文本过滤可用节点类型</summary>
    public IEnumerable<NodeTypeDefinition> GetFilteredTypes(NodeTypeRegistry registry);
    
    /// <summary>按分类分组</summary>
    public IEnumerable<(string Category, IEnumerable<NodeTypeDefinition> Types)>
        GetGroupedTypes(NodeTypeRegistry registry);
}
```

### 16.2 NodeSearchModel（查找已有节点）

Ctrl+F 打开：

```csharp
public class NodeSearchModel
{
    public string SearchText { get; set; }
    public bool IsOpen { get; set; }
    
    /// <summary>按名称/类型/ID搜索图中已有节点</summary>
    public IEnumerable<Node> Search(Graph graph);
    
    /// <summary>选中并聚焦到指定节点</summary>
    public void NavigateTo(string nodeId, GraphViewModel viewModel);
}
```

---

## 17. 小地图（MiniMap）

```csharp
public class MiniMapRenderer
{
    public Vec2 Size { get; set; } = new Vec2(200, 150);
    public MiniMapPosition Position { get; set; } = MiniMapPosition.BottomRight;
    public float Opacity { get; set; } = 0.8f;
    public bool IsVisible { get; set; } = true;
    
    // 小地图数据已包含在 GraphFrame.MiniMap 中，由引擎渲染器统一绘制
    
    /// <summary>处理小地图上的点击（快速跳转）</summary>
    public bool HandleInput(GraphViewModel viewModel, IPlatformInput input, Rect2 windowRect);
}

public enum MiniMapPosition
{
    TopLeft, TopRight, BottomLeft, BottomRight
}
```

小地图显示：
- 所有节点的缩略矩形（用节点颜色填充）
- 当前视口的矩形框（半透明白色）
- 点击小地图 → 视口跳转到对应位置
- 在小地图上拖拽 → 实时平移视口

---

## 18. 序列化与持久化

序列化和持久化是两个独立的层次，各引擎可以独立选择策略：

```
Graph（内存对象）
    ↓ IGraphSerializer（序列化层）
中间格式（JSON string / byte[] / ...）
    ↓ IGraphPersistence（持久化层）
存储介质（.asset / .tres / .json 文件 / 数据库）
```

某些引擎可以**跳过中间格式**，直接把 Graph 映射到引擎原生存储：

```
通用路径：  Graph → JSON string → 存入 SO 的 string 字段 → .asset
原生路径：  Graph → 直接映射 SO 的 [Serializable] 字段 → .asset（跳过 JSON）
```

### 18.1 IGraphSerializer（序列化层）

用于复制粘贴、跨引擎导入导出、调试查看等场景：

```csharp
public interface IGraphSerializer
{
    string Serialize(Graph graph);
    Graph? Deserialize(string data);
    
    /// <summary>序列化子图（选中的节点和连线，用于复制粘贴）</summary>
    string SerializeSubGraph(Graph graph, IEnumerable<string> nodeIds);
    
    /// <summary>反序列化子图并合并到目标图</summary>
    IEnumerable<Node> DeserializeSubGraphInto(Graph target, string data, Vec2 offset);
}
```

### 18.2 IGraphPersistence（持久化层）

每个引擎提供自己的实现：

```csharp
public interface IGraphPersistence
{
    void Save(Graph graph);
    Graph? Load();
    bool IsDirty { get; }       // 内存中是否有未保存的修改
}
```

### 18.3 各引擎持久化策略

| 引擎 | 持久化方式 | 是否经过 JSON |
|------|-----------|-------------|
| **Unity** | ScriptableObject（原生序列化） | **否**，结构数据直接映射 `[Serializable]`，业务数据用 JSON |
| **Godot** | Resource (.tres) | **否**，直接映射 Export 字段 |
| **跨引擎导出** | .json 文件 | **是**，用 `IGraphSerializer` |
| **剪贴板** | JSON 字符串 | **是**，用 `IGraphSerializer` |

### 18.4 Unity 原生持久化实现

```csharp
// NodeGraph.Unity 中
[CreateAssetMenu(menuName = "NodeGraph/Graph Asset")]
public class GraphAsset : ScriptableObject
{
    [SerializeField] private string _graphId;
    [SerializeField] private GraphTopologyPolicy _topology;
    [SerializeField] private List<SerializedNode> _nodes = new();
    [SerializeField] private List<SerializedEdge> _edges = new();
    [SerializeField] private List<SerializedGroup> _groups = new();
    [SerializeField] private List<SerializedComment> _comments = new();
}

[Serializable]
public class SerializedNode
{
    public string id;                  // GUID
    public string typeId;
    public Vector2 position;           // 直接用 Unity 的 Vector2
    public int displayMode;
    public List<SerializedPort> ports;
    public string userDataJson;        // 业务数据仍用 JSON（因为类型不定）
}
```

这样 Unity 侧：
- **结构数据**（位置、ID、端口）→ Unity 原生序列化，性能好，Inspector 友好
- **业务数据**（INodeData）→ JSON 字符串字段（因为框架不知道业务类型）
- **不走一遍完整 JSON 序列化**，避免大图的性能问题

### 18.5 JsonGraphSerializer

默认实现，用于跨引擎导入导出和剪贴板：

```json
{
    "id": "3a7f2b1c-e4d8-4a5f-b9c2-1d3e5f7a8b0c",
    "settings": {
        "topology": "DAG"
    },
    "nodes": [
        {
            "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "typeId": "SpawnTask",
            "position": { "x": 100, "y": 200 },
            "displayMode": "Expanded",
            "ports": [
                { "id": "f0e1d2c3-b4a5-6789-0123-456789abcdef", "name": "In", "direction": "Input", "kind": "Control", "dataType": "exec" },
                { "id": "12345678-9abc-def0-1234-56789abcdef0", "name": "Out", "direction": "Output", "kind": "Control", "dataType": "exec" }
            ],
            "userData": { ... }
        }
    ],
    "edges": [
        {
            "id": "fedcba98-7654-3210-fedc-ba9876543210",
            "sourcePortId": "12345678-9abc-def0-1234-56789abcdef0",
            "targetPortId": "aabbccdd-eeff-0011-2233-445566778899",
            "userData": { ... }
        }
    ],
    "groups": [ ... ],
    "comments": [ ... ]
}
```

### 18.6 IUserDataSerializer

业务层数据的 JSON 序列化接口，`INodeData` 和 `IEdgeData` 需要业务层提供：

```csharp
public interface IUserDataSerializer
{
    string SerializeNodeData(INodeData data);
    INodeData? DeserializeNodeData(string typeId, string json);
    string SerializeEdgeData(IEdgeData data);
    IEdgeData? DeserializeEdgeData(string json);
}
```

---

## 19. 自动布局

```csharp
public interface ILayoutAlgorithm
{
    /// <summary>计算节点布局位置</summary>
    Dictionary<string, Vec2> ComputeLayout(Graph graph, Vec2 startPosition);
}
```

### 19.1 内置布局算法

| 算法 | 适用场景 |
|------|----------|
| `TreeLayout` | 树形结构（行为树、单入口DAG） |
| `LayeredLayout` | 分层布局（DAG，类似 Sugiyama 算法） |
| `ForceDirectedLayout` | 力导向布局（通用，适合无明确方向的图） |

---

## 20. 数学类型（NodeGraph.Math）

```csharp
public struct Vec2
{
    public float X;
    public float Y;
    
    // 构造/运算符/常用方法
    public static Vec2 Zero => new Vec2(0, 0);
    public static Vec2 One => new Vec2(1, 1);
    public float Length();
    public float LengthSquared();
    public Vec2 Normalized();
    public static float Distance(Vec2 a, Vec2 b);
    public static Vec2 Lerp(Vec2 a, Vec2 b, float t);
    
    // 运算符
    public static Vec2 operator +(Vec2 a, Vec2 b);
    public static Vec2 operator -(Vec2 a, Vec2 b);
    public static Vec2 operator *(Vec2 v, float s);
    
    // 引擎隐式转换（在适配层中通过扩展方法提供）
}

public struct Rect2
{
    public float X, Y, Width, Height;
    
    public Vec2 Position { get; set; }
    public Vec2 Size { get; set; }
    public Vec2 Center { get; }
    public Vec2 TopLeft { get; }
    public Vec2 BottomRight { get; }
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }
    
    public bool Contains(Vec2 point);
    public bool Overlaps(Rect2 other);
    public Rect2 Expand(float padding);
    public static Rect2 Encapsulate(IEnumerable<Rect2> rects);
}

public struct Color4
{
    public float R, G, B, A;
    
    public static Color4 White => new Color4(1, 1, 1, 1);
    public static Color4 Black => new Color4(0, 0, 0, 1);
    public static Color4 FromHex(string hex);
    public Color4 WithAlpha(float alpha);
    
    // 预定义颜色用于节点/端口
    public static class Palette
    {
        public static Color4 ControlPort => FromHex("#FFFFFF");
        public static Color4 FloatPort => FromHex("#84E084");
        public static Color4 IntPort => FromHex("#6BB5FF");
        public static Color4 StringPort => FromHex("#F5A623");
        public static Color4 BoolPort => FromHex("#E05252");
    }
}

public static class BezierMath
{
    /// <summary>计算贝塞尔曲线上的点</summary>
    public static Vec2 Evaluate(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t);
    
    /// <summary>将贝塞尔曲线分段为折线（用于不支持贝塞尔的引擎）</summary>
    public static Vec2[] Tessellate(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, int segments);
    
    /// <summary>计算两个端口之间的贝塞尔曲线切线</summary>
    public static (Vec2 tangentA, Vec2 tangentB) ComputePortTangents(
        Vec2 sourcePos, Vec2 targetPos, PortDirection sourceDir);
}
```

---

## 21. 程序集与目录结构

```
Assets/Extensions/NodeGraph/
│
├── Documents/
│   ├── NodeGraph设计文档.md              ← 本文档
│   ├── 接口参考.md                       ← API 快速参考
│   └── 测试指南.md                       ← 测试验证指南
│
├── Math/                                  ← NodeGraph.Math.asmdef（零依赖）
│   ├── Vec2.cs
│   ├── Rect2.cs
│   ├── Color4.cs
│   └── BezierMath.cs
│
├── Core/                                  ← NodeGraph.Core.asmdef
│   ├── AssemblyInfo.cs                    ← InternalsVisibleTo 配置
│   ├── Graph.cs
│   ├── Node.cs
│   ├── Port.cs                            ← Port + PortDefinition
│   ├── Edge.cs
│   ├── IdGenerator.cs
│   ├── GraphSettings.cs
│   ├── GraphEvents.cs
│   ├── GraphDecoration.cs                 ← NodeGroup + GraphComment
│   ├── SubGraphNode.cs                    ← 旧版（v2.2 后由 SubGraphFrame 替代，待迁移）
│   ├── SubGraphFrame.cs                   ← v2.2 扁平化内联框（计划新增）
│   ├── ConditionDescriptor.cs             ← v2.2 条件描述树（计划新增）
│   ├── IConditionTypeRegistry.cs          ← v2.2 条件类型注册表（计划新增）
│   ├── NodeDisplayMode.cs
│   ├── NodeTypeDefinition.cs
│   ├── NodeTypeRegistry.cs
│   ├── TypeCompatibilityRegistry.cs
│   ├── IConnectionPolicy.cs
│   ├── DefaultConnectionPolicy.cs
│   ├── GraphAlgorithms.cs
│   └── Interfaces/
│       ├── INodeData.cs
│       └── IEdgeData.cs
│
├── Commands/                              ← NodeGraph.Commands.asmdef
│   ├── ICommand.cs
│   ├── CompoundCommand.cs
│   ├── CommandHistory.cs
│   └── BuiltIn/
│       ├── AddNodeCommand.cs
│       ├── RemoveNodeCommand.cs
│       ├── MoveNodeCommand.cs
│       ├── ConnectCommand.cs
│       ├── DisconnectCommand.cs
│       ├── AddGroupCommand.cs
│       └── AddCommentCommand.cs
│
├── Abstraction/                           ← NodeGraph.Abstraction.asmdef
│   ├── IEditContext.cs
│   ├── IPlatformInput.cs
│   ├── INodeContentRenderer.cs
│   ├── IEdgeLabelRenderer.cs
│   ├── IGraphValidator.cs
│   ├── IGraphPersistence.cs
│   └── IGraphSerializer.cs
│
├── View/                                  ← NodeGraph.View.asmdef
│   ├── GraphViewModel.cs
│   ├── SelectionManager.cs
│   ├── SearchMenuModel.cs
│   ├── NodeSearchModel.cs
│   ├── MiniMapRenderer.cs
│   ├── KeyBindingManager.cs
│   ├── NodeVisualTheme.cs                 ← 视觉主题配置（v1.1+）
│   ├── GraphFrame/                        ← 渲染描述数据类型（v2.0）
│   │   ├── GraphFrame.cs
│   │   ├── NodeFrame.cs
│   │   ├── EdgeFrame.cs
│   │   ├── PortFrame.cs
│   │   ├── OverlayFrame.cs
│   │   ├── BackgroundFrame.cs
│   │   └── MiniMapFrame.cs
│   ├── FrameBuilders/                     ← 渲染描述构建器（v2.0）
│   │   ├── IGraphFrameBuilder.cs
│   │   └── DefaultFrameBuilder.cs
│   ├── BlueprintProfile.cs                ← 蓝图配置包（v2.0）
│   └── Handlers/
│       ├── IInteractionHandler.cs
│       ├── PanZoomController.cs
│       ├── NodeDragHandler.cs
│       ├── ConnectionDragHandler.cs
│       ├── MarqueeSelectionHandler.cs
│       └── NodeInteractionHandler.cs
│
├── Serialization/                         ← NodeGraph.Serialization.asmdef（noEngineReferences）
│   ├── IUserDataSerializer.cs
│   ├── JsonGraphModel.cs
│   ├── JsonGraphSerializer.cs
│   └── SimpleJson.cs
│
├── Layout/                                ← NodeGraph.Layout.asmdef（noEngineReferences）
│   ├── ILayoutAlgorithm.cs
│   ├── TreeLayout.cs
│   ├── LayeredLayout.cs
│   ├── ForceDirectedLayout.cs
│   └── LayoutHelper.cs
│
├── Unity/                                 ← NodeGraph.Unity.asmdef（Editor-only）
│   ├── UnityGraphRenderer.cs             ← 引擎原生渲染器（Zero-Matrix 模式，消费 GraphFrame）
│   ├── CanvasCoordinateHelper.cs         ← 画布坐标辅助（替代 GUI.BeginClip）
│   ├── UnityEditContext.cs
│   ├── UnityPlatformInput.cs
│   ├── UnityTypeConversions.cs
│   └── Persistence/
│       ├── SerializedTypes.cs
│       ├── GraphAsset.cs
│       ├── GraphAssetConverter.cs
│       └── UnityGraphPersistence.cs
```

**业务编辑器**（独立于 NodeGraph 框架，位于 `Extensions/CombatBlueprint/`）：

```
Extensions/CombatBlueprint/                ← CombatBlueprint.asmdef（Editor-only）
├── SpawnBlueprintWindow.cs                ← 战斗蓝图编辑器窗口
├── SpawnNodeTypes.cs                      ← 6 种节点类型注册
├── SpawnNodeData.cs                       ← 业务数据定义
├── SpawnTaskContentRenderer.cs            ← 内容渲染器
├── SpawnTransitionLabelRenderer.cs        ← 连线标签渲染器
├── SpawnPlanGraphConverter.cs             ← SpawnPlanAsset ↔ Graph 转换
└── RecentBlueprints.cs                    ← 最近打开文件记录
```

### 21.1 程序集依赖关系

```
NodeGraph.Math          ← 零依赖
NodeGraph.Core          ← 依赖 Math
NodeGraph.Commands      ← 依赖 Core
NodeGraph.Abstraction   ← 依赖 Core, Math
NodeGraph.View          ← 依赖 Core, Commands, Abstraction, Math
NodeGraph.Serialization ← 依赖 Core, Abstraction, Math
NodeGraph.Layout        ← 依赖 Core, Math
NodeGraph.Unity         ← 依赖以上全部 + UnityEditor
CombatBlueprint         ← 依赖以上全部 + StageDesigner.Editor（业务层）
```

---

## 22. 跨引擎适配指南

### 22.1 接入新引擎的步骤（v2.0）

1. **实现 `IPlatformInput`** — 适配引擎的输入事件（鼠标/键盘/修饰键）
2. **实现 `IEditContext`** — 用引擎的 UI 控件实现节点内嵌编辑
3. **实现引擎原生 Renderer** — 消费 `GraphFrame`，用引擎最优技术绘制节点/连线/端口
4. **创建宿主窗口** — 引擎的窗口/面板类，驱动 ProcessInput → BuildFrame → Render

> `IDrawContext` 已移除，不再需要实现。

### 22.2 Unity 适配（v2.1）

#### Zero-Matrix 渲染模式

Unity 渲染器采用 **Zero-Matrix** 模式：不设置 `GUI.matrix`，所有画布坐标通过辅助方法手动转换为窗口坐标后再绘制。

**原因**：`Handles` API（`DrawSolidDisc`、`DrawBezier` 等）在 `GUI.matrix` 包含缩放分量时，
与 `GUI.BeginClip` / `EditorWindow` 的坐标系交互存在不可预测的偏移，导致端口圆圈的
渲染位置与命中检测位置不匹配。

**坐标转换公式**：
```
windowPos  = canvasPos  * zoom + pan + screenOffset
windowSize = canvasSize * zoom
其中 screenOffset = graphRect.position（画布区域在 EditorWindow 中的偏移）
```

**渲染器辅助方法**：
- `C2W(cx, cy)` — 画布点 → 窗口点
- `C2WRect(r)` — 画布矩形 → 窗口矩形
- `S(canvasSize)` — 画布尺寸 → 窗口尺寸（标量缩放）
- `ScaledFontSize(base)` — 缩放后的字号

#### 性能优化

| 优化项 | 问题 | 解决方案 |
|--------|------|----------|
| **NeedsRepaint 时序** | `BuildFrame` 开头重置标记，导致 `Repaint()` 永远不被调用 | 将重置移到 `ProcessInput` 开头 |
| **事件分流** | 每次 `OnGUI` 都执行完整流水线（2~3+ 次/帧） | 输入事件只做 `ProcessInput`，`Repaint` 才做 `BuildFrame` + `Render` |
| **GUIStyle 缓存** | 每帧每节点 `new GUIStyle()` 产生 GC 压力 | 4 种样式对象只创建一次，后续仅更新 `fontSize` |
| **工具栏去重** | `DrawToolbar()` 被调用两次（一次被遮挡层覆盖） | 只在遮挡层之上绘制一次 |
| **wantsMouseMove** | 鼠标移动时不触发 `OnGUI` | 在 `OnEnable` 中启用 |

#### 宿主窗口模板

```csharp
public class NodeGraphEditorWindow : EditorWindow
{
    private GraphViewModel _viewModel;
    private UnityGraphRenderer _renderer;
    private UnityEditContext _editCtx;
    private UnityPlatformInput _input;
    private CanvasCoordinateHelper _coordinateHelper;
    
    void OnEnable()
    {
        wantsMouseMove = true; // 启用鼠标移动事件，提升交互流畅度
    }
    
    void OnGUI()
    {
        _coordinateHelper.SetGraphAreaRect(graphRect);
        _input.Update(Event.current, _coordinateHelper);
        
        var eventType = Event.current.type;
        
        // ── 输入处理（仅输入事件）──
        if (eventType != EventType.Repaint && eventType != EventType.Layout)
        {
            _viewModel.PreUpdateNodeSizes();
            _viewModel.ProcessInput(_input);
        }
        
        // ── 渲染（仅 Repaint 事件）──
        if (eventType == EventType.Repaint)
        {
            _viewModel.Update(deltaTime);
            var frame = _viewModel.BuildFrame(viewport);
            // Zero-Matrix：传入 screenOffset，渲染器手动转换坐标
            _renderer.Render(frame, _viewModel.Theme, viewport,
                _editCtx, graphRect.position);
        }
        
        if (_viewModel.NeedsRepaint)
            Repaint();
    }
}
```

### 22.3 Godot 4 适配（v2.0）

```csharp
public partial class NodeGraphPanel : Control
{
    private GraphViewModel _viewModel;
    private GodotGraphRenderer _renderer;     // 用 StyleBoxFlat/CanvasItem
    private GodotEditContext _editCtx;
    private GodotPlatformInput _input;
    
    public override void _Draw()
    {
        var frame = _viewModel.BuildFrame(GetRect().ToNodeGraph());
        _renderer.Render(this, frame, _viewModel.Theme);  // draw_style_box, draw_circle 等
    }
    
    public override void _GuiInput(InputEvent @event)
    {
        _input.Update(@event);
        _viewModel.ProcessInput(_input);
        _viewModel.Update(deltaTime);
        QueueRedraw();
    }
}
```

### 22.4 Dear ImGui 适配（v2.0）

```csharp
public class NodeGraphImGuiWindow
{
    private GraphViewModel _viewModel;
    private ImGuiGraphRenderer _renderer;     // 用 ImDrawList
    private ImGuiEditContext _editCtx;
    private ImGuiPlatformInput _input;
    
    public void Render()
    {
        ImGui.Begin("Node Graph");
        _input.Update();
        _viewModel.ProcessInput(_input);
        _viewModel.Update(deltaTime);
        
        var frame = _viewModel.BuildFrame(ImGui.GetContentRegionAvail());
        _renderer.Render(frame, _viewModel.Theme);  // AddRectRounded, AddBezierCubic 等
        
        ImGui.End();
    }
}
```

---

## 23. 实施路线图

### Phase 1: 基础框架 ✅

- [x] Math 模块（Vec2 / Rect2 / Color4 / BezierMath）
- [x] Core 模块（Graph / Node / Port / Edge / GraphSettings / GraphDecoration）
- [x] NodeTypeRegistry + TypeCompatibilityRegistry
- [x] DefaultConnectionPolicy + GraphAlgorithms
- [x] Commands 模块（ICommand / CommandHistory / CompoundCommand / 内置命令 7 个）
- [x] Abstraction 接口定义（IEditContext / IPlatformInput / IGraphPersistence / INodeContentRenderer / IEdgeLabelRenderer / IGraphValidator / IGraphSerializer）

### Phase 2: 视图与交互 ✅

- [x] GraphViewModel + SelectionManager
- [x] PanZoomController
- [x] NodeDragHandler + ConnectionDragHandler
- [x] MarqueeSelectionHandler（含 Shift/Ctrl 修饰键）
- [x] NodeInteractionHandler（双击/折叠/展开）
- [x] KeyBindingManager + 默认快捷键

### Phase 3: Unity 适配 ✅

- [x] UnityDrawContext（IMGUI/GL 渲染）
- [x] UnityEditContext（EditorGUI 控件）
- [x] UnityPlatformInput（Event 系统适配）
- [x] UnityTypeConversions（Vec2 ↔ Vector2 / Rect2 ↔ Rect / Color4 ↔ Color）
- [x] NodeGraphEditorWindow 基类
- [x] Unity 持久化（GraphAsset / SerializedTypes / GraphAssetConverter / UnityGraphPersistence）

### Phase 4: 高级功能 ✅

- [x] SearchMenuModel（节点创建搜索菜单）
- [x] NodeSearchModel（图内节点搜索）
- [x] MiniMapRenderer（小地图）
- [x] NodeGraph.Serialization 模块（IUserDataSerializer / JsonGraphModel / JsonGraphSerializer / SimpleJson）

### Phase 5: 自动布局 ✅

- [x] ILayoutAlgorithm 接口
- [x] TreeLayout（树形布局，BFS 分层）
- [x] LayeredLayout（分层布局，简化 Sugiyama + 重心排序）
- [x] ForceDirectedLayout（力导向布局，Fruchterman-Reingold）
- [x] LayoutHelper（ApplyLayout / InterpolateLayout / CenterLayout）

### Phase 6: 刷怪蓝图集成 ✅

- [x] SpawnNodeTypes（6 种节点类型：Start / SpawnTask / Join / Delay / Branch / SubPlan）
- [x] SpawnNodeData（业务数据：SpawnTaskNodeData / DelayNodeData / BranchNodeData / TransitionEdgeData / SubPlanNodeData）
- [x] SpawnTaskContentRenderer + DelayContentRenderer + BranchContentRenderer
- [x] SpawnTransitionLabelRenderer（连线过渡条件标签）
- [x] SpawnPlanGraphConverter（SpawnPlanAsset ↔ Graph 双向转换）
- [x] SpawnBlueprintWindow（战斗蓝图编辑器窗口，现已迁移至 `Extensions/CombatBlueprint/`）

### Phase 7: 视觉升级（阶段一）✅

- [x] NodeVisualTheme 主题系统（纯 C#，集中管理 ~40 个视觉参数）
- [x] 节点阴影（多层半透明叠加模拟柔和投影）
- [x] 标题栏/主体分隔线 + 节点外边框
- [x] 端口样式升级（空心环=未连接 / 实心圆=已连接 + 深色外圈）
- [x] 连线跟随源端口类型着色
- [x] 选中外发光效果（多层渐隐 + 实边框）
- [x] 节点自动尺寸计算（基于端口数+标题宽度+内容区）
- [x] 网格/小地图主题化

### Phase 8: GraphFrame 架构重构 ✅

目标：将渲染职责从纯 C# 层分离到引擎原生层，支持多蓝图类型定制。

- [x] **8.1** GraphFrame 数据类型（GraphFrame / NodeFrame / EdgeFrame / PortFrame / OverlayFrame / BackgroundFrame / MiniMapFrame）
- [x] **8.2** IGraphFrameBuilder 接口 + DefaultFrameBuilder（从 GraphViewModel 提取现有渲染逻辑）
- [x] **8.3** BlueprintProfile 配置包（Theme + FrameBuilder + NodeTypes + Features）
- [x] **8.4** GraphViewModel 重构（BuildFrame() 替代 Render()，移除 IDrawContext 依赖）
- [x] **8.5** UnityGraphRenderer（Zero-Matrix 模式，消费 GraphFrame，使用 Handles/IMGUI 原生绘制）
- [x] **8.6** SpawnBlueprintWindow 迁移到新架构 + 清理旧 IDrawContext/UnityDrawContext 代码（窗口现位于 `Extensions/CombatBlueprint/`）

### Phase 9: 视觉细节与交互修复 ✅

- [x] **9.1** 端口圆圈内嵌到节点矩形内（PortInset = 10f）
- [x] **9.2** 标题文字靠左垂直居中（TitlePaddingLeft）
- [x] **9.3** Multiple Input 连线端点连到各自槽位圆圈（GetEdgeTargetPosition）
- [x] **9.4** Multiple 端口展开改为目标槽位数（方案B：targetSlots = Max(用户目标, edgeCount+1, 2)）
- [x] **9.5** 框选微小蓝框闪现修复（起点到终点距离阈值 5px）

### Phase 10: 性能优化 ✅

- [x] **10.1** NeedsRepaint 时序修复（重置移到 ProcessInput 开头，BuildFrame 不再清除）
- [x] **10.2** wantsMouseMove 启用（OnEnable 中设置，确保鼠标移动时触发 OnGUI）
- [x] **10.3** OnGUI 事件分流（输入事件只做 ProcessInput，Repaint 才做 BuildFrame + Render）
- [x] **10.4** GUIStyle 缓存（4 种样式对象只创建一次，后续仅更新 fontSize）
- [x] **10.5** 工具栏绘制去重（移除被遮挡层覆盖的冗余绘制）

### Phase 11: SubGraph 扁平化内联框实现（v2.3）✅

- [x] **11.1** GraphContainer 基类重构 + SubGraphFrame 数据模型
  - 重构 `GraphDecoration`（`Position+Size` → `Rect2 Bounds`）
  - 新增 `GraphContainer` 抽象基类（`Title` + `HashSet<string> ContainedNodeIds` + `AutoFit()`）
  - 重构 `NodeGroup` 继承 `GraphContainer`（`ContainedNodeIds` 从 List 升级为 HashSet）
  - 重构 `GraphComment` 适配新 `Bounds` 属性
  - 新增 `SubGraphFrame : GraphContainer`（`IsCollapsed` + `RepresentativeNodeId` + `SourceAssetId?`）
  - `Graph.cs` 新增 `_subGraphFrames` 管理 + `AllContainers` 属性
  - 适配所有引用 `NodeGroup.ContainedNodeIds`（List→HashSet）和 `Position/Size`→`Bounds` 的代码
- [x] **11.2** DecorationFrame 渲染帧 + FrameBuilder 装饰层支持
  - 新增 `DecorationFrame` + `DecorationKind` 渲染帧类型
  - `GraphFrame` 新增 `List<DecorationFrame> Decorations`
  - `DefaultFrameBuilder.BuildFrame()` 扩展：遍历 `AllContainers` + `Comments` 生成 DecorationFrame
  - `UnityGraphRenderer` 新增 `DrawDecoration()` 方法（背景矩形 + 标题栏 + 边框）
  - 渲染顺序调整：Background → Decorations → Edges → Nodes → Overlays → MiniMap
- [x] **11.3** 折叠/展开切换
  - `DefaultFrameBuilder` 渲染决策：折叠时跳过 ContainedNodeIds 节点和内部边
  - SubGraphFrame 折叠按钮交互（点击 [▼]/[▶] 切换）
  - `ToggleSubGraphCollapseCommand`（支持 Undo/Redo）
- [x] **11.4** 代表节点（RepresentativeNode）+ 边界端口
  - `"__SubGraphBoundary"` 节点类型注册
  - SubGraphFrame 创建时自动生成 RepresentativeNode（含默认 In/Out 端口）
  - 折叠渲染：RepresentativeNode 生成 NodeFrame（显示 SubGraphFrame 标题 + 边界端口）
  - 展开渲染：RepresentativeNode 隐藏，其端口由 DecorationFrame.BoundaryPorts 渲染到框边缘
  - 连线约束：确保边界端口的 Edge 在折叠/展开状态下都能正确渲染
- [x] **11.5** 子图资产拷贝实例化
  - `SubGraphInstantiator`：深拷贝子图资产的节点和边到父 Graph
  - 自动创建 RepresentativeNode + SubGraphFrame 包裹拷贝后的节点
  - ID 重映射（避免与父图已有节点冲突）
- [x] **11.6** Command 支持 + Undo
  - `CreateSubGraphCommand`（创建 SubGraphFrame + RepresentativeNode + 内部节点/边）
  - `RemoveSubGraphCommand`（移除 SubGraphFrame 及其所有内部节点/边 + RepresentativeNode）
  - `MoveSubGraphCommand`（整体移动框 + 内部节点）
  - 现有 `RemoveNodeCommand` 需适配：删除 ContainedNode 时从 SubGraphFrame 中移除
- [x] **11.7** 序列化/持久化支持
  - `JsonGraphSerializer` 扩展：SubGraphFrame 序列化/反序列化
  - Unity `GraphAsset` / `UnityGraphPersistence` 扩展：SubGraphFrame 持久化

### Phase 12: 条件描述系统实现（v2.4）✅

- [x] **12.1** 框架层：`ConditionDescriptor` 类型层次（LeafCondition / AndCondition / OrCondition / NotCondition + Clone）
- [x] **12.2** 框架层：`ConditionTypeDef` + `ConditionParamDef` + `IConditionTypeRegistry` + 默认实现
- [x] **12.3** 框架层：条件序列化（`ConditionModel` 中间模型 + `ConditionSerializer` 双向转换 + `SimpleJsonCondition`）
- [x] **12.4** 业务层迁移：`TransitionEdgeData` 从 `TransitionType` 枚举升级为 `ConditionDescriptor?`（旧枚举已删除）
- [x] **12.5** 业务层：Inspector 条件编辑器（`ConditionTypeChoice` 下拉 + 参数编辑）+ 标签渲染器升级
- [ ] **12.6** `ConditionCompiler`（ConditionDescriptor → CompiledCondition 编译器）— 待后续按需实现
- [ ] **12.7** `CompiledCondition` 运行时数据结构（位掩码 + 扁平 ConditionNode[] 数组）— 待后续按需实现
- [ ] **12.8** `IRuntimeConditionContext` + 求值器实现（D+B 混合求值）— 待后续按需实现

### Phase 13: FrameBuilder 扩展（v2.4）✅

- [x] **13.1** 提取 `BaseFrameBuilder` 基类（通用流程 + `IsHorizontalLayout` / `ComputeEdgeRoute` / `GetBoundaryPortPosition` virtual 差异点）
- [x] **13.2** `DefaultFrameBuilder` 重构为 `BaseFrameBuilder` 薄子类（水平布局，零重写）
- [x] **13.3** `BehaviorTreeFrameBuilder`（垂直布局 + `BezierMath.ComputeVerticalTangents`）
- [x] **13.4** `StateMachineFrameBuilder`（水平布局 + 加强弧度切线，预留箭头扩展）
- [x] **13.5** `BezierMath.ComputeVerticalTangents` 新增垂直切线计算方法

### Phase 14: CombatBlueprint 独立化 ✅ / SDK 🔲

- [x] **14.0** 战斗蓝图编辑器从 `NodeGraph/SpawnBlueprint/` 迁移到 `Extensions/CombatBlueprint/`（独立模块，namespace `CombatBlueprint`）
- [ ] **14.1** 通用蓝图编辑器 SDK 提取（从 SpawnBlueprintWindow 抽象可复用部分）
- [ ] **14.2** 技能蓝图集成验证
- [ ] **14.3** 对话树集成验证

### Phase 15: 视觉打磨 — 纯矢量渲染（v2.5）✅

全面采用纯矢量绘制，零纹理依赖，任意缩放无伪影。

- [x] **15.A** 节点圆角（`DrawFilledRoundedRect`：十字矩形 + 四角 `DrawSolidDisc`）
- [x] **15.B** 标题栏渐变（4 条 `Color.Lerp` 色带，标题色 → 主体色平滑过渡）
- [x] **15.C** 连线流动动画（3 个沿贝塞尔曲线移动的小圆点，`EditorApplication.timeSinceStartup` 驱动）
- [x] **15.D** 连线标签药丸背景（`DrawFilledRoundedRect` 胶囊形 + `DrawAAPolyLine` 圆弧边框）
- [x] **15.E** 端口悬停高亮（`HoveredPortId` + `HoveredPortSlotIndex` 槽位级追踪 + 矢量光环 `DrawSolidCircleRing`）
- [x] **15.F** 阴影高斯模糊（指数衰减 alpha + 矢量圆角矩形多层叠加）
- [x] **15.G** 废弃 `RoundedRectTexture.cs`（已标记 `[Obsolete]`，无代码引用）

**矢量绘制工具方法**：

| 方法 | 用途 | 原理 |
|------|------|------|
| `DrawFilledRoundedRect` | 节点主体/阴影/药丸标签 | 十字矩形(`EditorGUI.DrawRect`) + 四角圆盘(`Handles.DrawSolidDisc`) |
| `DrawSolidCircleRing` | 端口悬停光环 | 32 段 `Handles.DrawAAPolyLine` 折线逼近圆 |
| `DrawRoundedBorder` | 节点边框/选中发光 | 4 条直线 + 4 个圆弧(`DrawAAPolyLine`) |
| `DrawScreenArc` | 药丸边框半圆弧 | 多段 `DrawAAPolyLine` 折线 |

**为什么不用纹理**：早期版本使用 `RoundedRectTexture` + GUIStyle 9-slice 绘制圆角，但存在以下问题：
1. 9-slice 在某些尺寸下四角变形（不规则形状）
2. 纹理缓存管理复杂（域重载失效、GC 压力）
3. `Handles.DrawWireDisc` 带 thickness 参数会产生虚线

纯矢量方案完全避免以上问题，且性能更优（无纹理创建/上传开销）。

### 待清理项

- [x] ~~移除废弃文件：`IDrawContext.cs`、`UnityDrawContext.cs`（v2.0 已不再使用）~~ ✅ 已在 Phase 10 后清理
- [x] ~~移除旧 SubGraph 导航代码：`SubGraphNode.cs`、`_graphStack`、`EnterSubGraph()`、`ExitSubGraph()`~~ ✅ 已清理
- [x] ~~删除 `RoundedRectTexture.cs`（纯矢量方案完全替代，Phase 15 后无代码引用）~~ ✅ 已删除
- [x] ~~删除 `NodeGraphEditorWindow.cs`（早期通用窗口，被 `SpawnBlueprintWindow` 完全取代，零引用）~~ ✅ 已删除
- [x] ~~`SpawnBlueprint/` 迁移至 `Extensions/CombatBlueprint/`（业务代码从框架目录分离）~~ ✅ 已迁移
- [ ] 评估 `IGraphValidator` 是否需要实现（Phase 1 设计文档提及但从未创建）
- [ ] 评估 `ContextMenuHandler` / `GroupDragHandler` 是否需要从业务层提取为框架内置 Handler

---

## 24. 错误处理策略

| 场景 | 策略 | 示例 |
|------|------|------|
| 连接被拒绝 | 返回 `ConnectionResult` 枚举（不抛异常） | `CanConnect()` → `CycleDetected` |
| 查找不存在的节点 | 返回 `null`（不抛异常） | `FindNode("xxx")` → `null` |
| 序列化格式错误 | 返回 `null` + 日志警告 | `Deserialize(badJson)` → `null` |
| 渲染异常 | try-catch 隔离，不崩溃编辑器 | 单个节点渲染失败不影响其他节点 |
| API 参数错误（空 ID、null 参数） | 抛 `ArgumentException` | `AddNode(null, ...)` → 异常 |
| 复合命令部分失败 | 整体回滚（原子性） | 复合 Undo 要么全成功要么全回滚 |

**原则**：图操作层面不抛异常（返回结果枚举或 null），API 误用层面抛异常（帮助开发者发现 bug）。

---

## 25. 技术决策汇总

| 决策 | 选择 | 理由 |
|------|------|------|
| .NET 版本 | .NET Standard 2.1 | Nullable + Default Interface Methods |
| 项目组织 | Unity asmdef（纯 C# 核心 + 引擎适配层） | 简单直接，未来需跨引擎时再抽 .csproj |
| ID 生成 | 完整 GUID (`Guid.NewGuid()`) | 多人协作零冲突，Git 合并友好 |
| 数学类型 | 自定义 Vec2/Rect2/Color4 | 零依赖，隐式转换 |
| 序列化格式 | JSON（IGraphSerializer） | 可读、跨引擎、调试友好 |
| 持久化 | 两层分离（IGraphSerializer + IGraphPersistence） | 各引擎可独立选择策略，Unity 原生 SO 不经 JSON |
| API 分层 | 双层 API（Graph 低层 + GraphViewModel 高层命令） | 低层供 Command/反序列化使用，高层自动进 Undo 栈 |
| 渲染主循环 | 引擎宿主驱动（ProcessInput/Update/BuildFrame） | 框架不拥有主循环，适配所有引擎 |
| 渲染架构 | GraphFrame 渲染描述（v2.0） | 纯 C# 层输出数据，引擎层 100% 自由选择渲染技术 |
| 蓝图配置 | BlueprintProfile + IGraphFrameBuilder | 蓝图类型差异与引擎渲染差异正交解耦 |
| IDrawContext | **已移除**（v2.0 不再使用） | GraphFrame 完全取代 |
| Unity 坐标系 | Zero-Matrix 模式（v2.1） | 不设置 GUI.matrix，手动 C2W() 转换坐标，消除 Handles+缩放的偏移问题 |
| NeedsRepaint 时序 | ProcessInput 开头重置（v2.1） | 确保处理器的重绘请求存活到窗口代码检查 |
| OnGUI 事件分流 | 输入/渲染分离（v2.1） | 避免每次 OnGUI 调用都执行完整流水线 |
| Multiple 端口槽位 | targetSlots 方案（v2.1） | 连接消耗空位不增长，点击"+"手动扩展 |
| 编辑控件抽象 | IMGUI 风格接口 | Unity/ImGui 天然一致 |
| 图拓扑 | 可配置（DAG/有向图/无向图） | 支持多种业务场景 |
| 错误处理 | 图操作返回结果枚举/null，API 误用抛异常 | 图操作不崩溃，开发错误及早发现 |
| 框架名称 | NodeGraph | 不限于 DAG |
| 目录位置 | Assets/Extensions/NodeGraph/ | 独立模块 |
| SubGraph 方案 | 扁平化内联框 + 代表节点（v2.3） | 所有节点在同一 Graph，SubGraphFrame 继承 GraphContainer，RepresentativeNode 承载边界端口 |
| SubGraph 实例化 | 拷贝模式（v2.2） | 深拷贝子图资产节点到父图，不影响原始资产 |
| SubGraph 边界端口 | RepresentativeNode 方案（v2.3） | 真实 Node 承载边界端口，Port.NodeId 不变，零侵入 Port/Edge 模型 |
| 装饰元素层次 | GraphContainer 抽象基类（v2.3） | GraphDecoration → GraphContainer(NodeGroup/SubGraphFrame) + GraphComment |
| 装饰元素 Bounds | Rect2 Bounds（v2.3） | 替代 Position+Size，语义更直接 |
| 容器节点集合 | HashSet\<string\>（v2.3） | O(1) 查找，FrameBuilder 折叠判断高频使用 |
| 装饰层渲染 | DecorationFrame（v2.3） | GraphFrame 新增 Decorations 列表，统一 Group/SubGraph/Comment 的渲染描述 |
| 条件放置位置 | Edge-only（v2.2） | 框架层条件只在 Edge 上，Node 条件是业务层自定义节点的内部实现 |
| 条件数据结构 | ConditionDescriptor 组合树（v2.2） | 框架层管 AND/OR/NOT 组合，业务层通过 IConditionTypeRegistry 注册具体语义 |
| 条件脚本化 | 脚本是 LeafCondition 类型，非框架基础设施（v2.2） | Lua/Python 仅编辑器预览，运行时纯 C#（Quantum 确定性限制） |
| 运行时条件格式 | D+B 混合：位掩码 + 扁平数组（v2.2） | 80% 布尔条件走位掩码（2-3 CPU 指令），复杂条件走扁平 ConditionNode[] |
| 运行时变量引用 | 编译期名称→整数索引（v2.2） | ConditionVariableRegistry 导出映射，运行时零字符串零 GC |
| 异步条件 | 执行器轮询模型（v2.2） | 条件求值器无状态，异步由图执行器每 Tick 轮询天然支持 |
| 条件返回值 | bool（v2.2），预留 ConditionResult 三态扩展 | 当前 HFSM 用 true/false，未来 BT 可扩展 Running 状态 |
