# SceneBlueprint

SceneBlueprint 是一个面向游戏开发的引擎无关外部场景蓝图编辑器，目标是为 Unity、Godot 等引擎提供统一的场景蓝图 Authoring Source、导出链与运行时契约。

它不再把任一引擎内的编辑器窗口作为主工作流入口，而是把外部桌面编辑器作为蓝图制作中心，把各游戏引擎集成层视为导入、同步、预览、运行时接线与项目适配入口。

## 项目背景

当前项目并不是从零开始，而是建立在两个已有开源项目的实践基础上继续演进：

- [com.zgx197.sceneblueprint](https://github.com/zgx197/com.zgx197.sceneblueprint)：早期面向 Unity 的场景蓝图框架，验证了场景蓝图的 DSL、编辑器与运行时分层、导出契约、解释执行等核心设计方向。
- [com.zgx197.nodegraph](https://github.com/zgx197/com.zgx197.nodegraph)：早期节点图底座，验证了节点图数据模型、编辑交互、GraphFrame 渲染描述、Unity 宿主适配等能力。

可以把当前仓库理解为：在前两个项目验证过基础方向之后，针对更复杂的场景蓝图 Authoring 需求，进一步升级出来的新一代外部编辑器形态。

## 为什么转向外部编辑器

随着场景蓝图设计不断复杂化，继续在 Unity 内部基于 IMGUI 低成本迭代主编辑器已经不可接受，主要问题集中在：

- 性能成本不可接受：复杂节点图、面板联动、工作区状态恢复、分析与调试视图叠加后，IMGUI 方案难以稳定支撑高性能交互体验。
- 功能完成度不可接受：多窗口协作、复杂工作台布局、现代化桌面交互、后续更强的调试与可视化能力，在 Unity 内嵌 IMGUI 体系里实现成本过高。
- 演进成本不可接受：当 Authoring 复杂度继续上升时，编辑器 UI、引擎宿主限制与业务逻辑会越来越紧耦合，后续维护与扩展代价会持续放大。

因此，SceneBlueprint 选择把“蓝图制作工具”正式迁移到引擎无关的外部编辑器中实现。

这并不意味着放弃 Unity、Godot 等引擎生态，而是重新划分职责：

- 外部编辑器负责 Authoring、可视化编辑、校验、调试、导出与内容工作流。
- 引擎集成层负责资源落地、项目同步、运行时接线、预览桥接与引擎侧适配。
- 运行时与契约层继续保持可被不同引擎消费的清晰边界。

## 项目定位

- 外部编辑器优先，不把任一引擎插件作为主工作流入口
- Authoring Source、Runtime Contract、Engine Integration 分层演进
- `TypeScript + React` 负责 Authoring/UI，`Rust + Tauri` 负责桌面宿主与基础设施，`C#` 负责 Toolchain / Runtime / Integration
- 面向可扩展、可集成、可持续演进的场景蓝图工具链

## 公开入口

- [项目主页（GitHub Pages）](https://zgx197.github.io/SceneBlueprint/)
- [GitHub Releases](https://github.com/zgx197/SceneBlueprint/releases)
- [对外文档索引](./docs/public/README.md)
- [开发文档索引](./docs/development/README.md)

## 发布与下载

- 正式版本会在推送 `v*` Git tag 后自动发布到 [GitHub Releases](https://github.com/zgx197/SceneBlueprint/releases)
- Windows 安装包、MSI 安装包、免安装绿色版与桌面构建产物会自动附加到对应 Release
- 对外发布说明可查看 [发布与下载文档](./docs/public/releases.md)

## 本地运行

1. 安装 Node.js、Rust、Visual Studio C++ Build Tools 与 WebView2 Runtime
2. 在仓库根目录执行 `npm install`
3. 执行 `npm run dev`

如果只需要验证前端工作台，可执行 `npm run dev:web`。

## 仓库结构

- `src/`：前端工作台与 Authoring 壳层
- `src-tauri/`：Tauri / Rust 桌面宿主
- `schemas/`：Schema 与后续契约定义位置
- `examples/`：样例工程与验证数据
- `toolchain/`：后续 C# 工具链入口
- `integrations/`：后续 Unity / Godot 等引擎集成
- `docs/public/`：对外说明文档
- `docs/development/`：内部设计、实现与迭代文档

## License

本项目使用 [Apache License 2.0](./LICENSE)。
