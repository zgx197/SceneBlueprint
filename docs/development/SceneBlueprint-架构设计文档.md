# SceneBlueprint 架构设计文档

> 版本：v1.4  
> 日期：2026-03-28  
> 状态：设计阶段，已完成第一阶段桌面盒子落地

---

## 目录

1. [文档定位](#1-文档定位)
2. [项目定位](#2-项目定位)
3. [外部编辑器架构原则](#3-外部编辑器架构原则)
4. [总体分层架构](#4-总体分层架构)
5. [技术选型与理由](#5-技术选型与理由)
6. [核心数据边界与主链路](#6-核心数据边界与主链路)
7. [多引擎集成策略](#7-多引擎集成策略)
8. [行业案例启发](#8-行业案例启发)
9. [实施顺序建议](#9-实施顺序建议)
10. [风险与约束](#10-风险与约束)
11. [附录](#11-附录)

---

## 1. 文档定位

本文档用于描述 SceneBlueprint 的长期设计边界、技术选型方向、模块职责和跨模块稳定约束。

本文档的目标不是提前固化所有实现细节，而是为后续开发提供稳定的判断标准。

本文档与《实现与迭代文档》分工如下：

- 架构设计文档：回答“我们长期要做成什么，以及各层边界是什么”
- 实现与迭代文档：回答“当前一阶段先做什么、怎么验证、如何快速收敛”

因此，本文档强调：

- 稳定边界
- 分层职责
- 技术路线判断
- 不应轻易改变的原则

而不强调：

- 具体类设计
- 精确 API 形式
- 过早绑定的实现细节
- 尚未稳定的交互与组件实现

---

## 2. 项目定位

### 2.1 项目定义

SceneBlueprint 是一个**面向游戏开发的外部场景蓝图编辑器与运行框架**。

它的核心目标不是成为某个引擎内的编辑器插件，而是成为：

- 场景事件与流程编排的作者工具
- 运行时契约与导出结果的生成中心
- Unity、Godot 及后续引擎的共同上游内容源头

SceneBlueprint 面向的核心能力包括：

- 可视化节点图编辑
- 时间轴编排
- 场景标记与空间语义组织
- 从 Authoring 数据导出 Runtime Contract
- 多引擎运行时消费与集成

### 2.2 设计目标

| 目标 | 优先级 | 说明 |
|------|--------|------|
| 外部编辑器优先 | P0 | Authoring 工作流不由任一引擎定义 |
| 引擎无关性 | P0 | 核心文件格式、契约和作者模型不绑定 Unity/Godot |
| 多语言分层 | P0 | 按职责选择 TypeScript / Rust / C#，而不是单语言通吃 |
| 作者体验 | P0 | 面向策划、TA、关卡设计，而不是面向引擎 Inspector |
| 可版本控制 | P0 | 关键文件格式适合 Diff、审查、自动化测试 |
| 可扩展性 | P1 | 允许后续接入更多引擎、运行时和工具链 |
| 性能与稳定性 | P1 | 能承载大图编辑、导出与运行时消费 |
| 商业化潜力 | P2 | 为未来商业级开源或商业产品留出演进空间 |

### 2.3 当前明确结论

当前阶段已经形成以下共识：

- SceneBlueprint 的定位是**外部编辑器工具**，不是 Unity 插件，也不是 Unreal 插件
- 引擎侧能力应作为 Integration 层存在，而不是作为 Authoring 核心存在
- 文件驱动同步应作为第一主链路，实时联动只作为增强层
- 核心稳定资产应当是 Spec、Schema、Contract 和测试样本，而不是某个宿主框架
- 技术选型应服务未来多引擎与商业级工具形态，而不是只服务当前单一原型速度

### 2.4 当前实现状态（已完成）

截至 2026-03-28，第一阶段最小桌面盒子已经落地：

- Tauri 桌面宿主工程已建立
- 前端工作台骨架已可挂载到桌面窗口
- Rust 宿主与前端的最小通信链路已打通
- 当前仓库已经具备继续向 Authoring、Toolchain、Contract、Integration 填充内容的基础容器

这意味着当前架构判断不再停留在纯讨论阶段，而已经进入“边实现边验证边收敛”的阶段。

---

## 3. 外部编辑器架构原则

这一节是本次文档更新的核心。

SceneBlueprint 作为外部编辑器工具，后续设计与实现必须优先遵守以下原则。

### 3.1 Authoring Source 是唯一语义源头

外部编辑器中的 Authoring 数据是场景蓝图的语义中心。

这意味着：

- 作者工作流由外部编辑器定义
- 引擎侧不反向定义 Authoring 模型
- 引擎插件只消费中立契约，不负责重新发明蓝图编辑格式

### 3.2 Authoring Source 与 Runtime Artifact 必须分离

成功的外部工具几乎都把“编辑态源文件”和“运行时消费产物”明确拆开。

SceneBlueprint 也必须保持这条边界：

- 编辑态数据：保留作者语义、编辑结构与可读性
- 导出产物：保留运行时所需结构、稳定版本与引擎消费友好性

不应让运行时结构反向绑死编辑结构。

### 3.3 文件驱动是基础，同步桥是增强

SceneBlueprint 必须把文件保存、导出、导入作为最基础的协作模式。

原因：

- 最稳定
- 最适合版本控制
- 最适合多引擎
- 最容易自动化测试
- 最不依赖特定编辑器 API

实时会话桥、联动调试、场景聚焦等能力，可以做，但只能建立在文件主链路已经稳定的前提下。

### 3.4 引擎集成应当“薄而可靠”

引擎适配层的职责应控制在以下范围：

- 消费 runtime contract
- 提供场景摘要或绑定桥接
- 提供调试和聚焦接口
- 负责引擎本地资源或对象解析

引擎适配层不应承担：

- 重新定义 authoring schema
- 重写编辑器核心逻辑
- 成为唯一的编辑入口

### 3.5 核心边界必须中立

只有以下内容应被视为长期稳定核心边界：

- `.sbdef` 或其他定义输入层
- `.blueprint.json` 或 Authoring Source
- runtime contract
- schema / fixtures / golden tests
- 引擎无关的概念命名与版本规则

宿主层、UI 组件、通信方式、内部状态组织都可以演进，但这些边界不能轻易漂移。

### 3.6 自动化能力必须是正式能力，而不是补充能力

商业级外部工具通常都具备明确的 CLI、API、批处理或自动化入口。

因此 SceneBlueprint 后续必须预留：

- Headless export
- Headless validate
- 批量构建与检查
- 可被 CI 调用的工具链入口

GUI 不是唯一入口。

### 3.7 宿主技术不应成为产品核心资产

Tauri、Electron、Avalonia、Qt 都只是宿主实现方式。

SceneBlueprint 的真正核心资产应当是：

- Authoring model
- Runtime contract
- Toolchain
- Integration strategy
- Workflow design

如果未来宿主层替换，核心模型和主链路不应被推倒重来。

---

## 4. 总体分层架构

### 4.1 分层结构

```
┌───────────────────────────────────────────────────────────────────────┐
│ Spec Core（语言无关 SSOT）                                            │
│ ├── .sbdef / 定义输入层                                               │
│ ├── Authoring Schema (.blueprint.json)                                │
│ ├── Runtime Contract Schema                                           │
│ ├── JSON Schema / Fixtures / Golden Tests                             │
│ └── 版本规则 / 概念命名 / 兼容策略                                     │
├───────────────────────────────────────────────────────────────────────┤
│ Authoring Core（TypeScript）                                          │
│ ├── Node Graph Model                                                  │
│ ├── Inspector Model                                                   │
│ ├── Timeline Model                                                    │
│ ├── Command / Undo / Selection / Validation                           │
│ ├── Workspace Session State                                           │
│ └── Editor UI（React / Monaco / Preview）                             │
├───────────────────────────────────────────────────────────────────────┤
│ Infra Core（Rust）                                                    │
│ ├── Desktop Host（Tauri）                                             │
│ ├── 文件系统 / Watcher / Workspace 索引                               │
│ ├── 本地缓存 / 后台任务 / 调度                                         │
│ ├── 进程桥 / IPC / 外部工具调度                                        │
│ └── 宿主能力封装                                                       │
├───────────────────────────────────────────────────────────────────────┤
│ Runtime / Toolchain Core（C#）                                        │
│ ├── Sbdef Parser / Generator                                          │
│ ├── Contract Export / Compile Pipeline                                │
│ ├── Validator / Headless CLI                                          │
│ ├── Runtime Core / Runner / Systems                                   │
│ └── 测试基准 / Golden Verification                                     │
├───────────────────────────────────────────────────────────────────────┤
│ Engine Integration（按引擎适配）                                      │
│ ├── Unity Integration（C#）                                           │
│ ├── Godot Integration（C# 优先，保留非 C# 桥接可能）                   │
│ ├── Unreal Integration（未来 C++）                                    │
│ └── 其他引擎 Adapter                                                   │
└───────────────────────────────────────────────────────────────────────┘
```

### 4.2 各层职责

| 层 | 主要职责 | 不负责 |
|----|----------|--------|
| Spec Core | 定义文件格式、契约、命名、样本、测试边界 | UI、引擎对象访问、宿主细节 |
| Authoring Core | 管理编辑态语义、交互模型、命令系统与视图状态 | 文件系统宿主、引擎运行时实现 |
| Infra Core | 提供桌面宿主、本地文件能力、进程桥与后台服务 | 业务语义定义、运行时规则定义 |
| Runtime / Toolchain Core | 负责导出、校验、运行时解释、生成 | UI 工作流、宿主界面 |
| Engine Integration | 消费 contract、适配引擎对象与资源 | 定义 authoring 模型 |

### 4.3 关键边界约束

- `Spec Core` 是唯一权威源
- `Authoring Core` 不能直接依赖 Unity、Godot 或 Unreal 类型
- `Runtime Core` 不能反向要求 Authoring Core 使用某个引擎数据结构
- `Infra Core` 不参与业务语义定义
- `Engine Integration` 只能消费 contract，不定义 contract

---

## 5. 技术选型与理由

### 5.1 当前推荐路线

当前推荐技术路线为：

- Authoring / UI：`TypeScript + React`
- Desktop Host：`Tauri`
- Infra Core：`Rust`
- Runtime / Toolchain / Integration：`C#`
- Spec / Contract：语言无关

这不是“某项技术最流行”的选择，而是从未来商业级外部工具的长期结构反推出来的结果。

### 5.2 为什么 Authoring Core 选择 TypeScript

Authoring 层的核心挑战是：

- 节点图交互
- Inspector 面板生成
- Timeline 组织
- 文本编辑器集成
- 可视化预览
- 插件化 UI
- 迭代速度

因此 TypeScript 更适合承担：

- 外部编辑器工作流
- Schema 驱动 UI
- 状态组织与命令系统
- 前端生态接入

**为什么不选 C# 作为 Authoring Core 主语言**：

- 容易把作者工作流重新拉回引擎或 .NET UI 心智
- 不利于利用成熟 Web 编辑器生态
- 容易让前后端边界再次纠缠

**为什么不选 Rust 作为 Authoring Core 主语言**：

- Authoring 的首要目标是交互和迭代，不是底层性能极限
- 用 Rust 做 UI 主逻辑会显著抬高迭代成本

### 5.3 为什么 Desktop Host 优先考虑 Tauri

Tauri 适合作为当前优先宿主选择，原因包括：

- 更符合“Web Authoring + 本地宿主能力”的分层方式
- 体积和分发相对更轻
- 宿主层权限模型更收敛
- 与 Rust 基础设施层天然契合
- 便于把 C# 工具链作为外部进程或 sidecar 调度

**但要强调**：

Tauri 是当前优先宿主方案，不应被视为不可替代的架构核心。

宿主能力必须通过抽象接口暴露，避免未来替换宿主时影响 Authoring Core 与 Toolchain Core。

### 5.4 为什么 Infra Core 选择 Rust

Rust 适合承担：

- 文件监听
- Workspace 索引
- 增量缓存
- 后台任务
- IPC 与宿主桥接
- 进程调度

它的价值在于：

- 能把桌面宿主能力和业务语义隔离开
- 对未来商业级工具的稳定性、边界控制和性能有利
- 与 Tauri 宿主路线一致

### 5.5 为什么 Runtime / Toolchain 继续选择 C#

C# 继续作为 Runtime 与 Toolchain 核心语言，原因包括：

- 能最大程度复用既有积累
- Unity 集成天然顺手
- Godot 首批也可用 C# 快速接入
- 适合作为导出链、校验器和解释器核心
- 可把运行时与引擎适配结合得更自然

当前方向是：

- Toolchain CLI 优先面向现代 .NET
- 共享抽象层根据引擎兼容需求控制目标框架
- Runtime Core 保持零 Unity API 依赖

### 5.6 为什么不以 Electron 作为当前首选

Electron 的优势在于：

- 生态成熟
- 案例多
- 跨平台渲染一致性较强
- 原型推进通常更稳

但不将其作为当前首选的原因是：

- 体积与资源开销更重
- 宿主安全面更宽
- 对未来商业工具的分发体验和资源占用不够理想

**判断**：
Electron 是合理备选，但不是当前首推主线。

### 5.7 为什么不以 Avalonia 作为当前主线

Avalonia 的优点是：

- .NET 一体化较强
- 跨平台原生 UI 路线清晰
- 渲染一致性更可控

但不作为当前主线的原因是：

- 会把编辑器主心智拉回 .NET UI
- 不利于充分利用 Web 编辑器与可视化生态
- 与未来外部编辑器插件化、嵌入式 Web 工作流相比不够灵活

### 5.8 为什么不以 Wails 或 Qt 作为当前主线

**Wails**：

- 本质上也是 Web + 原生宿主
- 但如果再引入 Go，会让语言版图进一步复杂化
- 相比之下，Rust 与 Tauri 的组合和当前路线更统一

**Qt**：

- 功能强、成熟、商业化经验丰富
- 但技术与许可负担更重
- 当前阶段不适合作为第一优先路线

### 5.9 当前技术选型结论

当前最推荐的技术方向不是“某个框架决定一切”，而是：

- `Authoring Core Web-first`
- `Desktop Host Tauri-first`
- `Infra Core Rust-first`
- `Runtime / Toolchain C#-first`
- `Contract Language-agnostic`

---

## 6. 核心数据边界与主链路

### 6.1 三层核心文件边界

SceneBlueprint 当前长期稳定的核心数据边界应收敛为三层：

1. 定义输入层
- `.sbdef` 或同类定义文件
- 用于描述 Action、Marker、端口、元数据和编辑器描述来源

2. Authoring Source
- `.blueprint.json`
- 用于保存节点图、时间轴、属性编辑、空间语义绑定等编辑态信息

3. Runtime Contract
- `runtime json` 或等效编译产物
- 用于运行时装载、校验、调试和引擎侧消费

### 6.2 编辑态主链路

```
定义输入层（.sbdef）
    ↓
Toolchain 生成 authoring descriptors / runtime definitions
    ↓
外部编辑器加载 descriptors
    ↓
编辑器产出与维护 .blueprint.json
    ↓
导出链将 .blueprint.json 编译为 runtime contract
    ↓
引擎适配层导入与消费 runtime contract
```

### 6.3 运行态主链路

```
runtime contract
    ↓
Runtime Core / Runner
    ↓
Systems / State / Blackboard
    ↓
Engine Integration Adapter
    ↓
Unity / Godot / 其他引擎对象与资源
```

### 6.4 同步策略

当前基线同步策略：

- 文件驱动同步
- 引擎侧监听与刷新
- 外部编辑器导出稳定契约

增强同步策略：

- 会话桥
- 实时聚焦
- 调试状态同步
- 场景摘要推送

增强同步不能替代基线同步。

### 6.5 当前不提前锁死的内容

以下内容当前不在本架构文档中提前锁死：

- `.sbdef` 精确语法
- `.blueprint.json` 最终字段全集
- runtime contract 的精确字段命名
- 内部状态容器与类层级
- 具体 UI 组件树

这些应在实现阶段逐步收敛，并通过 schema 与测试样本沉淀。

---

## 7. 多引擎集成策略

### 7.1 核心思想

多引擎支持不是让多个引擎共同决定 Authoring 模型，而是让多个引擎共同消费同一份中立契约。

### 7.2 引擎集成层职责

每个引擎的 Integration 层负责：

- runtime contract 装载
- 引擎对象映射
- 资源解析与引用桥接
- 场景摘要采集
- 调试与聚焦接口

每个引擎的 Integration 层不负责：

- authoring schema 定义
- 节点图 UI
- 导出规则定义
- 跨引擎公共概念命名

### 7.3 优先顺序

| 引擎 | 优先级 | 当前判断 |
|------|--------|----------|
| Unity | P0 | 首批重点适配对象 |
| Godot 4 | P1 | 首批共同验证多引擎边界 |
| Unreal 5 | P2 | 后续单独投入 C++ 集成 |
| 其他引擎 | P3 | 等核心 contract 稳定后扩展 |

### 7.4 当前集成策略结论

- Unity 与 Godot 首批优先使用 C# 集成
- Unreal 若进入路线图，应单独作为 C++ Integration 项处理
- 所有集成都围绕 contract，而不是围绕作者模型重做一套编辑器

---

## 8. 行业案例启发

本节不用于做竞品抄袭，而用于提炼外部编辑器工具的共同成功模式。

### 8.1 参考案例类型

当前对 SceneBlueprint 最有参考价值的案例是：

- `articy:draft`：外部叙事与流程编辑器 + 引擎导入
- `Spine`：外部动画编辑器 + 多引擎 runtime
- `Tiled`：外部地图编辑器 + 结构化项目文件
- `Wwise`：外部 Authoring 应用 + Integration + Runtime SDK
- `Houdini + Houdini Engine`：外部内容生产工具 + 引擎侧参数化消费与烘焙

### 8.2 这些成功案例的共同模式

这些工具长期成功，通常都具备以下共性：

- 外部编辑器才是语义中心
- Authoring Source 与 Runtime Artifact 分离
- 文件和工程结构适合版本控制
- 引擎侧集成相对薄
- 运行时或导入 SDK 官方维护
- 自动化入口明确
- 版本兼容规则明确

### 8.3 SceneBlueprint 应吸收的模式

SceneBlueprint 应重点吸收以下模式：

- 把外部编辑器做成主产品，而不是附属工具
- 把引擎插件做成集成层，而不是主工作流入口
- 把 contract 做成跨引擎稳定中枢
- 把导出、验证、批处理视为正式能力
- 把版本兼容和 schema 测试前置

### 8.4 SceneBlueprint 不应照搬的方向

SceneBlueprint 不应回退到以下路线：

- 以 Unity 内嵌编辑器为事实主入口
- 以某个引擎资产格式作为通用 authoring 格式
- 让运行时结构直接绑死编辑结构
- 让宿主框架取代产品边界成为核心资产

---

## 9. 实施顺序建议

### 9.1 当前推荐顺序

从当前阶段到未来正式产品，推荐按以下顺序推进：

1. 先明确最小数据边界
- 定义输入层
- `.blueprint.json`
- runtime contract

2. 先做 Web-first 的 Authoring 原型
- 验证节点图、Inspector、Timeline、日志区
- 不急于一开始就押注完整桌面宿主细节

3. 再接入桌面宿主能力
- 文件系统
- 打开/保存
- watch
- 进程调度

4. 再打通导出链与引擎接入
- Headless export
- runtime contract 校验
- Unity / Godot 消费闭环

5. 最后增强实时联动与高级能力
- 会话桥
- 调试联动
- 场景摘要增量同步
- 性能优化

### 9.2 当前阶段优先事项

当前阶段优先关注：

- 边界清晰
- 最小闭环
- 导出稳定
- 文件结构可靠
- 技术路线不过早失控

当前阶段不优先追求：

- 一次性穷举全部概念
- 过度复杂的底层抽象
- 过早的多端同步系统
- 大而全的引擎集成范围

---

## 10. 风险与约束

### 10.1 技术风险

| 风险 | 影响 | 当前判断 |
|------|------|----------|
| Tauri 生态与系统 WebView 差异 | 中 | 可接受，但应保持宿主可替换 |
| 多引擎集成复杂度 | 高 | 必须依赖 contract 稳定与薄集成策略 |
| Runtime 与 Authoring 边界漂移 | 高 | 必须持续通过 schema 与 golden tests 约束 |
| 外部编辑器功能膨胀 | 高 | 需要坚持阶段交付和边界控制 |
| 概念体系过早固化 | 中 | 采用分层演进与概念登记策略 |

### 10.2 当前约束

- 架构文档不提前锁死大量具体代码结构
- 实现阶段允许试错，但不得破坏核心边界
- 引擎集成层不得反向定义 authoring 结构
- 宿主技术应服务架构，而不是主导架构

### 10.3 当前架构判断总结

在未来希望把 SceneBlueprint 打磨成商业级外部工具的前提下，当前最合理的总体方向是：

- 用 Web 技术承载 Authoring Core
- 用桌面宿主承载本地工具能力
- 用 C# 保持 Toolchain 与 Runtime 优势
- 用中立 contract 承担多引擎稳定边界
- 用文件驱动作为基础，用实时联动作为增强

---

## 11. 附录

### 11.1 术语表

> 说明：术语表不是一次性定稿。SceneBlueprint 的概念会按 `Spec / Authoring / Compile / Runtime / Integration` 各层逐步沉淀，当前阶段不追求穷举。

| 术语 | 定义 |
|------|------|
| Blueprint | 场景蓝图，描述场景行为流程的编辑态数据 |
| Action | 蓝图中的执行单元 |
| Marker | 场景中的空间参考点或逻辑标记 |
| Authoring Source | 编辑器维护的源数据 |
| Runtime Contract | 导出后供运行时消费的中立契约 |
| Toolchain | 解析、生成、校验、导出的工具链 |
| Integration | 各引擎侧的适配与桥接层 |
| Session Bridge | 外部编辑器与引擎间的实时通信桥 |

### 11.2 概念登记方式

后续新增概念时，按“登记”而非“立刻定稿”的方式维护，避免过早抽象。

| 字段 | 说明 |
|------|------|
| 名称 | 当前使用名称 |
| 所属层 | Spec / Authoring / Compile / Runtime / Integration |
| 状态 | Core / Derived / Tentative |
| 是否持久化 | 是否进入文件格式、schema 或 contract |
| 是否跨模块共享 | 是否会成为稳定边界的一部分 |
| 备注 | 是否可能改名、降级或拆分 |

### 11.3 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-03-28 | SceneBlueprint 明确定位为外部编辑器工具 | 避免重新回到单引擎插件心智 |
| 2026-03-28 | 不采用单一语言通吃 | Authoring、宿主、Runtime、Integration 诉求差异过大 |
| 2026-03-28 | TypeScript 作为 Authoring Core | 最贴合外部编辑器 UI 与工作流 |
| 2026-03-28 | Rust 作为 Infra Core | 更适合作为宿主与本地基础设施层 |
| 2026-03-28 | C# 保留为 Runtime / Toolchain / 首批引擎集成核心 | 复用既有积累，利于 Unity/Godot 接入 |
| 2026-03-28 | 文件驱动同步作为基线 | 稳定、可版本控制、适合多引擎 |
| 2026-03-28 | 实时联动只作为增强层 | 避免把基础链路建立在脆弱通信上 |
| 2026-03-28 | 宿主层不视为核心资产 | 防止技术框架绑死产品架构 |
| 2026-03-28 | 当前许可证切换为 Apache-2.0 | 更利于后续商业级开源与企业采用 |

### 11.4 参考方向

- 外部编辑器案例：articy:draft、Spine、Tiled、Wwise、Houdini
- 技术方向：Tauri、React、Monaco、Three.js、现代 .NET
- 集成方向：Unity、Godot、后续 Unreal

---

## 文档信息

- 版本：v1.4
- 创建日期：2026-03-26
- 更新说明：
  - v1.4：补充当前实现状态与许可证决策，明确第一阶段 Tauri 桌面盒子已建立
  - v1.3：强化“外部编辑器工具”定位，补充外部编辑器架构原则、技术选型理由、行业案例启发与长期边界约束

---

**后续更新原则**：

- 当实现层验证出新的稳定边界时，再把它们提升到本架构文档
- 当只是阶段性实现方案变化时，优先更新《实现与迭代文档》
- 当某项技术选型发生变化时，必须先重新审视本文件中的架构原则是否仍然成立

