# SceneBlueprint 文档归档目录

> 目录用途：存放已归档（`doc_status: archived`）或已废弃（`doc_status: deprecated`）且不再作为主线依据的文档。

## 归档规则

1. 仅迁移已在 `文档导航.md` 中标记为 `archived` / `deprecated` 的文档。
2. 迁移前先在原文档头部补充：
   - `doc_status`
   - `last_reviewed`
   - `superseded_by`（若有替代文档）
3. 迁移后必须更新 `文档导航.md` 的路径。
4. 与当前实现强绑定、仍被频繁引用的文档可先保留在原位置，仅通过 `doc_status` 控制优先级。

## 当前策略

当前阶段已执行“硬归档”：
- `deprecated` / `archived` 文档已迁移到 `_archive/` 或 `_archive/Tests/`；
- `文档导航.md` 与主文档引用已同步到归档路径。
