---
title: SceneBlueprint
description: SceneBlueprint 对外文档入口
---

# SceneBlueprint

SceneBlueprint 是一个面向游戏开发的引擎无关外部场景蓝图编辑器，目标是为 Unity、Godot 等引擎提供统一的场景蓝图 Authoring Source、导出链与运行时契约。

## 项目定位

- 外部编辑器优先，不把任一引擎插件作为主工作流入口
- 面向多引擎集成，而不是面向单引擎内嵌编辑器
- 强调 Authoring Source、Runtime Contract、Engine Integration 的清晰边界
- 适合版本控制、自动化校验与长期演进的内容工作流

## 快速开始

1. 安装 Node.js、Rust、Visual Studio C++ Build Tools 与 WebView2 Runtime
2. 克隆仓库并执行 `npm install`
3. 在仓库根目录执行 `npm run dev`

如果只需要验证前端工作台，可执行 `npm run dev:web`。

## 文档入口

- [仓库首页](https://github.com/zgx197/SceneBlueprint)
- [对外文档目录（GitHub）](https://github.com/zgx197/SceneBlueprint/tree/master/docs/public)
- [开发文档目录（仓库内）](https://github.com/zgx197/SceneBlueprint/tree/master/docs/development)

## 当前公开信息范围

本 Pages 站点用于承载：

- 产品概览
- 快速开始
- 后续公开使用文档
- 后续集成说明与发布说明

内部设计、实现状态与阶段记录继续保留在仓库开发文档中，而不作为公开主页内容。

