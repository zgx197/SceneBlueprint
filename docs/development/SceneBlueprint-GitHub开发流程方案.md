# SceneBlueprint GitHub 开发流程方案

> 版本：v0.1  
> 日期：2026-03-28  
> 状态：第一阶段基础流程已落地

---

## 1. 文档定位

本文档用于记录 SceneBlueprint 当前阶段最合适的第一套 GitHub 开发流程方案。

本文档关注的是：

- 仓库级开发流程
- GitHub Actions 分层方式
- 分支与保护策略
- 文档与 Pages 组织方式
- 哪些流程适合未来沉淀为 Agent Skill 或 MCP 服务

本文档不负责：

- 具体业务代码实现
- 编辑器功能设计细节
- 引擎集成实现细节

---

## 2. 当前仓库现状

截至当前阶段，SceneBlueprint 已具备以下基础：

- 已建立 `TypeScript + React + Tauri + Rust` 的第一阶段桌面盒子骨架
- 已具备基础前端检查命令：`npm run check`
- 已具备基础前端构建命令：`npm run build:web`
- 已具备 Rust 宿主基础编译检查：`cargo check`
- 已建立提交规范校验：`commit-policy.yml`
- 已将对外文档与开发文档分层：
  - `README.md` 作为对外入口
  - `docs/public/` 作为对外文档目录
  - `docs/development/` 作为内部开发文档目录

这意味着 SceneBlueprint 已经具备建立第一套 GitHub 开发流程的基础条件，但还不适合一开始就引入过重、过复杂的自动化体系。

---

## 3. 当前需要解决的问题

从当前仓库状态和后续方向看，主要问题包括：

### 3.1 提交流程需要长期稳定

当前已经补上了提交作者与 commit message 的远端校验，但还需要把这套规则视为仓库长期约束，而不是临时修补。

目标是保证：

- 作者身份稳定
- 历史可追踪
- 提交语义清晰
- 后续变更日志、发布记录、自动摘要可持续使用

### 3.2 前端壳与桌面宿主需要最小质量门

当前最容易在快速迭代中被改坏的部分是：

- 前端类型检查
- 前端构建链路
- Tauri / Rust 宿主最小编译链路

如果没有最小质量门，后续工作台演进会不断积累隐性破坏。

### 3.3 文档站点与开发文档还没有完全形成闭环

当前已经做了文档分层，但还没有形成：

- 对外文档入口的自动发布
- Pages 站点的持续构建
- 内部开发文档与公开文档的正式边界说明

### 3.4 当前阶段不适合直接引入过重流程

SceneBlueprint 目前仍处于“盒子建立后，逐步往里填内容”的阶段。

因此当前不适合直接引入：

- 全平台矩阵构建
- 复杂 benchmark 流程
- 覆盖率上传
- 自动 release 打包
- 大量模块化 workflow

当前更适合建立一套“轻量但稳定”的第一版流程。

---

## 4. 第一套 GitHub 开发流程目标

第一套流程建议围绕以下目标建立：

1. 保证提交历史和基本规范稳定
2. 保证 Web 工作台和 Tauri 宿主不被轻易改坏
3. 保证对外文档入口可持续建设
4. 保持流程轻量，不拖慢当前快速迭代
5. 为后续 Toolchain、Integration、性能测试、发布流程留出演进空间

---

## 5. 当前推荐的 Workflow 结构

### 5.1 保留：提交规范校验

建议保留当前的：

- `commit-policy.yml`

职责：

- 校验 author 必须为 `zgx197`
- 校验 commit message 必须带前缀
- 校验 commit message 必须包含中文描述

定位：

- 这是仓库纪律层，不负责构建、不负责测试，只负责保证历史质量

### 5.2 新增：前端 CI

建议新增：

- `frontend-ci.yml`

建议执行内容：

- `npm install`
- `npm run check`
- `npm run build:web`

建议触发条件：

- `push` 到 `master`
- `pull_request` 指向 `master`

定位：

- 保证前端工作台壳层、类型检查和构建链路稳定
- 这是当前最优先的一层质量门

### 5.3 新增：Tauri 宿主 CI

建议新增：

- `tauri-host-ci.yml`

建议执行内容：

- 安装 Rust toolchain
- 在 `src-tauri/` 中执行 `cargo check`

建议触发条件：

- `push` 到 `master`
- `pull_request` 指向 `master`

当前建议：

- 第一版优先只跑 `windows-latest`
- 不急于一开始做多平台矩阵

原因：

- 当前主要开发环境是 Windows
- 当前阶段优先验证宿主盒子稳定性，而不是立即做跨平台发布保证

### 5.4 新增：Pages 文档流程

建议新增：

- `docs-pages.yml`

第一版建议只做：

- 构建 `docs/public/` 的公开文档入口
- 发布 GitHub Pages
- 不直接把 `docs/development/` 暴露为主站点入口

定位：

- 建立公开文档入口
- 为后续产品概览、快速开始、集成说明、FAQ 留位置
- 不把内部开发状态直接写进公开文档

---

## 6. 当前不建议立即引入的流程

以下流程很重要，但当前阶段不建议立即引入：

### 6.1 Toolchain CI

原因：

- 当前 C# toolchain 目录仍是占位阶段
- 过早增加会带来空流程维护成本

### 6.2 UI 自动化冒烟测试

原因：

- 当前界面骨架刚建立
- 交互模型和页面结构仍会频繁变化

### 6.3 性能基准流程

原因：

- 当前还没有稳定的性能样本和关键指标
- 过早做 benchmark 会让数据没有长期意义

### 6.4 自动 Release / 桌面打包发布（已具备更稳的手动链路）

当前已经补充了一条更稳的手动桌面发布链路，用于 MVP 阶段打通：

- `release-desktop.yml` 同时支持 `workflow_dispatch` 与 `push tags` 自动发布
- 当前优先支持 Windows 构建
- CI 的 Node 运行环境已统一提升到 22
- 默认自动读取仓库当前版本号
- 支持在触发时手动输入版本号，并在 workflow 内临时对齐构建版本
- 支持按需选择草稿发布或正式发布
- 稳定版可通过推送 `v*` tag 自动触发正式 Release
- 当前会自动生成中文 Release 说明、写明各下载文件用途，并创建或更新基于版本号的 GitHub Release

但以下内容仍然没有完全稳定：

- 正式签名策略
- 多平台发布矩阵
- 自动化版本推进规则
- 面向正式用户的发布节奏
---

## 7. 推荐的分支与保护策略

当前虽然是单人开发，但仍建议尽早建立轻量分支保护。

### 7.1 当前推荐策略

建议对 `master` 采用以下规则：

- 开启 branch protection
- 要求以下 workflow 通过：
  - `Commit Policy`
  - `Frontend CI`
  - `Tauri Host CI`
- 暂不强制 code review
- 暂不强制 squash merge
- 重要改动优先通过 PR 合入

### 7.2 为什么当前不建议过重保护

原因：

- 当前仍处于快速搭骨架阶段
- 流程应该帮助开发，而不是阻塞开发
- 单人开发阶段更需要的是“可验证”而不是“审批流程”

---

## 8. 文档与 Pages 的边界建议

当前文档建议继续坚持两层结构：

### 8.1 对外文档

放置位置：

- `README.md`
- `docs/public/`

内容特点：

- 面向仓库访客、使用者、潜在贡献者
- 不写内部阶段推进状态
- 不写实现中间判断
- 强调项目定位、能力边界、公开使用方式

### 8.2 开发文档

放置位置：

- `docs/development/`

内容特点：

- 允许记录阶段状态
- 允许记录权衡、风险、待办、实验性判断
- 面向项目开发，而不是对外展示

### 8.3 Pages 第一版建议

GitHub Pages 第一版建议只发布：

- 产品概览
- 快速开始
- 文档入口索引
- 未来公开报告入口

当前不建议：

- 直接把全部开发文档当成公开站点主体
- 把内部实现状态作为主页核心内容

---

## 9. SceneBlueprint 的流程演进路线

建议按阶段推进，而不是一次性上全套流程。

### 9.1 第一阶段：建立最小质量门

建议完成：

- `commit-policy.yml`
- `frontend-ci.yml`
- `tauri-host-ci.yml`
- `docs-pages.yml`

### 9.2 第二阶段：接入 Toolchain CI

当 C# toolchain 启动后，建议新增：

- `toolchain-ci.yml`

建议执行内容：

- `dotnet restore`
- `dotnet build`
- 最小 validate / export 冒烟

### 9.3 第三阶段：接入 UI 冒烟测试

当工作台交互开始稳定后，建议新增：

- `ui-smoke.yml`

目标：

- 验证主工作台可打开
- 验证关键面板能加载
- 验证基本交互不会直接报错

### 9.4 第四阶段：接入性能冒烟与代表性样本

当性能样本形成后，建议新增：

- `performance-smoke.yml`
- `performance-full.yml`

### 9.5 第五阶段：接入发布流程

当桌面应用准备对外发布后，建议新增：

- `release-desktop.yml`

---

## 10. 哪些能力适合做成 Agent Skill

Agent Skill 更适合沉淀“固定步骤明确、偏本地开发协作、适合标准化复用”的能力。

### 10.1 适合做成 Agent Skill 的方向

1. `scene-blueprint-ci-auditor`
- 用于检查当前仓库已有 workflow、命令入口、文档边界是否一致
- 适合做“仓库开发流程巡检”

2. `scene-blueprint-doc-boundary`
- 用于检查 `README`、`docs/public/`、`docs/development/` 是否混写
- 适合做“文档边界自检”

3. `scene-blueprint-commit-policy-helper`
- 用于生成规范 commit message 模板
- 用于提醒当前改动更适合 `feat/fix/docs/chore` 中哪一类

4. `scene-blueprint-workflow-bootstrap`
- 用于根据当前仓库结构生成最小 workflow 草案
- 适合仓库初始化阶段快速搭流程

### 10.2 为什么这些更适合 Skill

因为它们的特点是：

- 强依赖仓库内上下文
- 更像“标准化协作流程”
- 不一定需要长期在线服务
- 更适合以可复用方法论形式沉淀

---

## 11. 哪些能力适合做成 MCP 服务

MCP 服务更适合沉淀“需要持续访问外部系统、跨会话上下文、自动汇总 GitHub 状态”的能力。

### 11.1 适合做成 MCP 服务的方向

1. `github-workflow-observer`
- 拉取 workflow 状态、最近失败记录、失败趋势
- 汇总当前分支或 PR 的质量门状态

2. `github-pages-publisher`
- 管理 Pages 站点入口、构建产物、报告索引更新
- 适合后续公开站点逐步完善后使用

3. `release-notes-collector`
- 根据 commit 历史、PR、标签生成阶段变更摘要
- 适合后续 release 或 milestone 汇总

4. `project-governance-reporter`
- 汇总提交规范执行情况、失败工作流统计、质量门通过率
- 适合项目逐步变大后做治理观察

### 11.2 为什么这些更适合 MCP

因为它们通常需要：

- 持续连接 GitHub 或其他外部系统
- 跨会话保存上下文
- 统一查询状态而不是一次性本地执行
- 输出长期趋势，而不是单次动作建议

---

## 12. 当前最推荐的落地顺序

如果只按“最小必要集”推进，当前最推荐顺序是：

1. 完善现有 `commit-policy.yml`
2. 新增 `frontend-ci.yml`
3. 新增 `tauri-host-ci.yml`
4. 新增 `docs-pages.yml`
5. 在 `docs/public/` 补足最小公开站点骨架
6. 后续再逐步扩展 Toolchain、UI 冒烟、性能流程

这套顺序的关键不是“覆盖所有未来需求”，而是：

- 先保证当前活跃模块稳定
- 先让 GitHub 流程成为正反馈
- 不让 CI 先于产品复杂化

---

## 13. 当前结论

SceneBlueprint 当前最合适的第一套 GitHub 开发流程，应当是：

- 用 `commit-policy` 管仓库纪律
- 用 `frontend-ci` 管前端工作台壳层
- 用 `tauri-host-ci` 管桌面宿主最小质量门
- 用 `docs-pages` 建立对外文档入口
- 用 `release-desktop` 打通 MVP 阶段最小手动发布链路

与此同时：

- 对外文档与开发文档继续保持分层
- 不急于照搬成熟项目的重型 CI
- 把流程设计成可演进结构，而不是一次性定死
- 预留未来沉淀为 Agent Skill 与 MCP 服务的空间

---

## 14. 当前已落地内容

当前已经落地的第一阶段 GitHub 开发流程包括：

- `commit-policy.yml`：提交作者与 commit message 规范校验
- `frontend-ci.yml`：前端类型检查与 Web 构建
- `tauri-host-ci.yml`：Tauri / Rust 宿主最小编译检查
- `docs-pages.yml`：`docs/public/` 的公开文档 Pages 构建与部署
- `docs/public/_config.yml` 与 `docs/public/index.md`：公开站点最小骨架
- `release-desktop.yml`：支持 Node 22、tag 自动发布、手动补发的 Windows 桌面发布流程

这意味着 SceneBlueprint 的第一套 GitHub 开发流程已经从“方案设计”进入“基础落地”阶段，并且已经具备更稳的桌面发布闭环。
