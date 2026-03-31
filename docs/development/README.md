# 开发文档（当前进度：P6 总进度 4/4，严格 100% 已完成）

本目录用于存放 SceneBlueprint 的内部设计、架构讨论、实现规划、阶段记录与实验性文档。

## 当前文档

- [架构设计文档](./SceneBlueprint-架构设计文档.md)
- [实现与迭代文档](./SceneBlueprint-实现与迭代文档.md)
- [GitHub 开发流程方案](./SceneBlueprint-GitHub开发流程方案.md)
- [Graph Workspace 最小接口草案](./GraphWorkspace-最小接口草案.md)
- [Phase 1 Notes](./phase-1-notes.md)

## 说明

- 本目录主要服务项目开发，不作为对外产品说明入口。
- 文档允许记录阶段判断、取舍、风险和实现状态。
- Graph 相关进度、迁移判断与当前阶段收口，统一回写到 `SceneBlueprint-实现与迭代文档.md`。
- 对外说明应优先沉淀到 `README.md` 或 `docs/public/`。

## 当前状态（中文进度）

- Graph Workspace 当前中文进度：`P0-P5` 已完成，`P6` 已严格收口到 `4/4`。
- `P6` 当前中文进度：`validate / export`、`深桥接`、`controller 瘦身`、`问题反馈 + 最小 UI 冒烟` 已全部完成。
- 旧 `com.zgx197.nodegraph` 中最重要的框架资产已完成迁移与重做；剩余未迁项当前不再追求 `1:1` 继续迁移。
- 已建立 `Vitest` 图编辑专项测试底座，并已覆盖运行时迁移、连接策略、命令历史、帧构建、序列化、桥接契约、图算法、graph behavior、子图语义、kernel 与正式导出主链路。
