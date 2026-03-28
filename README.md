# SceneBlueprint

SceneBlueprint 是一个引擎无关的场景蓝图编辑与运行框架，采用外部编辑器优先的架构，面向 Unity、Godot 等多引擎集成。

当前仓库处于重构起点阶段，重点是重新建立清晰的设计边界、实现节奏和后续项目骨架。

## 项目方向

- 外部编辑器优先，而不是以任一引擎内置编辑器为中心
- Authoring、Export、Runtime Contract、Engine Integration 明确分层
- TypeScript、Rust、C# 按模块职责分工
- 通过最小可运行闭环快速迭代，并同步补充自测与性能基线

## 当前文档

- [架构设计文档](./SceneBlueprint-%E6%9E%B6%E6%9E%84%E8%AE%BE%E8%AE%A1%E6%96%87%E6%A1%A3.md)
- [实现与迭代文档](./SceneBlueprint-%E5%AE%9E%E7%8E%B0%E4%B8%8E%E8%BF%AD%E4%BB%A3%E6%96%87%E6%A1%A3.md)

## 当前状态

- 仓库已完成历史 Unity package 内容清理
- 已建立新的设计文档与实现迭代文档基线
- 下一阶段将优先搭建可视化界面、最小数据闭环、自测与性能观测

## 仓库约定

- 优先保持目录清晰、职责单一
- 先验证边界与闭环，再逐步补全功能
- 架构文档与实现文档分离维护

## License

本项目使用 [MIT License](./LICENSE)。
