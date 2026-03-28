# SceneBlueprint 架构设计文档

> 版本：v1.2  
> 日期：2026-03-28  
> 状态：设计阶段

---

## 目录

1. [项目概述](#1-项目概述)
2. [核心架构设计](#2-核心架构设计)
3. [外部编辑器方案](#3-外部编辑器方案)
4. [多引擎支持策略](#4-多引擎支持策略)
5. [性能设计](#5-性能设计)
6. [应用场景分析](#6-应用场景分析)
7. [竞品分析](#7-竞品分析)
8. [实施路线图](#8-实施路线图)
9. [风险评估](#9-风险评估)

---

## 1. 项目概述

### 1.1 项目定位

SceneBlueprint 是一个**场景级蓝图编排框架**，面向 Unity 及多引擎游戏开发，提供：

- **可视化节点图编辑**：设计场景事件流程（刷怪、机关、剧情等）
- **时间轴编排**：精确控制波次、演出、同步事件
- **DSL 驱动代码生成**：`.sbdef` 定义 Action/Marker，自动生成类型安全代码
- **运行时解释器**：基于帧的执行引擎，零依赖解释蓝图数据
- **外部编辑器**（规划）：脱离 Unity 的专业级编辑体验
- **多语言模块化架构**：按模块职责拆分 TypeScript / Rust / C#，发挥各语言优势

### 1.2 设计目标

| 目标 | 优先级 | 说明 |
|------|--------|------|
| 引擎无关性 | P0 | Core 层零 Unity 依赖，支持多引擎 |
| 多语言分层 | P0 | Authoring、Infra、Runtime、Integration 按职责选型而非单语言通吃 |
| 策划友好 | P0 | 非技术人员可直接编辑蓝图 |
| 运行时性能 | P1 | 支持 1000+ 节点流畅执行 |
| 类型安全 | P1 | DSL 生成代码，编译期错误检查 |
| 可视化编辑 | P1 | 提供专业级外部编辑器体验，支持复杂节点图、时间轴与白模预览 |
| 商业化潜力 | P2 | 可作为中间件出售或服务 |

### 1.3 当前状态

- ✅ 五层架构（Contract/Domain/Application/Adapter/Infrastructure）
- ✅ `.sbdef` DSL 与代码生成（v0.1-v0.5）
- ✅ 运行时解释器（BlueprintRunner + Systems）
- ✅ 旧版 Unity 原型验证了 DSL、导出与运行时主链路
- ⚠️ 外部编辑器（Tauri + React + Rust）：规划中
- ⚠️ 多语言模块职责边界：已明确，待在新仓库落地
- ⚠️ 多引擎支持：规划中

---

## 2. 核心架构设计

### 2.1 分层架构

```
┌───────────────────────────────────────────────────────────────────────┐
│ Spec Core（语言无关 SSOT）                                            │
│ ├── .sbdef DSL                                                        │
│ ├── Blueprint Authoring Schema (.blueprint.json)                      │
│ ├── Runtime Contract Schema (SceneBlueprintData / runtime json)       │
│ └── JSON Schema / Golden Tests / 示例数据                             │
├───────────────────────────────────────────────────────────────────────┤
│ Authoring Core（TypeScript）                                          │
│ ├── 节点图状态                                                         │
│ ├── 属性面板 schema                                                   │
│ ├── Timeline 模型                                                     │
│ ├── 编辑器命令系统 / Undo / 插件 API                                  │
│ └── React UI / Monaco / Three.js 视图                                 │
├───────────────────────────────────────────────────────────────────────┤
│ Infra Core（Rust）                                                    │
│ ├── 文件监听 / Workspace 索引                                         │
│ ├── 增量缓存 / 后台任务                                               │
│ ├── 编辑器宿主（Tauri）                                               │
│ └── 本地通信桥（WebSocket / IPC）                                     │
├───────────────────────────────────────────────────────────────────────┤
│ Runtime / Toolchain Core（C#）                                        │
│ ├── Sbdef Parser / CodeGen                                            │
│ ├── Blueprint Export / Compile Pipeline                               │
│ ├── BlueprintRunner / BlueprintFrame                                  │
│ └── Runtime Systems / 测试基准                                        │
├───────────────────────────────────────────────────────────────────────┤
│ Engine Integration（按引擎适配）                                      │
│ ├── Unity Integration（优先 C#）                                      │
│ ├── Godot Integration（优先 C#，保留非 C# 接入可能）                  │
│ └── [其他引擎 Adapter - 待扩展]                                       │
└───────────────────────────────────────────────────────────────────────┘
```

**关键原则**：
- `Spec Core` 是唯一权威源（SSOT），不能绑定某一种实现语言。
- `TypeScript` 负责外部编辑器中的作者工作流与 UI 体验。
- `Rust` 负责 Tauri 宿主、文件系统、缓存和性能敏感型基础设施。
- `C#` 负责运行时解释器、工具链、代码生成与 Unity/Godot 首批集成。
- 引擎适配层只能依赖中立契约，不能反向定义 Authoring 数据结构。
- 概念体系采用**分层演进**策略，不要求在项目初期一次性穷举全部核心概念。
- 只有跨模块共享、需要稳定命名、需要持久化或需要多引擎共同消费的对象，才应升级为正式核心概念。

### 2.2 数据流架构

**编辑时数据流**：
```
.sbdef 文件（策划编写）
    ↓
Sbdef Parser / CodeGen（C# Toolchain）
    ↓
SbdefAst
    ↓
├─ C# Runtime 产物
│   ├─ UAT.*.g.cs（类型常量）
│   ├─ UActionPortIds.*.g.cs（PropertyKey）
│   ├─ ActionDefs.*.g.cs（IActionDefinitionProvider）
│   └─ UMarker*.g.cs（Marker 运行时定义）
│
└─ Authoring 描述产物
    ├─ action-defs.json（节点/端口/分类/schema 描述）
    ├─ marker-defs.json（Marker / gizmo / 绑定描述）
    └─ 可选 action-defs.ts（类型化前端消费层）

外部编辑器（Tauri）
.blueprint.json（节点图 Authoring 数据）
    ↓
TypeScript Authoring Core
    ├─ React Flow（节点图）
    ├─ Inspector Schema（属性面板）
    ├─ Timeline Model（时间轴）
    └─ Whitebox Preview（白模预览）
    ↓
Export Pipeline（C# / Rust 调度）
    ↓
SceneBlueprintData / runtime json（运行时契约）
```

**运行时数据流**：
```
runtime json / SceneBlueprintData
            ↓
BlueprintRunner.Load(json)
    ↓
BlueprintFrame（帧状态）
    ↓
Systems.Tick()（按优先级排序执行）
    ↓
Adapter 层（Unity / Godot / 其他引擎实现）
```

### 2.3 DSL 设计（.sbdef）

当前阶段不在架构文档中固化精确语法，以免把仍在演进中的 DSL 细节误写成最终方案。

**当前稳定方向**：
- `.sbdef` 仍然承担**声明层**职责，而不是 authoring 数据本身。
- DSL 预计继续覆盖 `Action`、`Marker`、端口、基础元数据和部分可视化描述。
- DSL 需要同时服务 `Runtime / Toolchain` 与 `Authoring` 两侧，因此会产出运行时定义和编辑器描述。
- 语法可以继续演进，但必须尽量保持：
  - 人工可读
  - 可版本控制
  - 易于代码生成
  - 易于生成 schema / descriptor

**当前不在此文档中提前锁死的内容**：
- 精确语法形式
- 关键字全集
- 类型系统的最终边界
- 默认值、可见性、验证规则的最终表达方式

### 2.4 代码生成产物

| 生成文件 | 用途 | 程序集 | 版本 |
|---------|------|--------|------|
| `UAT.*.g.cs` | Action 类型 ID 常量（`UAT.Vfx.CameraShake`） | Runtime | v0.1 |
| `UActionPortIds.*.g.cs` | PropertyKey<T> 常量 | Runtime | v0.2 |
| `ActionDefs.*.g.cs` | IActionDefinitionProvider 实现 | Runtime | v0.2 |
| `UMarkerTypeIds.*.g.cs` | Marker 类型 ID 常量 | Runtime | v0.3 |
| `UMarkers.*.g.cs` | SceneMarker partial 类骨架 | Runtime | v0.3 |
| `Editor/UMarkerDefs.*.g.cs` | IMarkerDefinitionProvider 实现 | Editor | v0.3 |
| `action-defs.json` | 外部编辑器节点/端口/schema 描述 | Authoring | v1.1 规划 |
| `marker-defs.json` | 外部编辑器 Marker/gizmo/绑定描述 | Authoring | v1.1 规划 |
| `action-defs.ts`（可选） | 前端类型安全封装 | Authoring | v1.1 规划 |

**类型 ID 命名转换规则**：
```
DSL 写法:        VFX.CameraShake
常量值:          "VFX.CameraShake"
UAT 类名:        UAT.Vfx.CameraShake（PascalCase，缩写保留）
PortIds 类名:    UActionPortIds.VFXCameraShake（去点，保留原大小写）
```

### 2.5 运行时解释器

**核心职责**：
- `Runner` 负责驱动执行循环。
- `Frame` 负责持有运行时真相。
- `System` 负责消费 runtime contract 并推进执行。
- 运行时只读取**编译后的 runtime contract**，不直接读取编辑态图结构。

**当前稳定约束**：
- 运行时需要区分静态数据与动态状态。
- 运行时状态必须能支撑调试、快照、回放与性能优化。
- System 的组织方式、调度粒度和缓存结构仍可继续演进，不在当前文档中提前锁死。

**执行模型**：
- **帧驱动**：每帧遍历所有 System，检查条件并执行
- **状态机**：节点状态（Idle → Executing → Completed）
- **事件触发**：Signal/Action 驱动节点流转
- **数据隔离**：Blackboard 按节点作用域隔离

### 2.6 多语言模块化策略

**结论**：SceneBlueprint 不采用“单一语言通吃”的架构，而采用**按模块职责分层选型**。

| 模块 | 首选语言 | 选择原因 | 输出/边界 |
|------|----------|----------|-----------|
| Spec Core | 语言无关 | 作为长期 SSOT，避免被任一实现绑死 | DSL、JSON Schema、测试样例 |
| Authoring Core | TypeScript | 外部编辑器 UI、节点图、时间轴、插件生态最佳 | `.blueprint.json`、前端 schema、命令系统 |
| Infra Core | Rust | Tauri 宿主、文件监听、缓存、后台任务性能最好 | IPC、索引、文件系统服务 |
| Runtime Core | C# | 复用既有资产，利于 Unity/Godot 首批运行时落地 | `SceneBlueprintData`、Runner、Systems |
| Engine Integration | C# 优先 | Unity 最自然，Godot 首批可快速落地 | Unity/Godot adapter、importer、runtime host |

**明确约束**：
- 外部编辑器 UI 与作者工作流不以 C# 为一等核心，避免前后端心智断裂。
- `C#` 仍然保留为运行时、工具链和引擎接入的核心实现语言。
- 若未来 Godot 需要支持非 C# 项目，仍可通过中立 contract 额外提供 GDScript/C++ 侧桥接。
- 所有语言实现都必须围绕同一份 Spec 和 Contract 测试数据对齐。

---

## 3. 外部编辑器方案

### 3.1 设计取舍

本次升级不再把“Unity 内置编辑器”视为基线能力，也不再以“是否接近 Unity 旧工作流”作为评判标准。

新的外部编辑器方案以以下原则为准：

- **作者优先**：编辑体验围绕策划、TA 和关卡设计工作流构建，而不是围绕引擎 Inspector 组织。
- **大图优先**：节点图、时间轴、白模预览必须从一开始就面向复杂图和大场景设计。
- **多引擎优先**：Authoring 数据与运行时契约必须天然可被 Unity、Godot 和后续引擎消费。
- **文本优先**：`.sbdef`、`.blueprint.json` 和 runtime contract 都应适合版本控制、Diff 和自动化校验。
- **解耦优先**：外部编辑器负责 authoring，集成插件负责桥接，不再让任一引擎定义作者工作流。

### 3.2 技术栈选型

**推荐栈：Tauri + React + TypeScript + Rust + C# Toolchain**

```
Tauri 编辑器
├── 前端（React + TypeScript）
│   ├── 节点图：react-flow-renderer / @xyflow/react
│   ├── 时间轴：自研（基于 Canvas）或 react-timeline-gantt
│   ├── DSL 编辑器：Monaco Editor（VS Code 核心）
│   ├── 属性面板：react-jsonschema-form
│   ├── 3D 预览：Three.js（白模场景）
│   ├── 状态管理：Zustand / Redux Toolkit
│   └── Authoring Core：Graph / Inspector / Timeline / Command System
│
├── 后端（Rust）
│   ├── 文件系统：监视工作区变更
│   ├── Workspace 索引 / 增量缓存
│   ├── 调度 C# Toolchain 执行代码生成 / 导出
│   ├── 通信桥：IPC / WebSocket 会话桥
│   └── 资源加载：glTF/JSON 场景导入
│
├── 工具链（C#）
│   ├── Sbdef Parser / Emitter
│   ├── Runtime Contract Export
│   ├── Headless Validator / Compiler
│   └── Unity / Godot 首批共享运行时
│
└── 构建产物
    ├── Windows：.exe（~5-10MB）
    ├── macOS：.app（~8-12MB）
    └── Linux：AppImage（~10MB）
```

**语言职责分工**：

| 领域 | 语言 | 原因 |
|------|------|------|
| 编辑器 UI / Authoring | TypeScript | UI 生态成熟，迭代快，适合插件化和 schema 驱动 |
| 宿主 / 后台基础设施 | Rust | 性能高，边界清晰，和 Tauri 原生契合 |
| Runtime / Toolchain / 引擎接入 | C# | 复用现有资产，Unity/Godot 集成效率最高 |
| 规格与契约 | 语言无关 | 防止任一实现绑死整体产品架构 |

**备选方案对比**：

| 方案 | 体积 | 性能 | 生态 | 评价 |
|------|------|------|------|------|
| Tauri | 极小 | 高 | 成长中 | ✅ 推荐 |
| Electron | 大（100MB+）| 中 | 成熟 | 体积 unacceptable |
| Flutter Desktop | 中 | 高 | 一般 | Dart 学习成本 |
| Native（Qt/WPF）| 小 | 极高 | 一般 | 开发慢 |

### 3.3 数据同步架构

**基线方案：文件驱动同步**

```
外部编辑器                    集成插件（Unity / Godot）
    │                         │
    ├── 保存 .blueprint.json ─┤
    ├── 保存 runtime json ────┤
    └── 保存 .sbdef ──────────┤
                              ├── 监听工作区变化
                              ├── 刷新本地缓存
                              └── 请求重新导入 / 重新装载
```

这个方案作为默认主链路，原因是：
- 最稳定
- 最适合版本控制
- 最容易支持多引擎
- 不依赖特定编辑器 API

**增强方案：会话桥实时联动**

```
外部编辑器 (Tauri)                集成插件（Unity / Godot）
    │                                  │
    ├── Session Bridge ───────────────┤
    │                                  ├── 聚焦场景对象
    │                                  ├── 推送场景摘要
    │                                  └── 同步调试状态
    │
    └── 实时请求 / 响应消息 ◄──────────┤
```

这个方案只作为增强能力，不作为唯一同步主链路。

**通信协议设计方向**：

| 消息类别 | 用途 |
|------|------|
| 聚焦类 | 让集成插件在场景中聚焦对象、Marker 或绑定目标 |
| 场景摘要类 | 让集成插件向外部编辑器推送场景摘要、Marker 列表和白模数据 |
| 蓝图更新类 | 让外部编辑器向集成插件通知当前蓝图或运行时契约已更新 |
| 调试控制类 | 控制播放、暂停、步进、状态刷新 |

协议字段与命名在实现阶段再收敛，不在架构文档中提前固化。

### 3.4 白模场景预览

**核心设计**：不导出完整美术资源，只导出**逻辑几何与空间语义**。

白模预览不应依赖 Unity 专属场景对象模型，而应依赖中立的场景摘要契约：

**场景摘要至少需要表达**：
- 版本信息
- Marker 列表
- 基础空间信息：位置、旋转、缩放或尺寸
- 逻辑几何类型：点、球、盒、多边形、路径等
- 可选地形或关卡轮廓摘要

各引擎集成插件只负责把本地场景转换成这个摘要格式，外部编辑器统一消费该摘要。

**渲染策略方向**：
- 按类型分组渲染
- 支持实例化批量绘制
- 支持视距与层级细节控制
- 支持当前选中对象高亮
- 保留未来切换渲染技术实现的空间，不在架构文档中提前绑定具体类设计

### 3.5 TimeLine 编辑器实现（Spawn.Wave）

**核心需求**：多轨道、拖拽调整、时间缩放、与 3D 预览联动

**当前稳定方向**：
- 时间轴至少包含轨道、片段、时间游标、缩放与选区。
- 时间轴需要与白模预览联动，而不是孤立存在。
- 时间轴的领域模型需要能支撑拖拽、伸缩、复制、对齐、吸附和批量编辑。
- 具体组件树、状态拆分方式和交互实现细节暂不在架构文档中锁死。

**性能优化**：
- Canvas 渲染时间轴（非 DOM，支持 1000+ 项）
- 虚拟滚动：只渲染可见时间区域
- Web Worker：时间冲突检测在后台线程

---

## 4. 多引擎支持策略

### 4.1 引擎无关层设计

**核心原则**：引擎无关性不能只靠“某个纯 C# 程序集”来承载，而必须靠**分层边界 + 中立契约**来承载。

- `Spec Core`：语言无关，定义 DSL、authoring schema、runtime contract。
- `Runtime Core`：当前优先使用纯 C#（.NET Standard / .NET），零 Unity API 依赖。
- `Authoring Core`：使用 TypeScript，但只能依赖 schema 和 contract，不能直接依赖 Unity/Godot 类型。
- `Infra Core`：使用 Rust，负责文件系统与进程边界，不参与业务语义定义。
- `Engine Integration`：按 Unity / Godot / 其他引擎分别适配。

**Runtime Core 需要提供的抽象能力**：
- 场景对象抽象
- 时间抽象
- 资源加载抽象
- 物理或空间查询抽象
- 可序列化的基础数学与空间数据类型

具体接口命名、拆分粒度和包结构在实现阶段再收敛。

### 4.2 引擎适配层实现

**Unity 适配需要承担**：
- 将 Unity 场景对象映射到中立场景抽象
- 提供 Unity 下的时间、资源、空间查询桥接
- 提供场景摘要与绑定桥接
- 提供 runtime contract 的导入、装载和调试接线

**Godot 适配需要承担**：
- 将 Godot 场景对象映射到中立场景抽象
- 提供 Godot 下的时间、资源、空间查询桥接
- 提供场景摘要与绑定桥接
- 提供 runtime contract 的导入、装载和调试接线

具体类名与实现形式不在本架构文档中锁死。

**说明**：
- Unity 与 Godot 的首批接入优先使用 C#，以便最大化复用 Runtime Core。
- 若后续 Godot 需要支持非 .NET 项目，可在保持 runtime contract 不变的前提下增加 GDScript / GDNative / C++ 桥接层。
- 引擎适配负责“消费 contract”和“提供场景摘要桥接能力”，不负责重新定义 contract 或 authoring 模型。

### 4.3 多引擎支持路线图

| 引擎 | 优先级 | 工作量 | 技术方案 |
|------|--------|--------|----------|
| Unity | P0（已有） | - | MonoBehaviour 适配 |
| Godot 4 | P1 | 2-3 周 | C# 适配器优先，保留非 C# 桥接路线 |
| Unreal 5 | P2 | 4-6 周 | C++ 插件 + 蓝图节点封装 |
| 自研引擎 | P2 | 2-4 周 | C++ 绑定或 WASM 解释器 |
| Web/JS | P3 | 3-4 周 | TypeScript Authoring + WASM/JS Runtime Bridge |

### 4.4 资源格式标准化

**蓝图数据格式**（引擎无关）：

运行时契约至少需要覆盖：
- 版本号与蓝图标识
- 节点或动作列表
- 控制流与数据流关系
- 变量与默认值
- 运行时所需的编译产物或 metadata

字段命名与最终结构在 compile/export 层稳定后再单独沉淀到 schema 中。

**资源引用方案**（跨引擎 GUID）：

资源引用需要满足：
- 跨引擎稳定标识
- 引擎本地路径或定位信息
- 资源类型信息
- 由引擎侧 resolver 负责最终解析

---

## 5. 性能设计

### 5.1 运行时性能指标

| 指标 | 目标值 | 测试方法 |
|------|--------|----------|
| 节点执行吞吐 | 10,000+ 节点/帧 | 空节点循环测试 |
| 单帧执行时间 | < 1ms（1000 节点）| Profiler 测量 |
| 内存占用 | < 50MB（单张蓝图）| Memory Profiler |
| 加载时间 | < 100ms（1000 节点）| 计时器 |
| GC 压力 | < 1 次/秒 | GC 计数 |

### 5.2 优化策略

**1. 节点状态机缓存**
- 尽量减少每帧的字典查找和动态分派
- 对活跃节点、可执行节点和系统映射建立缓存

**2. Blackboard 池化**
- 减少临时对象和字典分配
- 让高频读写数据结构具备复用能力

**3. 二进制序列化**
- 在 JSON 之外预留更高效的载入格式
- 将文本友好格式与运行时高效格式拆开

**4. 图分割（支持万级节点）**
- 允许把大图拆成多个逻辑块
- 只加载当前活跃区域对应的数据
### 5.3 外部编辑器性能

**Three.js 优化**：
- **InstancedMesh**：1000+ 相同几何体一个 draw call
- **LOD**：远距离用 billboard，中距离用简化几何
- **视锥剔除**：只渲染可见区域
- **对象池**：Marker 预览对象复用

**React 优化**：
- **虚拟滚动**：时间轴只渲染可见区域
- **Canvas 渲染**：复杂时间轴用 Canvas 而非 DOM
- **Web Worker**：布局计算在后台线程
- **Memoization**：节点数据变化检测

### 5.4 内存预算

| 模块 | 预算 | 说明 |
|------|------|------|
| 运行时 Core | 20MB | 解释器 + Blackboard |
| 单张蓝图 | 10-50MB | 根据节点数变化 |
| 场景快照 | 5-20MB | Marker 绑定数据 |
| 外部编辑器 | 200-500MB | Three.js + React |
| **总计** | **< 1GB** | 现代 PC 轻松承受 |

---

## 6. 应用场景分析

### 6.1 完美契合的游戏类型（90-100% 复用）

**1. 动作 RPG / 地牢探索**
- **核心应用**：房间/关卡脚本、波次战斗、机关解谜
- **蓝图示例**：
  ```
  [进入房间] → [触发刷怪波次] → [清理完成] → [开启宝箱/解锁门]
  ```
- **扩展**：连招教学 QTE、环境叙事、随机地牢组合

**2. 塔防游戏**
- **核心应用**：波次编排、路径控制、塔建造逻辑
- **适配**：Spawn.Wave 时间轴直接复用，添加 Tower.Build Action
- **优势**：精确控制多路出兵节奏、动态难度调整

**3. 剧情向 RPG / 视觉小说**
- **核心应用**：分支对话、任务流程、演出控制
- **适配**：Dialogue.Show Action，Branch 节点做选择
- **扩展**：时间轴控制"角色移动+镜头+音效"同步

**4. 生存恐怖**
- **核心应用**：惊吓编排（触发器+延迟+刷怪）、资源管理、多结局
- **优势**：精确控制"玩家走到X点 → 延迟3秒 → 事件触发"

**5. 波次生存（Horde Mode）**
- **核心应用**：动态难度导演系统
- **适配**：根据玩家表现（Blackboard 记录）调整后续波次

### 6.2 良好支持的游戏类型（70-90% 复用）

**6. 模拟经营 / 生活模拟**
- **适用**：NPC 日程表、节日活动、生产链
- **挑战**：需要补充持续模拟能力（作物生长是渐变）
- **建议**：SceneBlueprint 处理"事件"，ECS 处理"模拟"

**7. Roguelike / 轻量地牢**
- **适用**：房间遭遇设计、随机事件、卡牌效果链
- **优势**：运行时加载不同 Blueprint 组合，程序生成内容

**8. 开放世界（动态事件）**
- **适用**：世界任务、营地袭击、世界 Boss 战
- **扩展需求**：流式加载、区域触发器、事件优先级管理

### 6.3 可用但非核心的类型（40-70% 复用）

**9. 回合制策略**
- **改造**：回合制执行模式（等待玩家输入 → 执行一步）
- **适用**：战斗演出、剧情事件
- **不适用**：核心战斗逻辑（传统策略框架更合适）

**10. 多人合作 PVE**
- **扩展需求**：网络同步 Blackboard、确定性回放
- **适用**：动态难度导演（《求生之路》式 AI 导演）

### 6.4 不适合的类型（< 40% 复用）

- **纯物理模拟**（《围攻 Besiege》）：离散事件 vs 连续模拟冲突
- **超高速动作**（《鬼泣》）：解释器延迟影响手感
- **大规模 RTS**（《星际争霸》）：100+ 单位 AI 开销过高
- **完全涌现沙盒**（《我的世界》）：剧本编排 vs 涌现玩法冲突

### 6.5 跨类型通用价值

**作为通用关卡脚本工具**：
- **平台跳跃**：控制移动平台路径和时间
- **潜行游戏**：控制巡逻路线和警戒状态
- **解谜游戏**：控制机关联动逻辑
- **射击游戏**：控制掩体出现/消失时机

**作为任务系统中间件**：
- **任务流程**：接取 → 完成条件 → 交付 → 奖励
- **动态任务**：运行时生成 Blueprint
- **任务演出**：时间轴控制过场动画

**作为新手引导框架**：
- **分步指引**：点击这里 → 现在去那里 → 使用技能
- **条件分支**：根据玩家操作速度调整节奏
- **可视化编辑**：策划直接调整引导步骤

---

## 7. 竞品分析

### 7.1 直接竞品对比

| 产品 | 类型 | 编辑器 | 运行时 | 价格 | 开源 | 引擎支持 |
|------|------|--------|--------|------|------|----------|
| **Articy Draft** | 专业叙事 | 独立 WPF | ❌ 需自行解析 | $$$ | ❌ | 通用 |
| **Yarn Spinner** | 对话系统 | Electron | ✅ 官方 | 免费 | ✅ MIT | Unity/UE/Godot/JS |
| **Ink** | 文本叙事 | Electron | ✅ 官方 | 免费 | ✅ MIT | Unity/UE/JS/Python |
| **PlayMaker** | 可视化脚本 | Unity 内置 | ✅ Unity | $$ | ❌ | Unity 专属 |
| **xNode** | 节点框架 | Unity 内置 | ❌ 需自行实现 | 免费 | ✅ MIT | Unity 专属 |
| **SceneBlueprint** | 场景编排 | 规划中（Tauri） | ✅ 内置 | 待定 | 计划中 | Unity（多引擎规划中） |

### 7.2 差异化优势

**vs Articy Draft**：
- ✅ 内置运行时解释器（Articy 只导出数据）
- ✅ 专业时间轴（Spawn.Wave 类需求）
- ✅ DSL 代码生成（类型安全）
- ✅ 预期低价/开源

**vs Yarn/Ink**：
- ✅ 可视化节点图（非纯文本）
- ✅ 空间标记系统（Marker）
- ✅ 时间轴编排（非对话专用）

**vs PlayMaker/xNode**：
- ✅ 外部编辑器（非 Unity 内置）
- ✅ 引擎无关（可移植 Godot/UE）
- ✅ 类型安全 DSL（非动态）

### 7.3 市场定位

**目标市场空缺**：
```
[外部编辑器] + [场景编排] + [内置运行时] + [多引擎] + [开源/低价]
        ↑
   SceneBlueprint 定位
```

**目标用户**：
- 独立游戏团队（负担不起 Articy）
- 需要轻量级剧情系统的项目
- 教育/培训类交互内容
- 中型工作室（需要比 Yarn 更强的场景控制能力）

---

## 8. 实施路线图

### 8.1 阶段划分

```
第一阶段：夯实基础（4-6 周）
├── Week 1-2: Spec Core 与 Runtime Core 拆分
│   ├── 明确 `.sbdef` / `.blueprint.json` / runtime json 三层边界
│   ├── 抽取纯 C# Runtime / Toolchain 核心
│   └── 建立 schema + golden tests
│
├── Week 3-4: 外部编辑器 MVP 骨架
│   ├── Tauri + React + TypeScript 基础框架搭建
│   ├── Rust backend 文件监听 / 索引服务
│   └── Monaco Editor 集成（.sbdef 编辑）
│
└── Week 5-6: 通信桥与原型验证
    ├── C# Toolchain 接入外部编辑器导出链
    ├── 文件驱动的多引擎集成闭环
    └── 最小节点图编辑闭环（读写 `.blueprint.json`）

第二阶段：功能完善（6-8 周）
├── Week 7-8: 节点图编辑器
│   ├── React Flow 集成
│   ├── 蓝图可视化编辑（读写 .blueprint.json）
│   └── 属性面板自动生成
│
├── Week 9-10: 3D 预览系统
│   ├── Three.js 白模渲染
│   ├── Marker 占位符显示
│   └── 时间轴与 3D 视图联动
│
└── Week 11-14: 高级功能
    ├── 多人协作（Git 集成）
    ├── 版本对比（可视化 Diff）
    └── 性能优化（虚拟滚动、二进制序列化）

第三阶段：多引擎支持（6-8 周）
├── Week 15-17: Godot 适配
│   ├── C# 适配器实现
│   ├── 资源加载器适配
│   └── 评估非 C# Godot 桥接需求
│
├── Week 18-20: Unreal 适配（可选）
│   ├── C++ 插件开发
│   ├── 蓝图节点封装
│   └── 示例项目
│
└── Week 21-22: 文档与发布准备
    ├── API 文档
    ├── 视频教程
    └── 开源/商业化准备

第四阶段：商业化（持续）
├── 开源核心（GitHub）
├── 高级功能付费（云协作等）
├── 引擎适配包销售
└── 企业支持服务
```

### 8.2 技术债务清理清单

**高优先级（立即）**：
- [ ] 明确 Spec Core 与 Runtime Core 边界
- [ ] 将 Runtime 改为纯 .NET（去除 Unity API 耦合）
- [ ] 抽象所有 Unity API 调用到 Adapter 层
- [ ] JSON 序列化改为 System.Text.Json（减少 Unity 依赖）
- [ ] 为外部编辑器补充 `action-defs.json` / `marker-defs.json` 生成产物

**中优先级（3 个月内）**：
- [ ] 添加完整的单元测试（NUnit/xUnit）
- [ ] 实现二进制序列化格式
- [ ] 优化 Blackboard 内存分配

**低优先级（6 个月内）**：
- [ ] 接入 Unity DOTS/ECS（性能极致优化）
- [ ] WebGL 运行时支持
- [ ] 云端协作服务

### 8.3 里程碑定义

| 里程碑 | 交付物 | 成功标准 |
|--------|--------|----------|
| **M1: Core 独立** | Spec + Runtime Core | Schema、contract 与 C# runtime 可独立测试 |
| **M2: 编辑器原型** | Tauri 应用 | 能编辑 `.sbdef` / `.blueprint.json` 并导出 runtime contract |
| **M3: 时间轴** | Wave 编辑器 | 实现截图中的 Spawn.Wave 交互 |
| **M4: 3D 预览** | 白模查看器 | 能加载导出场景并联动时间轴 |
| **M5: Godot 支持** | Godot 示例项目 | 相同 Blueprint 在 Godot 运行 |
| **M6: 开源发布** | GitHub 仓库 | 100+ Stars，完整文档 |

---

## 9. 风险评估

### 9.1 技术风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| **Tauri 生态不成熟** | 中 | 中 | 备选 Electron；关注社区进展 |
| **多引擎适配复杂度** | 高 | 中 | 优先 Unity/Godot；UE 延后 |
| **性能不达预期** | 中 | 高 | 早期做性能基准测试；预留 DOTS 升级路径 |
| **会话桥同步可靠性** | 中 | 中 | 文件驱动作为基线；实时联动只做增强层 |
| **DSL 编译器维护成本** | 低 | 中 | 语法保持简单；良好的测试覆盖 |

### 9.2 项目风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| **开发周期超出** | 高 | 高 | 敏捷开发，MVP 优先；阶段交付 |
| **前端人力不足** | 高 | 高 | 简化首版 UI；招聘或培训 |
| **竞品抢先** | 中 | 中 | 快速开源建立社区；差异化功能 |
| **商业模式失败** | 中 | 高 | 保留开源选项；多元收入 |

### 9.3 缓解策略

**技术风险缓解**：
1. **原型先行**：在全面开发前，用 2 周做技术验证原型
2. **模块化设计**：每个引擎适配独立，失败不影响核心
3. **性能预算**：设定明确的性能指标，超预算时启动优化

**项目风险缓解**：
1. **阶段交付**：每个阶段有可演示的成果，降低沉没成本
2. **开源策略**：即使商业失败，开源也能建立声誉
3. **社区驱动**：早期引入用户反馈，避免闭门造车

---

## 10. 附录

### 10.1 术语表

> 说明：术语表不是一次性定稿。SceneBlueprint 的概念会按 `Spec / Authoring / Compile / Runtime / Integration` 各层逐步沉淀，当前阶段不追求穷举。

| 术语 | 定义 |
|------|------|
| **Blueprint** | 场景蓝图，描述场景行为流程的节点图数据 |
| **Action** | 动作，蓝图中的执行单元（如刷怪、播放动画） |
| **Marker** | 标记，场景中的空间参考点（刷怪点、触发区） |
| **Blackboard** | 黑板，蓝图执行期间的共享数据存储 |
| **System** | 系统，处理特定 Action 类型的执行逻辑 |
| **DSL** | 领域特定语言，.sbdef 文件格式 |
| **Runner** | 解释器，驱动 Blueprint 执行的运行时 |
| **Node Graph** | 节点图，可视化编辑的图结构 |
| **Timeline** | 时间轴，用于精确时间控制的编辑器组件 |

### 10.2 概念登记方式

后续新增概念时，按“登记”而非“立刻定稿”的方式维护，避免过早抽象。

| 字段 | 说明 |
|------|------|
| 名称 | 当前使用名称 |
| 所属层 | Spec / Authoring / Compile / Runtime / Integration |
| 状态 | Core / Derived / Tentative |
| 是否持久化 | 是否进入文件格式、schema 或 contract |
| 是否跨模块共享 | 是否会成为稳定边界的一部分 |
| 备注 | 是否可能改名、降级或拆分 |

### 10.3 参考资源

**技术参考**：
- [Tauri 文档](https://tauri.app/)
- [React Flow](https://reactflow.dev/)
- [Monaco Editor](https://microsoft.github.io/monaco-editor/)
- [Three.js](https://threejs.org/)

**竞品参考**：
- [Articy Draft](https://www.articy.com/)
- [Yarn Spinner](https://yarnspinner.dev/)
- [Ink](https://www.inklestudios.com/ink/)

**架构参考**：
- [FrameSync Engine](https://github.com/zhenghongzhi/FrameSyncEngine)（您的项目基础）
- [xNode](https://github.com/Siccity/xNode)（Unity 节点图参考）

### 10.4 决策记录

| 日期 | 决策 | 理由 | 替代方案 |
|------|------|------|----------|
| 2026-03-26 | 采用 Tauri 而非 Electron | 体积小，性能好 | Electron（生态成熟但体积大） |
| 2026-03-26 | 白模预览而非真实模型 | 实现简单，跨引擎 | 真实模型预览（需复杂资源管道） |
| 2026-03-26 | 解释型而非编译型 | 动态加载，调试友好 | 编译为 IL（性能好但复杂） |
| 2026-03-26 | .sbdef DSL 代码生成 | 类型安全，IDE 友好 | 纯反射（运行时灵活但无类型检查） |
| 2026-03-28 | 不采用单一语言通吃 | Authoring、Infra、Runtime 诉求差异过大 | 全部使用 C# 或全部使用 TypeScript |
| 2026-03-28 | TypeScript 作为 Authoring Core | 外部编辑器 UI/状态/插件生态最匹配 | C#（前后端割裂），Rust（迭代慢） |
| 2026-03-28 | Rust 作为 Infra Core | Tauri 宿主、缓存、文件监听性能和边界控制更优 | Node.js、纯前端实现 |
| 2026-03-28 | C# 保留为 Runtime / Toolchain Core | 复用既有资产，Unity/Godot 首批接入效率最高 | 全部改写为 Rust 或 TypeScript |
| 2026-03-28 | 不再以 Unity 内置编辑器为设计基线 | 避免新框架继续被旧工作流束缚 | 保留 Unity-first 演进路线 |

---

## 文档信息

- **版本**：v1.2
- **作者**：AI Assistant + 项目团队
- **创建日期**：2026-03-26
- **更新日志**：
  - v1.0：初始版本，涵盖架构、外部编辑器、多引擎、性能、应用、竞品、路线图
  - v1.1：明确多语言模块化方案，补充 TypeScript / Rust / C# 职责边界与实施顺序
  - v1.2：删除 Unity-first 假设，改为以外部编辑器、文件驱动同步和中立契约为基线

---

**后续更新计划**：
- 外部编辑器原型完成后，补充 UI 设计稿和交互细节
- 多引擎适配完成后，补充各引擎具体实现差异
- 开源发布后，根据社区反馈调整路线图
