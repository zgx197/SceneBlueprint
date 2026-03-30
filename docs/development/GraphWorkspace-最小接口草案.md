# Graph Workspace 最小接口草案（已整合更新）

> 版本：v0.2
> 日期：2026-03-29
> 状态：持续更新，作为当前仓库 Graph Workspace 的主开发文档

---

## 1. 文档定位（已更新）

本文档现在承担两份职责：

- 描述当前仓库 `Graph Workspace` 的最小架构边界
- 作为当前 `Graph Canvas` 交互规则的唯一主文档

原来的 `GraphCanvas-交互设计清单.md` 已经完成了它第一轮“定规则”的职责，因此不再单独维护正文，后续统一收敛到本文。

这份文档的目标不是一次性定死最终商业级框架，而是为当前仓库提供一版足够稳定的开发依据：

- 能指导我们继续实现当前 Graph Workspace
- 能明确哪些旧 nodegraph 设计应该保留
- 能明确哪些能力需要在外部编辑器里重做
- 能避免继续把复杂逻辑直接堆进 React 组件

---

## 2. 当前核心判断（已确认）

当前仓库里的 Graph Workspace 已经具备“最小可交互画布”骨架，但还没有真正对应上旧版 `com.zgx197.nodegraph` 的完整体系。

当前状态更准确地说是：

- 已经有了 `document / state / commands / definitions / runtime / interaction / frame / ui / binding / storage / serialization` 的最小分层
- 已经有了节点拖拽、端点连线、边选择、右键菜单、最小命令闭环
- 已经建立第一轮宿主无关的 `Runtime / Interaction Handler / Frame Builder / Graph Frame` 中间层
- 已经建立正式 Graph 文件、宿主文件持久化、输入抽象与最小编辑协议，但离旧版 nodegraph 的完整体系仍有明显距离

所以我们后续的目标不是继续把 `GraphCanvas.tsx` 做成一个越来越大的组件，而是逐步把它拆回一套稳定结构。

---

## 3. 旧 nodegraph -> 当前仓库正式映射表（已整理）

下面这张表用于明确：旧版 `com.zgx197.nodegraph` 的每一层，在当前外部编辑器仓库里应该如何处理。

### 3.1 映射决策说明

- `保留`：设计方向成立，应继续沿用，只是换宿主技术栈
- `重做`：核心思想成立，但旧实现绑定 Unity / IMGUI / C# 细节，必须在当前仓库重新实现
- `不再继承`：旧实现对当前外部编辑器意义不大，或应下沉到更外层适配，不再沿旧结构直接延续

### 3.2 正式映射表

| 层次 | 旧 nodegraph 代表能力 | 当前仓库落点 | 决策 | 说明 |
|----------|--------------|--------------|----------|--------------|
| 核心图文档层 | `Graph / Node / Port / Edge` | `document/graphDocument.ts` | 保留 | 这是最基础的宿主无关能力，当前已经有最小版本，但还要继续补强索引、语义约束与稳定模型。 |
| 节点定义层 | `NodeTypeDefinition / NodeTypeRegistry` | `definitions/graphDefinitions.ts` | 保留 | 方向完全正确，应继续作为业务节点注册入口。 |
| 连接规则层 | `IConnectionPolicy / IConnectionValidator / TypeCompatibilityRegistry` | 当前仅散落在 controller 中 | 重做 | 这是后续复杂 SceneBlueprint 必须成立的独立层，不能继续写死在 `GraphWorkspaceController.ts` 里。 |
| 图设置与事件层 | `GraphSettings / GraphEvents / GraphBehavior` | 当前未建立 | 重做 | 外部编辑器同样需要稳定的图行为配置、事件广播与拓扑约束。 |
| 命令系统 | `ICommand / CompoundCommand / CommandHistory` | `commands/graphCommands.ts` | 重做 | 当前快照式 bus 只够 MVP，用于长期演进的命令对象、复合命令、命令合并还未建立。 |
| 选择管理层 | `SelectionManager` | `state/graphViewState.ts` + controller | 重做 | 当前只有最小单选状态，后续要补主选中、多选、追加/移除、全选、统一选择源。 |
| 交互处理器层 | `ConnectionDragHandler / NodeDragHandler / MarqueeSelectionHandler / ContextMenuHandler / PanZoomController` | 当前主要在 `ui/GraphCanvas.tsx` 内 | 重做 | 这些能力不该继续聚集在 React 组件中，需要抽成宿主无关交互层。 |
| 视图主入口层 | `GraphViewModel` | `GraphWorkspaceController.ts` | 重做 | 当前 controller 是最小雏形，但还不是旧版那种真正的编辑主入口。 |
| 渲染帧层 | `IGraphFrameBuilder / GraphFrame / NodeFrame / EdgeFrame / PortFrame` | 当前仅有 `graphUiAdapter.ts` | 重做 | 这是当前最关键缺口之一，后续 Graph Canvas、Scene Viewport、测试都需要这个中间层。 |
| 主题与节点渲染信息 | `NodeVisualTheme / NodeRenderInfo` | 当前只有局部 CSS / UI 映射 | 重做 | 需要建立统一主题与节点渲染元数据，而不是继续把视觉规则散落在 CSS 和 JSX 里。 |
| 节点内容编辑协议 | `INodeContentRenderer / IEditContext` | 当前仅 Inspector 读取选择态 | 重做 | 外部编辑器同样需要“节点内容定义 -> UI 编辑器 -> 数据回写”的清晰接口。 |
| 搜索与创建菜单 | `SearchMenuModel / NodeSearchModel` | 当前仅右键新增最小浏览 | 重做 | 节点创建搜索、图内节点搜索后续都要恢复为独立能力。 |
| 小地图 | `MiniMapRenderer / MiniMapFrame` | 当前未建立 | 重做 | 这是成熟图编辑器常见基础能力，适合在视图中间层建立。 |
| 自动布局 | `ILayoutAlgorithm / TreeLayout / LayeredLayout / ForceDirectedLayout` | 当前未建立 | 保留 | 能力应保留，但可以晚于核心交互落地。 |
| 序列化与持久化 | `JsonGraphSerializer / GraphDto / DefaultGraphPersister / IUserDataSerializer` | 当前仅 `graphWorkspaceStorage.ts` | 重做 | 当前 localStorage 草稿保存不等于正式图序列化，需要未来独立格式与版本化。 |
| 宿主抽象层 | `IPlatformInput / ITextMeasurer / IGraphPersistence` | 当前未建立 | 重做 | 外部编辑器更需要这一层，用于隔离 Web UI、Tauri、Rust 宿主与未来引擎桥接。 |
| Unity 宿主渲染与编辑适配 | `UnityGraphRenderer / UnityEditContext / UnityPlatformInput` | 当前仓库无对应 | 不再继承 | 这些是旧包对 Unity 的专属实现，当前应改为外部编辑器宿主适配层。 |
| Unity Inspector 面板耦合实现 | 旧 Editor 内嵌 Inspector 细节 | 当前 `binding/graphInspectorBinding.ts` | 不再继承 | 我们只保留“统一选择态 -> Inspector 投影”的思路，不沿用 Unity 面板实现方式。 |
| 快捷键管理器 | `KeyBinding / KeyBindingManager` | 当前仅少量键盘处理 | 保留 | 方向应保留，但要作为独立输入层能力重建。 |
| 分组 / 注释 / 子图容器 | `GraphDecoration / NodeGroup / SubGraphFrame / GraphComment` | `graphDocument.ts` 中只有最小占位结构 | 保留 | 概念必须保留，但当前还没有进入真正可用状态。 |
| 诊断覆盖与调试态 | 节点/边 Overlay、状态标签 | 当前未建立 | 保留 | 这对后续运行时调试和引擎联动很有价值，应继续纳入边界。 |
| 测试样例与快速回归 | `NodeGraphQuickTest` | 当前未建立 graph 专项测试 | 保留 | SceneBlueprint 后续必须补图编辑专项测试，而不是只靠手动看 UI。 |

### 3.3 当前最关键的三条结论

从映射结果看，当前最该优先推进的是：

1. 把旧版 `GraphViewModel + Handlers + FrameBuilder + GraphFrame` 这条中间骨架重新建立起来。
2. 把连接规则、命令对象、选择管理从 UI 组件里继续抽离。
3. 把当前 local draft 级保存，升级为真正的图序列化与宿主无关持久化边界。

---

## 4. 当前仓库建议保留的最小层次（已更新）

当前仓库第一阶段已经长出了一版最小层次，后续不建议推翻，只建议继续补强：

```text
src/features/graph/
  GraphPanel.tsx
  GraphWorkspaceController.ts
  document/
  state/
  commands/
  definitions/
  ui/
  binding/
  storage/
```

各层当前和后续的推荐职责如下：

- `document/`
  负责图本身是什么。
  包含节点、端口、边、分组、注释、子图等稳定编辑态数据。

- `definitions/`
  负责有哪些节点可创建。
  包含节点类型、分类、默认端口、默认 payload、搜索元数据。

- `state/`
  负责当前怎么编辑。
  包含视口、选择态、连接预览、悬停、框选、拖拽等瞬时态。

- `commands/`
  负责结构如何变化。
  后续应从“快照总线”继续升级为“语义命令对象 + undo/redo + compound”。

- `ui/`
  负责具体展示。
  当前是手写 `GraphCanvas`，后续即使引入第三方图库，也应该仍然只把它放在这一层。

- `binding/`
  负责把 Graph 选择态投影到 Inspector、Scene、工具栏等其他区域。

- `storage/`
  负责草稿保存、正式存档、版本迁移、宿主文件系统持久化。

---

## 5. 当前仓库 Graph Workspace 最小接口草案（已更新）

这一部分不再追求把 TypeScript 接口细节全部写满，而是收敛成更稳定的职责边界说明。

### 5.1 Document 层（保留，需继续补强）

最小职责：

- 提供 `GraphDocument / GraphNode / GraphPort / GraphEdge`
- 提供 `groups / comments / subgraphs` 的稳定数据入口
- 不承载 React 状态
- 不承载宿主对象引用
- 不承载鼠标悬停、拖拽预览等瞬时态

当前结论：

- 这一层已经成立
- 但还没有补齐旧版 `GraphSettings / GraphEvents / Decoration / SubGraphFrame` 的语义能力

### 5.2 Definitions 层（保留）

最小职责：

- 注册节点类型
- 提供默认端口结构
- 提供节点显示名、分类、摘要
- 提供节点创建时的默认 payload

当前结论：

- 方向正确
- 后续需要继续补节点显示模式、颜色、搜索、业务描述等元信息

### 5.3 State 层（保留，需继续拆细）

最小职责：

- 保存视口状态
- 保存选择态
- 保存连接预览态
- 保存悬停、拖拽、框选等交互态

当前结论：

- 基础结构已成立
- 但 `marqueeSelection` 还未真正进入行为闭环
- 多选、主选中、批量选择语义还未建立

### 5.4 Commands 层（重做升级）

最小职责：

- 所有结构性编辑统一走命令
- 支持 `undo / redo`
- 未来支持复合命令、拖拽合并、日志、回放、自动保存

当前结论：

- 当前最小命令集已经能支撑基础编辑
- 但离旧版成熟命令系统还有明显差距

### 5.5 Workspace Controller 层（重做升级）

最小职责：

- 拼装 `document / definitions / state / commands / storage / binding`
- 对 UI 暴露单一读取入口
- 对 UI 暴露统一命令入口

当前结论：

- 当前 `GraphWorkspaceController.ts` 已经是主入口雏形
- 但仍然承担了过多 reducer 规则和连接判断
- 后续应逐步退回“协调者”角色，而不是继续成为巨型逻辑文件

### 5.6 UI Adapter / Frame 层（第一轮中间层已落地）

最小职责：

- 把文档和状态投影成可渲染模型
- 把交互回传成 Graph 命令请求
- 为不同 UI 实现提供一致输入输出

当前结论：

- 当前已经建立 `frame/graphFrame.ts` 与 `frame/graphFrameBuilder.ts`
- `GraphCanvas` 已开始直接消费 `GraphFrame`，不再自行承担节点与连线几何计算
- 后续仍可继续增强为更完整的宿主无关渲染中间表示

### 5.7 Inspector Binding 层（保留）

最小职责：

- 输出统一选择目标
- 不让 Inspector 直接依赖 Graph Canvas 内部实现

当前结论：

- 当前 `graphInspectorBinding.ts` 方向正确
- 但还只是 node/edge 最小投影，后续需要继续容纳 scene-marker、scene-object、group、subgraph 等目标

### 5.8 Storage 层（重做升级）

最小职责：

- 保存本地草稿
- 保存正式图文档
- 处理版本迁移
- 处理宿主文件系统路径

当前结论：

- 当前 localStorage 方案适合开发期草稿
- 不适合作为正式图持久化方案

---

## 6. Graph Canvas 交互基线（已整合，第一轮已完成）

下面这部分原本来自 `GraphCanvas-交互设计清单.md`，现在已经合并进本文。

### 6.1 交互目标类型（已完成）

当前统一以四类目标组织交互：

- `canvas`
- `node`
- `edge`
- `port`

### 6.2 命中优先级（已完成）

命中顺序统一为：

1. `port`
2. `edge`
3. `node`
4. `canvas`

这条规则后续不得轻易改变。

### 6.3 鼠标职责分配（已完成）

- 左键负责主编辑流程
  选择、拖拽、开始连线、完成连线、清空选择

- 右键负责上下文菜单
  不再让浏览器默认菜单介入

- 中键 / `Alt + 左键` / `Shift + 左键`
  负责平移视口

- 滚轮
  负责以指针位置为中心缩放

- `Esc`
  负责关闭菜单、取消连线预览，后续也负责取消其他特殊交互

### 6.4 选择态规则（已完成）

当前第一轮采用单选优先：

- 左键点 node：选中该节点
- 左键点 edge：选中该边
- 左键点 canvas：清空当前选择

右键规则统一为：

- 右键 node：先同步选择该节点，再开菜单
- 右键 edge：先同步选择该边，再开菜单
- 右键 canvas：不强制改当前选择

### 6.5 连线规则（已完成）

- 只能从 `output port` 发起连线
- `input port` 只负责接收完成连线
- 连线预览从端点锚点开始
- 只有 `output -> input`、类型兼容、命令层校验通过时才允许成功连接
- `Esc`、点击空白、成功连接后都会取消预览

### 6.6 菜单分层（已完成）

当前最小菜单分层已经确定：

- `Canvas Menu`
  新建节点、重置视口

- `Node Menu`
  删除当前节点、断开所有连线

- `Edge Menu`
  删除当前连线

- `Port Menu`
  断开此端点所有连线

### 6.7 菜单动作与命令映射（已完成）

| 菜单动作 | 当前命令语义 |
|----------|--------------|
| 新建节点 | `graph.add-node` |
| 删除当前节点 | `graph.remove-nodes` |
| 删除当前连线 | `graph.remove-edges` |
| 断开节点所有连线 | `graph.disconnect-node-edges` |
| 断开端点所有连线 | `graph.disconnect-port-edges` |
| 重置视口 | 视口受控 patch |

### 6.8 当前未纳入第一轮完成标准的能力（已确认）

以下能力不再算作“第一轮交互未完成”，而是后续增强项：

- 完整多选与批量菜单
- 完整框选交互
- 端口专属菜单增强
- 边中插入节点
- 复制粘贴
- 完整快捷键体系
- 复杂节点内联编辑

结论：

- `GraphCanvas-交互设计清单.md` 的第一轮目标已经完成
- 它现在适合被视为“已整合完成”，不再单独维护正文

---

## 7. 当前已落地 / 部分落地 / 未落地（已梳理）

### 7.1 已落地

- 最小 `document / state / commands / definitions / runtime / interaction / frame / ui / binding / storage / serialization` 目录结构
- 最小工作区控制器
- `Graph Runtime` 中间层
- `Selection Manager` 中间层
- `Connection Policy` 中间层
- `Graph Frame / GraphFrameBuilder` 中间层
- `PanZoom / NodeDrag / ConnectionPreview` 交互 handler
- 节点拖拽
- 端点连线预览
- 选中边高亮与中点控制柄
- 节点 / 边 / 端点 / 画布右键菜单第一版
- 端点连接计数显示
- 正式 Graph 文件序列化格式
- 宿主 Graph 文件读写接口（前端 + Tauri）
- Graph 输入抽象第一版
- 文本测量协议第一版
- Inspector 统一读取 graph 选择态
- Inspector 节点属性编辑协议第一版
- 多选 / 框选第一版
- Graph 搜索面板第一版
- Graph 小地图第一版
- Graph 复制 / 粘贴第一版
- Graph 自动布局第一版
- Graph 调试与诊断覆盖层第一版

### 7.2 部分落地

- 命令系统
  已经完成与 `Graph Runtime` 的第一轮解耦，并补入 `graph.patch-node-payload`，但复合命令、拖拽合并、命令对象语义仍未补齐

- 选择系统
  已建立 `Selection Manager`，但多选、主选中、批量选择语义仍未补齐

- 连接校验
  已建立 `Connection Policy`，但类型转换、复杂端口策略与子图边界规则仍未补齐

- 渲染中间层
  已建立 `Graph Frame / GraphFrameBuilder`，并开始通过文本测量参与节点宽高计算，但还没有覆盖 minimap、decoration、subgraph frame 等更完整帧结构

- 宿主持久化
  已有默认 Graph 文件路径解析与读写闭环，但文件选择器、最近项目与多项目管理仍未建立

### 7.3 未落地

- 文件选择器 / 最近项目 / 多项目切换
- 更完整的自动布局算法与布局策略
- 更完整的搜索系统（搜索历史 / 跳转模式 / 命令面板）
- SubGraph / Group / Comment 的真正运行时语义
- Graph 专项自动化测试

---

## 8. 后续实现顺序建议（已更新）

基于当前映射结果，后续 Graph Workspace 最划算的推进顺序是：

### 8.1 第一步：先补中间层（已完成）

优先建立：

- `Graph Runtime`（已完成）
- `Selection Manager`（已完成）
- `Connection Policy`（已完成）
- `Interaction Handler`（已完成）
- `Graph Frame`（已完成）

本轮已经完成的结果：

- 已新增 `runtime/`、`interaction/`、`frame/` 三层目录
- `GraphWorkspaceController.ts` 已退回到协调与拼装层
- `GraphCanvas.tsx` 已改为消费 `GraphFrame` 与交互 handler
- 连接规则、选择规则、节点拖拽与缩放平移的核心逻辑已经从 UI 中抽离

### 8.2 第二步：再补正式序列化与宿主抽象（已完成第一轮闭环）

本轮已落地：

- 图文档序列化格式
- 宿主文件持久化接口
- 输入抽象
- 测量与编辑协议
- Inspector 节点属性回写
- 原生菜单保存项目接入正式 Graph 文件

本轮完成后的实际结果：

- 当前仓库已经能把 Graph Workspace 保存为正式 JSON 文件，并从宿主文件系统重新加载
- `GraphCanvas` 已不再直接依赖 DOM 细节判断主输入语义
- `GraphFrameBuilder` 已开始通过文本测量决定节点宽高，而不是完全写死固定尺寸
- `Inspector` 已经能够基于节点定义协议读取并编辑最小 payload 字段

仍待下一轮补强：

- 文件选择器与最近项目
- 更完整的字段类型与业务编辑器
- 更严格的版本迁移与 schema 升级策略

### 8.3 第三步：最后补高级体验能力（已完成第一轮闭环）

本轮已落地：

- Minimap
- Search
- Auto Layout
- 多选 / 框选增强
- 复制粘贴
- 调试与诊断覆盖层

本轮完成后的实际结果：

- `GraphCanvas` 已支持 `Ctrl / Cmd` 多选、框选与删除
- 当前仓库已具备最小 Graph 剪贴板与偏移粘贴能力
- 当前仓库已具备第一版自动布局命令，支持对选中节点或全图执行布局
- 当前 Graph Workspace 已接入搜索面板、小地图与调试覆盖层
- 这些高级体验能力已经直接挂接在当前 `Runtime / Commands / Frame / UI` 结构上，而不是继续堆在临时组件状态里

仍待下一轮补强：

- 更成熟的布局算法与节点群组策略
- 更完整的复制粘贴语义（跨图、外部文本、系统剪贴板）
- 更完整的搜索与命令面板体验
- 更成熟的 minimap 交互（拖动视口框、缩略层级）

---

## 9. 当前结论（已确认）

当前 `Graph Workspace` 后续开发的核心方向可以收敛成一句话：

**保留旧 nodegraph 对“分层、命令、宿主无关、渲染中间层”的正确设计思想，但不再继承 Unity / IMGUI 绑定实现；在当前仓库中优先补齐中间骨架，再继续扩展复杂交互和业务节点能力。**






