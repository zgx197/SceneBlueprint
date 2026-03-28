# SceneBlueprint

SceneBlueprint 是一个面向游戏开发的引擎无关外部场景蓝图编辑器，目标是成为 Unity、Godot 等多引擎共享的 Authoring Source、导出链与运行时契约上游。

当前仓库已经完成第一阶段 Tauri 桌面盒子搭建，具备基础桌面宿主、前端工作台骨架与最小前后端通信能力。后续迭代将在这个盒子里继续填充 Authoring、Toolchain、Runtime Contract 与多引擎 Integration。

## 项目定位

- 外部编辑器优先，不把任一引擎插件作为主工作流入口
- Authoring Source、Runtime Contract、Engine Integration 分层演进
- `TypeScript + React` 负责 Authoring/UI，`Rust + Tauri` 负责桌面宿主与基础设施，`C#` 负责 Toolchain / Runtime / Integration
- 先建立可运行闭环，再持续补充自测、性能观测和多引擎适配

## 当前阶段

- 第一阶段目标是先搭出 Tauri 桌面盒子，而不是一次性实现完整编辑器
- 当前已具备 Web 工作台骨架、Rust 宿主工程、基础命令通路与桌面窗口启动链路
- 当前目录结构以可扩展为目标，后续会随着 Authoring、Contract、Toolchain 的稳定程度继续演进

## 仓库文档

- [架构设计文档](./SceneBlueprint-%E6%9E%B6%E6%9E%84%E8%AE%BE%E8%AE%A1%E6%96%87%E6%A1%A3.md)
- [实现与迭代文档](./SceneBlueprint-%E5%AE%9E%E7%8E%B0%E4%B8%8E%E8%BF%AD%E4%BB%A3%E6%96%87%E6%A1%A3.md)

## 本地运行

1. 安装 Node.js、Rust、Visual Studio C++ Build Tools 与 WebView2 Runtime
2. 在仓库根目录执行 `npm install`
3. 执行 `npm run dev`

如果只需要验证前端工作台，可执行 `npm run dev:web`。

## 当前仓库结构

- `src/`：前端工作台与 Authoring 壳层
- `src-tauri/`：Tauri/Rust 桌面宿主
- `schemas/`：Schema 与后续契约定义位置
- `examples/`：样例工程与验证数据
- `toolchain/`：后续 C# 工具链入口
- `integrations/`：后续 Unity / Godot 等引擎集成
- `docs/`：补充设计、记录与后续专题文档

## 开发约定

- 先稳定边界，再补复杂功能
- 架构文档描述长期设计边界，实现文档描述当前阶段落地顺序
- 尽量让文件格式、样例与自动化验证先于宿主细节固化

## License

本项目使用 [Apache License 2.0](./LICENSE)。
