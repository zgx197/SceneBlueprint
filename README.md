# SceneBlueprint

SceneBlueprint 是一个面向游戏开发的引擎无关外部场景蓝图编辑器，目标是为 Unity、Godot 等引擎提供统一的场景蓝图 Authoring Source、导出链与运行时契约。

SceneBlueprint 强调外部编辑器优先、多引擎集成、清晰的数据边界，以及适合版本控制和自动化验证的内容工作流。

## 项目定位

- 外部编辑器优先，不把任一引擎插件作为主工作流入口
- Authoring Source、Runtime Contract、Engine Integration 分层演进
- `TypeScript + React` 负责 Authoring/UI，`Rust + Tauri` 负责桌面宿主与基础设施，`C#` 负责 Toolchain / Runtime / Integration
- 面向可扩展、可集成、可持续演进的场景蓝图工具链

## 文档结构

- [对外文档索引](./docs/public/README.md)
- [开发文档索引](./docs/development/README.md)

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
