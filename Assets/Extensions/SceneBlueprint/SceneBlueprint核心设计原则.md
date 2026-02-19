# SceneBlueprint 核心设计原则

> **文档版本**：v1.3  
> **创建日期**：2026-02-16  
> **最后更新**：2026-02-19  
> **状态**：✅ active  
> **重要性**：🔴 核心原则 - 所有功能设计必须遵循
> **doc_status**: active  
> **last_reviewed**: 2026-02-19

---

> ⚠️ **节点变更说明（2026-02-19）**：本文档部分示例引用了以下**已删除节点**，阅读时请忽略这些引用：
> - `Location.RandomArea` / `Location.RandomInArea` — 已删除，场景随机位置路径不再支持
> - `Monster.Pool` — 已删除，怪物配置移至场景层（`WaveSpawnConfig` 标注组件）
> - `Spawn.Execute` — 已删除，被 `Spawn.Preset` + `Spawn.Wave` 覆盖
> - `Behavior.Assign` — 已删除，行为配置移至 `SpawnAnnotation`
> - `Condition.AllDead` — 已删除
>
> **当前有效的刷怪路径**：
> - 路径 A（预设刷怪）：编辑器固化 PointMarker → `Spawn.Preset` 绑定这些 PointMarker
> - 路径 B（波次刷怪）：`Spawn.Wave` 绑定 AreaMarker，波次逻辑配置在节点 `waves` 属性中

---

## 目录

- [一、设计理念](#一设计理念)
  - [1.1 场景中心化原则](#11-场景中心化原则)
  - [1.2 职责分离原则](#12-职责分离原则)
  - [1.3 单向数据流原则](#13-单向数据流原则)
- [二、SceneView 与 Blueprint 的关系](#二sceneview-与-blueprint-的关系)
  - [2.1 SceneView：空间真相的唯一来源](#21-sceneview空间真相的唯一来源)
  - [2.2 Blueprint：逻辑编排与引用](#22-blueprint逻辑编排与引用)
  - [2.3 职责边界表](#23-职责边界表)
- [三、工作流模式](#三工作流模式)
  - [3.1 迭代式工作流（推荐）](#31-迭代式工作流推荐)
  - [3.2 场景优先工作流](#32-场景优先工作流)
  - [3.3 反模式警示](#33-反模式警示)
- [四、编辑器交互设计](#四编辑器交互设计)
  - [4.1 Marker 创建与标注](#41-marker-创建与标注)
  - [4.2 Blueprint 引用机制](#42-blueprint-引用机制)
  - [4.3 双向追溯功能](#43-双向追溯功能)
- [五、技术实现要点](#五技术实现要点)
  - [5.1 Marker ID 管理](#51-marker-id-管理)
  - [5.2 绑定验证](#52-绑定验证)
  - [5.3 导出与运行时](#53-导出与运行时)
- [六、最佳实践](#六最佳实践)
  - [6.1 推荐的场景组织方式](#61-推荐的场景组织方式)
  - [6.2 推荐的蓝图结构](#62-推荐的蓝图结构)
  - [6.3 常见场景示例](#63-常见场景示例)
- [七、编辑器预览刷新与一致性策略](#七编辑器预览刷新与一致性策略)
  - [7.1 设计目标](#71-设计目标)
  - [7.2 事件驱动脏刷新主链路](#72-事件驱动脏刷新主链路)
  - [7.3 精准刷新优化点](#73-精准刷新优化点)
  - [7.4 一致性与兖底策略](#74-一致性与兖底策略)
- [八、Blueprint 定位与编辑器工具边界](#八-blueprint-定位与编辑器工具边界)
  - [8.1 Blueprint 的核心定位](#81-blueprint-的核心定位)
  - [8.2 编辑器工具 vs Blueprint 节点的判定规则](#82-编辑器工具-vs-blueprint-节点的判定规则)
  - [8.3 预设刷怪 vs 运行时随机刷怪——两条路径](#83-预设刷怪-vs-运行时随机刷怪两条路径)
  - [8.4 位置生成工具的策划工作流](#84-位置生成工具的策划工作流)
  - [8.5 反模式：混淆编辑时与运行时](#85-反模式混淆编辑时与运行时)
- [九、PropertyType 扩展：StructList 结构化列表](#九propertytype-扩展structlist-结构化列表)
  - [9.1 背景](#91-背景)
  - [9.2 设计方案](#92-设计方案)
  - [9.3 存储策略](#93-存储策略)
  - [9.4 使用示例](#94-使用示例)
  - [9.5 显示效果](#95-显示效果)
- [十、Trigger 节点统一为条件等待节点](#十trigger-节点统一为条件等待节点)
  - [10.1 背景](#101-背景)
  - [10.2 设计决策](#102-设计决策)
  - [10.3 改动对比](#103-改动对比)
  - [10.4 典型蓝图连接](#104-典型蓝图连接)
  - [10.5 运行时系统](#105-运行时系统)
  - [10.6 System 执行顺序](#106-system-执行顺序)

---

## 一、设计理念

### 1.1 场景中心化原则

**核心理念**：SceneView 是"空间真相的唯一来源"（Single Source of Truth for Spatial Data）

```
SceneView（空间层）
  ├─ 回答所有的"在哪里"问题
  ├─ 定义 Marker 的位置、形状、范围
  └─ 可视化空间关系

Blueprint（逻辑层）
  ├─ 回答所有的"怎么做"问题
  ├─ 定义时序、条件、数据流
  └─ 引用 SceneView 中的 Marker（单向依赖）
```

**为什么重要**：
- ✅ **消除歧义**：当空间数据冲突时，SceneView 是唯一权威
- ✅ **降低复杂度**：避免双向同步带来的状态一致性问题
- ✅ **符合直觉**：策划习惯在场景中思考和操作
- ✅ **提升可维护性**：职责清晰，问题定位容易

---

### 1.2 职责分离原则

**核心理念**：SceneView 负责"是什么"（What），Blueprint 负责"做什么"（Do What）

#### SceneView 的职责

| 职责 | 说明 | 示例 |
|-----|------|------|
| **空间声明** | 声明场景中的空间元素 | "这里是刷怪区域"、"这是触发线" |
| **位置定义** | 定义精确的坐标、形状、范围 | 多边形区域、路径点序列 |
| **视觉标注** | 为策划提供可视化辅助 | 区域颜色、标签、图标 |
| **组织结构** | 标记之间的逻辑分组 | "走廊A的刷怪点"、"Boss房间的机关" |

**SceneView 不负责**：
- ❌ 何时触发（时序）
- ❌ 触发条件（逻辑）
- ❌ 行为控制（AI）
- ❌ 数据流转（连线）

---

#### Blueprint 的职责

| 职责 | 说明 | 示例 |
|-----|------|------|
| **时序编排** | 定义事件的发生顺序 | "第一波 → 第二波 → Boss" |
| **条件逻辑** | 定义触发条件和分支 | "全灭后 → 开门"、"血量<50% → 狂暴" |
| **数据流** | 定义数据的传递和转换 | "位置 + 怪物配置 → 刷怪" |
| **事件响应** | 定义对游戏事件的响应 | "玩家进入 → 触发战斗" |

**Blueprint 不负责**：
- ❌ 创建或修改 Marker
- ❌ 定义空间位置和形状
- ❌ 存储位置坐标数据
- ❌ 管理场景对象的层级关系

---

### 1.3 单向数据流原则

**核心理念**：数据从 SceneView 流向 Blueprint，不回流

```
┌─────────────────────┐
│     SceneView       │  空间数据的定义和存储
│                     │
│  Marker:            │
│    - ID             │
│    - Type           │
│    - Position       │
│    - Shape          │
└──────────┬──────────┘
           │
           │ 提供 Marker IDs（只读引用）
           ↓
┌─────────────────────┐
│     Blueprint       │  引用 Marker IDs，编排逻辑
│                     │
│  Node:              │
│    - BindingId ─────┼→ 引用 Marker ID
│    - Parameters     │
│    - Connections    │
└──────────┬──────────┘
           │
           │ 生成 Playbook
           ↓
┌─────────────────────┐
│      Runtime        │  根据 IDs 查询空间数据，执行逻辑
│                     │
│  1. 解析 Playbook   │
│  2. 查询 Marker     │
│  3. 执行节点逻辑    │
└─────────────────────┘
```

**关键特性**：
- ✅ Blueprint 只读取 Marker ID，不修改 Marker
- ✅ 策划可以随时调整 SceneView，Blueprint 无感知（除非 ID 变化）
- ✅ 运行时通过 ID 动态查询，支持热更新

**避免的反模式**：
- ❌ Blueprint 创建 Marker
- ❌ Blueprint 修改 Marker 属性
- ❌ Marker 存储复杂逻辑配置

---

## 二、SceneView 与 Blueprint 的关系

### 2.1 SceneView：空间真相的唯一来源

#### 核心概念

**Marker**：场景中的空间标记，是 SceneView 的基本元素

```csharp
public class Marker
{
    public string Id;           // 唯一标识（UUID）
    public string Tag;          // 友好名称/分类标签
    public MarkerType Type;     // Point、Area、Path、Volume 等
    public Vector3 Position;    // 世界坐标
    public Quaternion Rotation; // 朝向
    public object ShapeData;    // 类型特定的形状数据
}
```

#### Marker 的类型

| 类型 | 用途 | 数据结构 |
|-----|------|---------|
| **Point** | 单点位置（刷怪点、传送点） | `Vector3 position` |
| **Area** | 平面区域（刷怪区、触发区） | `Vector3[] polygon` |
| **Path** | 路径（巡逻路线、移动轨迹） | `Vector3[] waypoints` |
| **Volume** | 3D 体积（检测区、伤害区） | `Bounds bounds` 或 `Mesh` |

#### Marker 的命名规范

```
推荐格式：<功能>_<区域>_<序号>

示例：
  - spawn_hallway_01       // 走廊刷怪区域 #1
  - trigger_boss_room      // Boss 房间触发器
  - patrol_guard_route_a   // 守卫 A 的巡逻路线
  - teleport_exit          // 出口传送点
```

---

### 2.2 Blueprint：逻辑编排与引用

#### 核心概念

**SceneBinding**：Blueprint 节点对 Marker 的引用

```csharp
public class SceneBinding
{
    public string MarkerId;     // 引用的 Marker ID
    public BindingType Type;    // 期望的 Marker 类型（验证用）
    public bool Required;       // 是否必需
}
```

#### 引用示例

```
Blueprint 节点：
  Location.RandomInArea:
    ├─ areaBinding: 
    │    └─ MarkerId: "spawn_hallway_01"
    │    └─ Type: Area
    │    └─ Required: true
    ├─ count: 5
    └─ minDistance: 3.0

运行时行为：
  1. 通过 "spawn_hallway_01" 查询 Marker
  2. 验证 Marker 类型是否为 Area
  3. 获取 polygon 数据
  4. 在区域内生成 5 个随机位置
```

---

### 2.3 职责边界表

| 问题 | SceneView | Blueprint | 说明 |
|-----|-----------|-----------|------|
| 怪物刷新在哪里？ | ✅ | ❌ | SceneView 定义区域或点位 |
| 怪物何时刷新？ | ❌ | ✅ | Blueprint 定义触发条件 |
| 刷几个怪物？ | ❌ | ✅ | Blueprint 配置数量参数 |
| 区域的形状是什么？ | ✅ | ❌ | SceneView 定义多边形 |
| 刷怪后做什么？ | ❌ | ✅ | Blueprint 连线后续节点 |
| 巡逻路径的点位？ | ✅ | ❌ | SceneView 放置路径点 |
| 是否循环巡逻？ | ❌ | ✅ | Blueprint 配置参数 |
| 触发区域在哪？ | ✅ | ❌ | SceneView 画区域 |
| 触发几次？ | ❌ | ✅ | Blueprint 配置参数 |

---

## 三、工作流模式

### 3.1 迭代式工作流（推荐）

**核心理念**：策划在 SceneView 和 Blueprint 之间迭代，每次只聚焦一个层面

#### 典型迭代流程

```
第 1 轮：快速搭建
  ┌─────────────┐
  │ SceneView   │  画一个刷怪区域（粗略）
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │ Blueprint   │  创建基础流程：Trigger → Spawn
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │   测试      │  "嗯，位置不太对"
  └─────────────┘

第 2 轮：空间调整
  ┌─────────────┐
  │ SceneView   │  调整区域形状，增加巡逻点
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │ Blueprint   │  添加 Behavior.Patrol，连接路径
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │   测试      │  "还不错，但怪物太少"
  └─────────────┘

第 3 轮：参数微调
  ┌─────────────┐
  │ SceneView   │  手动调整个别怪物位置
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │ Blueprint   │  增加怪物数量，调整间隔
  └──────┬──────┘
         ↓
  ┌─────────────┐
  │   测试      │  "完美！"
  └─────────────┘
```

**关键优势**：
- ✅ 每次只改一个层面，降低认知负担
- ✅ 快速看到反馈，及时调整
- ✅ 符合自然的设计思维过程
- ✅ 支持渐进式细化

---

### 3.2 场景优先工作流

**核心理念**：大部分操作在 SceneView 完成，Blueprint 作为辅助

#### 推荐的操作流程

**步骤 1：在 SceneView 中标注**

```
策划在 SceneView 中：
  1. 激活"区域标注工具"
  2. 在场景中画多边形 → 自动创建 Marker
     - ID: 自动生成（如 "area_001"）
     - Tag: 手动输入（如 "SpawnZone_Hallway"）
  3. 继续标注其他空间元素（触发区、巡逻路径等）
```

**步骤 2：快速创建蓝图（可选的自动化）**

```
场景标注完成后：
  右键 Marker → "添加到蓝图"
  
  弹出快捷菜单：
  ┌─────────────────────────┐
  │ 这个区域将用于：        │
  │                         │
  │ ○ 刷怪区域              │
  │ ○ 触发区域              │
  │ ○ 巡逻路径              │
  │ ○ 其他（稍后配置）      │
  │                         │
  │ [创建节点] [取消]       │
  └─────────────────────────┘
  
  选择"刷怪区域" → 自动在 Blueprint 中创建：
    - Location.RandomInArea（绑定此区域）
    - Monster.Pool（默认配置）
    - Spawn.Execute（自动连线）
```

**步骤 3：在 Blueprint 中细化逻辑**

```
切换到 Blueprint 窗口：
  - 调整节点参数（数量、间隔等）
  - 添加条件分支
  - 连接后续行为
```

---

### 3.3 反模式警示

#### ❌ 反模式 1：Blueprint 修改 SceneView

**错误示范**：

```csharp
// 错误：在 Blueprint 节点中创建 Marker
public class SpawnNode
{
    public void Execute()
    {
        // ❌ 不应该在运行时或编辑时创建 Marker
        var marker = SceneMarkerManager.CreateMarker(
            type: MarkerType.Area,
            position: transform.position
        );
    }
}
```

**为什么错**：
- 破坏了"SceneView 是空间真相唯一来源"的原则
- 导致空间数据散落在两处，难以追踪
- 调试困难：策划不知道 Marker 是从哪里来的

**正确做法**：
- 所有 Marker 必须在 SceneView 中手动创建或通过场景工具创建
- Blueprint 只能引用已存在的 Marker

---

#### ❌ 反模式 2：Marker 包含复杂逻辑

**错误示范**：

```csharp
// 错误：在 Marker 中存储逻辑配置
public class SpawnAreaMarker
{
    public Vector3[] Polygon;        // ✅ 空间数据（合理）
    public int SpawnCount;           // ❌ 逻辑配置（不应该在这里）
    public float SpawnInterval;      // ❌ 逻辑配置（不应该在这里）
    public string OnCompleteEvent;   // ❌ 事件逻辑（不应该在这里）
}
```

**为什么错**：
- Marker 应该只描述"是什么"，不描述"做什么"
- 逻辑散落在场景中，难以整体把控流程
- 无法复用同一个 Marker 用于不同的逻辑

**正确做法**：
```csharp
// Marker 只存储空间数据
public class AreaMarker
{
    public string Id;
    public Vector3[] Polygon;  // ✅ 只存储形状
}

// 逻辑配置在 Blueprint 节点中
public class SpawnNode
{
    public string AreaBindingId;   // 引用 Marker ID
    public int SpawnCount;         // ✅ 逻辑参数在这里
    public float SpawnInterval;    // ✅ 逻辑参数在这里
}
```

---

#### ❌ 反模式 3：频繁切换上下文

**低效流程**：

```
策划操作：
1. SceneView：画区域
2. 切换到 BlueprintWindow
3. 创建节点
4. 点击"绑定场景对象"
5. 切回 SceneView 选择区域
6. 切回 BlueprintWindow 配置参数
7. 切回 SceneView 查看效果
8. 切回 BlueprintWindow 调整参数
9. ...（无限循环）
```

**改进方案**：
- 提供"快速创建"功能，在 SceneView 中一键生成基础蓝图
- 实现"视觉投影"，在 SceneView 中预览 Blueprint 逻辑的空间效果
- 支持在 SceneView 中直接调整常用参数（通过浮层面板）

---

## 四、编辑器交互设计

### 4.1 Marker 创建与标注

#### 推荐的 UI 布局

```
SceneView 工具栏：
┌────────────────────────────────────────────┐
│ [选择] [移动] [旋转] │ [画区域▼] [放点] [绘路径] │
└────────────────────────────────────────────┘
                         ↑
                    标注工具组
```

#### 交互流程

**创建 Area Marker**：

```
1. 点击"画区域"按钮
2. 在 SceneView 中点击鼠标左键 → 放置第一个顶点
3. 继续点击 → 添加更多顶点
4. 双击或按 Enter → 完成闭合多边形
5. 弹出属性面板：
   ┌─────────────────────┐
   │ 区域标记            │
   │                     │
   │ 名称: [走廊刷怪区]  │
   │ 标签: [SpawnZone]   │
   │ 颜色: [🔴红色 ▼]   │
   │                     │
   │ [确认] [取消]       │
   └─────────────────────┘
```

**创建 Point Marker**：

```
1. 点击"放点"按钮
2. 在场景中点击 → 放置点位
3. 直接在场景中显示可编辑的标签
4. 拖动点可调整位置
```

**创建 Path Marker**：

```
1. 点击"绘路径"按钮
2. 依次点击路径上的点
3. 按 Enter 完成
4. 路径自动连线，显示方向箭头
```

---

### 4.2 Blueprint 引用机制

#### 绑定操作流程

**在 Blueprint 中绑定 Marker**：

```
1. 创建节点（如 Location.RandomInArea）
2. 点击 "area" 属性旁的 [🔗 绑定] 按钮
3. SceneView 自动进入"选择模式"：
   - 所有 Area 类型的 Marker 高亮显示（蓝色轮廓）
   - 鼠标悬停在 Marker 上 → 显示名称和 ID
   - 点击某个 Marker → 自动绑定
4. 绑定成功后，节点显示：
   ┌────────────────────────┐
   │ Location.RandomInArea  │
   │                        │
   │ 🔗 area: 走廊刷怪区    │  ← 显示友好名称
   │ count: 5               │
   │ minDistance: 3.0       │
   └────────────────────────┘
```

#### 视觉反馈

**绑定后的节点显示**：

```
节点标题栏：
┌────────────────────────┐
│ Location.RandomInArea  │
│ 🔗 走廊刷怪区          │  ← 副标题显示绑定的 Marker
└────────────────────────┘

悬停提示：
┌────────────────────────┐
│ 绑定到场景对象：       │
│   ID: spawn_hallway_01 │
│   类型: Area           │
│   位置: (10, 0, 5)     │
│                        │
│ 双击可在场景中聚焦     │
└────────────────────────┘
```

**未绑定时的提示**：

```
┌────────────────────────┐
│ Location.RandomInArea  │
│                        │
│ ⚠️ area: [未绑定]      │  ← 橙色警告
│ count: 5               │
└────────────────────────┘
```

---

### 4.3 双向追溯功能

#### 从 SceneView 追溯到 Blueprint

```
操作：
  SceneView 中右键 Marker → "在蓝图中查找引用"

结果：
  Blueprint 窗口自动打开并高亮所有引用此 Marker 的节点
  
  示例：
  [Trigger.EnterArea] ← 蓝色高亮框
       ↓
  [Location.RandomInArea] ← 蓝色高亮框
       ↓
  [Spawn.Execute]
       ↓
  [Behavior.Assign]
  
  侧边栏显示：
  ┌─────────────────────────┐
  │ 引用此标记的节点：      │
  │                         │
  │ • Trigger.EnterArea     │
  │   → area 属性           │
  │                         │
  │ • Location.RandomInArea │
  │   → area 属性           │
  │                         │
  │ [关闭]                  │
  └─────────────────────────┘
```

---

#### 从 Blueprint 追溯到 SceneView

```
操作：
  Blueprint 中右键节点 → "在场景中显示绑定对象"
  或：双击绑定属性（如 "🔗 area: 走廊刷怪区"）

结果：
  SceneView 自动：
    1. 聚焦（Frame）到 Marker 位置
    2. 高亮 Marker（闪烁蓝色轮廓）
    3. 显示 Marker 信息面板
    
  ┌─────────────────────┐
  │ 走廊刷怪区          │
  │                     │
  │ ID: spawn_hall_01   │
  │ 类型: Area          │
  │ 顶点数: 6           │
  │ 面积: 45.2 m²       │
  │                     │
  │ 被 2 个节点引用     │
  └─────────────────────┘
```

---

#### 单向视觉投影（推荐功能）

```
操作：
  在 Blueprint 中选中节点（如 Location.RandomInArea）

结果：
  SceneView 自动显示"只读投影"：
    - 绑定的 Area 边界闪烁高亮（蓝色）
    - 预览生成的位置点（半透明绿色圆圈）
    - 节点参数信息浮层显示在场景中
    
  ┌─────────────────────┐
  │ 预览：              │
  │ Location.RandomInArea│
  │                     │
  │ • 5 个生成点        │
  │ • 最小间距: 3.0m    │
  │ • 分布算法: Poisson │
  └─────────────────────┘
```

**关键**：
- 这是只读投影，不修改 SceneView 的实际数据
- 帮助策划理解 Blueprint 逻辑的空间含义
- 取消选中节点 → 投影消失

---

## 五、技术实现要点

### 5.1 Marker ID 管理

#### ID 生成策略

```csharp
public static class MarkerIdGenerator
{
    // 推荐：使用 GUID 确保唯一性
    public static string Generate()
    {
        return Guid.NewGuid().ToString("N"); // 32位16进制字符串
    }
    
    // 可选：使用友好的短 ID（需要维护去重表）
    public static string GenerateFriendly(string prefix)
    {
        return $"{prefix}_{DateTime.Now:yyyyMMddHHmmss}_{Random.Range(1000, 9999)}";
    }
}
```

#### ID 引用验证

```csharp
public class SceneBindingValidator
{
    public static ValidationResult Validate(string markerId, BindingType expectedType)
    {
        // 1. 检查 Marker 是否存在
        var marker = MarkerRegistry.Find(markerId);
        if (marker == null)
            return ValidationResult.Error($"未找到标记: {markerId}");
        
        // 2. 检查类型是否匹配
        if (!IsTypeCompatible(marker.Type, expectedType))
            return ValidationResult.Error($"类型不匹配: 期望 {expectedType}，实际 {marker.Type}");
        
        return ValidationResult.Success();
    }
}
```

---

### 5.2 绑定验证

#### 编辑期验证

```csharp
// 在 Blueprint 编辑器中实时验证
public class BlueprintValidator
{
    public void ValidateNode(ActionNodeData node)
    {
        foreach (var binding in node.SceneBindings)
        {
            var result = SceneBindingValidator.Validate(
                binding.MarkerId,
                binding.ExpectedType
            );
            
            if (!result.IsValid)
            {
                // 在节点上显示错误标记
                MarkNodeAsInvalid(node, result.ErrorMessage);
            }
        }
    }
}
```

#### 运行时验证

```csharp
// 在 Playbook 加载时验证
public class PlaybookLoader
{
    public void Load(PlaybookData data)
    {
        foreach (var action in data.Actions)
        {
            foreach (var bindingId in action.SceneBindingIds)
            {
                if (!MarkerRegistry.Exists(bindingId))
                {
                    Debug.LogError($"场景标记缺失: {bindingId}");
                    // 可选：显示编辑器警告，阻止运行
                }
            }
        }
    }
}
```

---

### 5.3 导出与运行时

#### 导出策略

```
方案 A：导出 Marker ID（推荐）
  Playbook.json:
  {
    "actions": [
      {
        "type": "Location.RandomInArea",
        "areaId": "spawn_hallway_01",  ← 只存 ID
        "count": 5
      }
    ]
  }
  
  优点：
    - 文件体积小
    - 场景调整无需重新导出蓝图
    - 支持热更新
  
  缺点：
    - 运行时需要查询 Marker 数据

方案 B：导出完整数据
  Playbook.json:
  {
    "actions": [
      {
        "type": "Location.RandomInArea",
        "area": {
          "polygon": [[10,0,5], [15,0,5], ...]  ← 内联完整数据
        },
        "count": 5
      }
    ]
  }
  
  优点：
    - 运行时无需查询
    - 独立运行
  
  缺点：
    - 文件体积大
    - 场景调整需要重新导出
    - 难以调试（数据冗余）
```

**推荐**：使用方案 A（导出 ID），配合运行时的 Marker 查询系统

---

#### 运行时查询

```csharp
public class MarkerQueryService
{
    private Dictionary<string, Marker> _markerCache;
    
    public Marker GetMarker(string id)
    {
        if (_markerCache.TryGetValue(id, out var marker))
            return marker;
        
        // 从场景中查询（首次查询时）
        marker = SceneMarkerRegistry.Find(id);
        if (marker != null)
            _markerCache[id] = marker;
        
        return marker;
    }
    
    public Vector3[] GetAreaPolygon(string areaId)
    {
        var marker = GetMarker(areaId);
        if (marker?.Type != MarkerType.Area)
            throw new InvalidOperationException($"Marker {areaId} is not an Area");
        
        return (Vector3[])marker.ShapeData;
    }
}
```

---

## 六、最佳实践

### 6.1 推荐的场景组织方式

#### Hierarchy 结构

```
Scene
├─ Environment（环境美术）
│  ├─ Buildings
│  ├─ Props
│  └─ Lighting
│
├─ Markers（蓝图标记）★
│  ├─ Combat（战斗相关）
│  │  ├─ spawn_hallway_01 (Area)
│  │  ├─ spawn_hallway_02 (Area)
│  │  └─ trigger_ambush (Area)
│  │
│  ├─ Patrol（巡逻路径）
│  │  ├─ patrol_guard_a (Path)
│  │  └─ patrol_guard_b (Path)
│  │
│  └─ Interaction（交互点）
│     ├─ chest_01 (Point)
│     └─ door_trigger (Point)
│
├─ GameLogic（游戏逻辑）
│  └─ BlueprintController（挂载 Playbook 的对象）
│
└─ _EditorOnly（编辑器专用，运行时隐藏）
   └─ PreviewObjects
```

**关键**：
- 所有 Marker 集中在 `Markers` 节点下，方便管理
- 按功能分类（Combat、Patrol、Interaction 等）
- 命名规范统一，便于搜索和引用

---

### 6.2 推荐的蓝图结构

#### 模块化设计

```
Blueprint 结构：

[Module 1: 初始化]
  [Flow.Start]
      ↓
  [Spawn.Preset: 守卫]
      ↓
  [Behavior.Assign: 待机]

[Module 2: 埋伏触发]
  [Trigger.EnterArea: 走廊]
      ↓
  [Location.RandomInArea: 刷怪区]
      ↓
  [Monster.Pool: 哥布林]
      ↓
  [Spawn.Execute]
      ↓
  [Behavior.Assign: 追击]

[Module 3: 战斗完成]
  [Condition.AllDead]
      ↓
  [VFX.ScreenFlash]
      ↓
  [Flow.OpenDoor]
```

**原则**：
- 按功能模块组织节点
- 使用注释节点标注模块边界
- 避免过长的连线（超过 5 个节点考虑拆分）

---

### 6.3 常见场景示例

#### 示例 1：基础埋伏战

**场景设置**：
```
Markers:
  - trigger_ambush (Area)：玩家触发区
  - spawn_ambush (Area)：怪物刷新区
```

**蓝图配置**：
```
[Trigger.EnterArea]
  └─ area: trigger_ambush
      ↓
[Location.RandomInArea]
  ├─ area: spawn_ambush
  ├─ count: 6
  └─ minDistance: 3.0
      ↓
[Monster.Pool]
  └─ monsters: [Goblin_Warrior]
      ↓
[Spawn.Execute]
      ↓
[Behavior.Assign]
  └─ behaviorType: Guard
```

---

#### 示例 2：多波次防守战

**场景设置**：
```
Markers:
  - spawn_wave1 (Area)：第一波刷怪区
  - spawn_wave2 (Area)：第二波刷怪区
  - spawn_wave3 (Area)：第三波刷怪区
```

**蓝图配置**：
```
[Flow.Start]
      ↓
[Spawn.Wave]
  ├─ area: spawn_wave1
  ├─ waveCount: 3
  ├─ monstersPerWave: 5
  └─ waveInterval: 10.0
      ↓ out（全部波次完成）
      ↓
[Spawn.Wave]
  ├─ area: spawn_wave2
  ├─ waveCount: 2
  ├─ monstersPerWave: 8
  └─ waveInterval: 15.0
      ↓
[Boss.Spawn]
  └─ bossId: "Boss_OrcChieftain"
```

---

#### 示例 3：巡逻守卫 + 警戒

**场景设置**：
```
Markers:
  - guard_spawn (Point)：守卫出生点
  - patrol_route (Path)：巡逻路径（5个点）
```

**蓝图配置**：
```
[Flow.Start]
      ↓
[Spawn.Preset]
  └─ presetPoints: guard_spawn
      ↓
[Behavior.Assign]
  ├─ behaviorType: Patrol
  ├─ patrolRoute: patrol_route
  ├─ loopPatrol: true
  └─ guardRadius: 8.0  ★ 巡逻时也会警戒
```

---

## 七、编辑器预览刷新与一致性策略（新增）

### 7.1 设计目标

围绕 `Location.RandomArea` 的 Scene 绿色预览，编辑器层遵循以下目标：

- **准确性**：Marker 几何、节点参数、绑定关系变化后，预览应及时同步。
- **性能**：避免每次变化都全量重算，优先做到 marker/node 粒度刷新。
- **稳定性**：面对 Undo/Redo、节点增删、模板实例化等复杂路径，缓存要可收敛。

### 7.2 事件驱动脏刷新主链路

当前实现从轮询式刷新切换为**事件驱动 + 脏标记 + 合并调度**：

1. 事件源（节点属性变更、层级变化、Undo 属性修改、节点增删等）只做 `MarkPreviewDirty...`。
2. 脏节点先进入集合，使用 `EditorApplication.delayCall` 合并到同一批次。
3. `FlushDirtyPreviews` 按“全量优先，单节点次之”执行。
4. 批量移除预览后统一重绘，避免多次 `SceneView.RepaintAll`。

这条链路保证了：
- 无空闲轮询开销；
- 同帧多事件可合并；
- 删除节点时预览可及时清理。

### 7.3 精准刷新优化点

为进一步减少不必要计算，当前实现包含两项关键优化：

1. **Marker→Node 反向索引**  
   - 维护 `markerId -> nodeIds` 与 `nodeId -> markerId`。  
   - 按 marker 刷新时优先索引命中，避免全图扫描。  

2. **节点签名短路（Node Signature Skip）**  
   - 对 `Location.RandomArea` 计算签名（节点参数 + 关联 Marker 几何签名）。  
   - 若签名未变化且已有缓存，直接跳过 `GenerateLocationPreview`。

### 7.4 一致性与兜底策略

为了避免“快但不准”，设计中保留了必要兜底：

- 图结构快照（节点 ID 集 + 子图计数）用于识别增删节点；
- 索引空命中时允许一次重建后重试；
- 层级变化路径下，若 marker 精准命中失败，可回退到 RandomArea 范围刷新；
- `BlueprintPreviewManager` 在全量刷新时清理 stale preview 和签名缓存。

该策略的本质是：**默认走精准路径，异常路径快速收敛，不牺牲正确性**。

---

## 八、Blueprint 定位与编辑器工具边界

### 8.1 Blueprint 的核心定位

**Blueprint = 运行时规则图的编辑时描述。**

Blueprint 描述的是"运行时会发生什么"——行动列表、触发条件、数据流关系。它**不是**编辑时的操作工具。

```
┌─────────────────────────┐          ┌─────────────────────────┐
│   Unity Editor 项目      │          │   运行时项目             │
│                         │   导出    │   (帧同步引擎)           │
│  · Blueprint 编辑器     │ ═══════► │                         │
│  · 编辑器工具（辅助）    │  Playbook │  · 消费 Playbook 数据   │
│                         │  (JSON)   │  · 执行行动逻辑          │
└─────────────────────────┘          └─────────────────────────┘
```

关键约束（来自场景蓝图系统总体设计 §4.1）：
- 蓝图的可视化图（节点/连线）是**纯编辑器概念**，运行时从来看不到
- 运行时看到的是一份**纯数据**——行动列表 + 条件关系 + 属性参数
- 导出步骤是桥梁，负责把"图"编译成"数据"

### 8.2 编辑器工具 vs Blueprint 节点的判定规则

当设计一个新功能时，用以下问题判断它应该是"编辑器工具"还是"Blueprint 节点"：

| 判定问题 | 编辑器工具 | Blueprint 节点 |
|---------|-----------|---------------|
| 运行时需要执行吗？ | ❌ 不需要 | ✅ 需要 |
| 产物是什么？ | Marker（持久化到场景） | Playbook 中的 Action 数据 |
| 策划需要反复迭代吗？ | ✅ 反复调整直到满意 | ❌ 配好参数即可 |
| 最终结果是确定的吗？ | ✅ 固定下来 | ❌ 可能每次运行不同 |

**典型示例**：

| 功能 | 归属 | 理由 |
|------|------|------|
| 在区域内随机撒点 → 微调 → 确认 | **编辑器工具** | 策划迭代过程，产物是 PointMarker |
| 运行时在区域内随机生成位置 | **Blueprint 节点** | 每次玩都不同，运行时执行 |
| 选择阵型（方阵/圆阵）生成点位 | **编辑器工具** | 辅助摆放，产物是 PointMarker |
| 配置怪物模板和权重 | **Blueprint 节点** | 运行时消费的数据 |
| 定义触发条件和时序 | **Blueprint 节点** | 运行时逻辑 |

### 8.3 预设刷怪 vs 运行时随机刷怪——两条路径

根据 Blueprint 定位，刷怪存在两条完全不同的路径：

#### 路径 A：预设刷怪（策划精确摆放，每次玩都一样）

```
═══ 编辑器层 ═══

SceneView: AreaMarker（空白区域）
    ↓
位置生成工具（编辑器工具，非 Blueprint 节点）
    ├─ 随机生成 / 阵型生成 / 手动放置
    ├─ 预览绿色点 → 不满意 → 重新生成
    ├─ 微调单个点的位置和朝向
    └─ 确认 → 固化为 N 个 PointMarker（持久化到场景）

═══ Blueprint 层 ═══

[Trigger] ──→ [Spawn.Preset] ──→ [Behavior.Assign]
                    │
                    ├─ presetPoints: 绑定那些 PointMarker
                    └─ monsterTemplate: 怪物模板

═══ 导出 ═══

Playbook.json: 包含固定坐标 + 怪物模板 + 行为配置
运行时：在这些固定位置放置怪物（所见即所得）
```

#### 路径 B：运行时随机刷怪（每次玩位置不同）

```
═══ 编辑器层 ═══

SceneView: AreaMarker（定义随机范围）

═══ Blueprint 层 ═══

[Monster.Pool] ──monsters──→ [Spawn.Execute]
                                    ↑
[Location.RandomArea] ──positions──→┘
  绑定: AreaMarker
  count / minSpacing 等参数

═══ 导出 ═══

Playbook.json: 包含区域参数 + 随机算法配置
运行时：每次执行时在区域内随机生成位置
```

#### 两条路径对比

| 维度 | 路径 A（预设刷怪） | 路径 B（运行时随机） |
|------|-------------------|---------------------|
| 位置确定时机 | 编辑时（策划确认后固定） | 运行时（每次不同） |
| 空间数据载体 | PointMarker（场景持久化） | AreaMarker 参数（运行时计算） |
| 策划控制度 | 完全控制每个点的位置/朝向 | 只控制区域和参数 |
| 预览一致性 | 预览 = 最终结果 | 预览仅供参考 |
| 适用场景 | Boss 出场、埋伏怪、守卫 NPC | Roguelike、随机遭遇 |
| Blueprint 节点 | `Spawn.Preset` | `Location.RandomArea` + `Spawn.Execute` |

### 8.4 位置生成工具的策划工作流

位置生成工具是**编辑器辅助工具**，帮助策划快速在 AreaMarker 内铺设 PointMarker。

> **设计决策（v1.2）**：取消"临时预览 → 固化"两步机制，改为直接生成真实 PointMarker。
> 原因：临时预览的自定义 Handle 与 Gizmo 管线的 Interactive Handle 冲突，且增加了不必要的复杂度。
> 直接生成后，策划用 Unity 原生 W/E/R 工具微调，零冲突、零额外 UI。

```
策划工作流：

第1步：选择区域
  SceneView 中选中一个 AreaMarker

第2步：选择生成策略 + 数量
  ┌─────────────────────────┐
  │ 位置生成工具              │
  │                         │
  │ 生成策略: [随机 ▼]      │  ← 随机 / 圆形阵型
  │ 数量: [5]               │
  │ 最小间距: [2.0]         │
  │                         │
  │ ☑ 自动添加标注: [Spawn] │  ← 可选，自动挂 SpawnAnnotation
  │                         │
  │ [随机生成]              │  ← 每次点击：换种子 + 清除旧点 + 生成新点
  │                         │
  │ 已有 5 个子 PointMarker │
  │ [清除全部子点位]         │
  └─────────────────────────┘

第3步：微调位置和朝向
  → Hierarchy 中选中某个子 PointMarker
  → W 移动 / E 旋转（Unity 原生操作）
  → 不满意？回到 AreaMarker → [随机生成]（重新来过）

第4步：标注属性（可选）
  → 选中 PointMarker → Inspector 中编辑 SpawnAnnotation
  → 设置 MonsterId / Level / Behavior 等
  → SceneView 中该点位颜色和标签随标注变化

第5步：Blueprint 中编排
  → 创建 Spawn.Preset 节点，绑定 AreaMarker
  → 导出时自动收集子 PointMarker + 标注数据
```

**关键设计原则**：
- 生成工具的产物是 **PointMarker**（场景空间数据），不是 Blueprint 数据
- **直接生成真实 PointMarker**，无临时预览，所见即所得
- 微调使用 Unity 原生 W/E/R 工具，零 Handle 冲突
- 标注属性（怪物 ID、行为等）通过 **MarkerAnnotation 组件**附加，属于 SceneView 层
- 整个过程不涉及 Blueprint，纯粹是 SceneView 层的操作
- 详见 [标记标注系统设计](标记标注系统设计.md)

### 8.5 反模式：混淆编辑时与运行时

#### ❌ 反模式：把编辑时迭代工具做成 Blueprint 节点

```
错误设计：
  [Location.RandomArea] 作为 Blueprint 节点
  策划在编辑器中预览 10 个绿色点
  但 Spawn.Execute 运行时只用了 4 个
  → 预览和最终结果不一致，预览变成"骗人的"
```

**为什么错**：
- 策划看到的预览 ≠ 运行时结果，破坏信任
- 策划无法微调单个点的位置
- "随机 → 满意 → 固定"的迭代流程无法实现

#### ✅ 正确做法

```
编辑时：位置生成工具（编辑器工具）
  → 策划反复随机/微调 → 确认 → 固化为 PointMarker
  → 预览 = 最终结果

运行时：Blueprint 节点引用这些 PointMarker
  → Spawn.Preset 在固定位置放置怪物
  → 所见即所得
```

**判定口诀**：
> 如果策划需要"看到 → 调整 → 再看 → 满意 → 固定"，那它是**编辑器工具**。
> 如果运行时需要"读取参数 → 执行逻辑 → 产生结果"，那它是**Blueprint 节点**。

---

## 九、PropertyType 扩展：StructList 结构化列表

### 9.1 背景

`Spawn.Wave` 的波次配置需要存储结构化列表数据（每波的刷怪数量、间隔、怪物筛选等），但原有 `PropertyType` 只支持标量类型，导致只能用 `Prop.String` 存 JSON 字符串，策划无法在 Inspector 中直观编辑。

### 9.2 设计方案

在不修改 NodeGraph 层的前提下，在 Core 层和 Editor 层分别扩展：

| 层 | 改动 | Unity 依赖 |
|----|------|-----------|
| Core 层 | `PropertyType` 新增 `StructList`；`PropertyDefinition` 新增 `StructFields`、`SummaryFormat` | 无 |
| Core 层 | `Prop` 工厂新增 `Prop.StructList()` 方法 | 无 |
| Editor 层 | `ActionNodeInspectorDrawer` 新增 `DrawStructListField()`（可排序、可增删列表） | 有 |
| Editor 层 | `ActionContentRenderer` 新增 StructList 摘要显示 | 无 |
| Editor 层 | `BlueprintExporter` 新增 StructList 序列化（`ValueType = "json"`） | 有 |
| Editor 层 | `StructListJsonHelper` 新增文件，负责 JSON ↔ `List<Dictionary<string, object>>` 转换 | 无 |

### 9.3 存储策略

StructList 在 `PropertyBag` 中以 JSON 字符串形式存储，零侵入：

```csharp
// PropertyBag 不需要改动，StructList 以 string 存储
string json = bag.Get<string>("waves") ?? "[]";
bag.Set("waves", "[{\"count\":5,...}]");
```

编辑器层在绘制时负责 JSON ↔ `List<Dictionary<string, object>>` 的转换。

### 9.4 使用示例

```csharp
// SpawnWaveDef 中的波次配置
Prop.StructList("waves", "波次配置",
    fields: new[]
    {
        Prop.Int("count", "刷怪数量", defaultValue: 5, min: 1, max: 50),
        Prop.Int("intervalTicks", "间隔(Tick)", defaultValue: 60, min: 0, max: 600),
        Prop.Enum("monsterFilter", "怪物筛选",
            new[] { "All", "Normal", "Elite", "Boss", "Minion", "Special" },
            defaultValue: "All"),
    },
    summaryFormat: "波次: {count} 波",
    order: 1)
```

### 9.5 显示效果

节点画布中只显示摘要文本（如"波次: 3 波"），详细编辑在侧边 Inspector 面板中进行，支持列表元素的添加、删除、上移、下移操作。

导出格式：`ValueType = "json"`，值为 JSON 数组字符串，运行时直接解析。

---

## 十、Trigger 节点统一为条件等待节点

### 10.1 背景

原 `Trigger.EnterArea` 是"自启动事件源"——没有 `in` 端口，蓝图启动后自动监听。这与其他 ActionNode 的激活规则不一致，破坏了 `Flow.Start` 作为唯一起点的语义。

### 10.2 设计决策

所有 ActionNode 遵循统一的激活规则：

```
Idle → (收到 in 端口事件) → Running → (条件满足) → Completed → (触发 out 端口)
```

`Flow.Start` 是唯一的流程起点，Trigger 节点只是"条件等待节点"。

### 10.3 改动对比

| 项目 | 旧设计 | 新设计 |
|------|--------|--------|
| 端口 | 只有 `onEnter` 输出 | `in`（输入）+ `out`（输出） |
| Duration | `Instant` | `Duration`（持续型，Running 阶段检查条件） |
| `maxTriggerTimes` | 存在 | 移除（Trigger 就是"等待一次条件满足"） |
| 激活方式 | 自启动 | 通过 `in` 端口被上游激活 |
| Running 行为 | 无 | 持续检查"玩家是否进入区域" |

### 10.4 典型蓝图连接

```
旧设计：
  [Trigger.EnterArea] ──onEnter──→ [Spawn.Wave]
  （Trigger 是独立起点，Flow.Start 的唯一性被破坏）

新设计：
  [Flow.Start] ──→ [Trigger.EnterArea] ──out──→ [Spawn.Wave] ──out──→ [Flow.End]
  （统一流程，Flow.Start 是唯一起点）
```

### 10.5 运行时系统

`TriggerEnterAreaSystem`（Order=105）处理 `Trigger.EnterArea` 节点：

- 扫描所有 `TypeId == "Trigger.EnterArea"` 且 `Phase == Running` 的节点
- 检查玩家是否在触发区域内（通过 `IPlayerPositionProvider` 接口）
- 条件满足 → `Phase = Completed`，由 `TransitionSystem` 路由至下游
- 未注入 `IPlayerPositionProvider` 时默认条件满足（测试模式）

### 10.6 System 执行顺序

| System | Order | 职责 |
|--------|-------|------|
| FlowSystem | 10 | 处理 Flow.Start/End/Delay/Join |
| SpawnPresetSystem | 100 | 处理 Spawn.Preset |
| TriggerEnterAreaSystem | 105 | 处理 Trigger.EnterArea |
| SpawnWaveSystem | 110 | 处理 Spawn.Wave |
| TransitionSystem | 900 | 传播完成事件，激活下游节点 |

---

## 附录

### 术语表

| 术语 | 定义 |
|-----|------|
| **SceneView** | Unity 的场景编辑视图，用于可视化编辑 3D 场景 |
| **Blueprint** | 逻辑蓝图，通过节点图编排游戏逻辑 |
| **Marker** | 场景中的空间标记，描述位置、区域、路径等 |
| **SceneBinding** | Blueprint 节点对 Marker 的引用关系 |
| **Playbook** | 导出的蓝图运行时数据，包含节点和连线信息 |
| **单向数据流** | SceneView → Blueprint 的单向依赖，避免循环依赖 |
| **场景中心化** | SceneView 作为空间数据的唯一权威来源 |
| **位置生成工具** | 编辑器辅助工具，帮助策划在 AreaMarker 内快速铺设 PointMarker（随机/阵型/手动） |
| **固化** | 将编辑器中的临时预览点转化为持久化的 PointMarker 的过程 |
| **预设刷怪** | 策划精确摆放位置，运行时在固定坐标放置怪物（路径 A） |
| **运行时随机刷怪** | 运行时在区域内随机生成位置，每次玩都不同（路径 B） |
| **StructList** | 结构化列表属性类型，每个元素包含多个子字段，Inspector 中显示为可排序列表 |
| **条件等待节点** | 有 in 端口，被激活后进入 Running，持续检查条件，满足后 Completed（如 Trigger.EnterArea） |
| **TriggerEnterAreaSystem** | 运行时系统，处理 Trigger.EnterArea 节点的区域检测逻辑（Order=105） |

---

### 设计决策记录

| 决策 | 理由 | 影响 |
|-----|------|------|
| 采用单向数据流 | 避免双向同步的复杂度 | 简化技术实现，降低 Bug 风险 |
| Marker 只存空间数据 | 职责分离，保持 Marker 的纯粹性 | 逻辑都在 Blueprint，便于整体把控 |
| 不支持 Blueprint 创建 Marker | 维护"SceneView 是唯一来源"的原则 | 策划必须先在场景中标注 |
| 使用 Marker ID 引用 | 解耦空间数据和逻辑数据 | 场景调整无需修改蓝图 |
| 提供“视觉投影”而非双向同步 | 在不增加复杂度的前提下提升 UX | 策划能理解蓝图的空间含义 |
| Blueprint = 运行时规则图的编辑时描述 | 明确职责边界，避免编辑时工具与运行时逻辑混淆 | 编辑器工具产出 Marker，Blueprint 节点产出 Playbook 数据 |
| 预设刷怪与运行时随机分离 | 两种需求本质不同，不应共用同一条路径 | 路径 A（编辑器工具 + Spawn.Preset）与路径 B（Location.RandomArea + Spawn.Execute）并存 |
| StructList 以 JSON 字符串存储在 PropertyBag 中 | 零侵入，不需要改动 PropertyBag 和序列化器 | 编辑器层负责 JSON ↔ 列表转换，导出格式 ValueType="json" |
| StructList 节点画布只显示摘要 | 画布空间有限，详细编辑在侧边 Inspector | 使用 SummaryFormat 模板格式化（如"波次: {count} 波"） |
| Trigger 节点统一为条件等待节点 | 统一 ActionNode 激活语义，Flow.Start 是唯一起点 | Trigger.EnterArea 新增 in 端口，移除 maxTriggerTimes，Duration 改为 Duration |
| 移除 Trigger.EnterArea 的 maxTriggerTimes | Trigger 就是"等待一次条件满足"，多次触发通过蓝图循环实现 | 简化节点语义和状态管理 |

---

### 未来扩展方向

1. **模板系统**：预设常见战斗场景的模板（埋伏战、防守战、Boss 战等）
2. **智能推荐**：根据场景布局自动推荐蓝图结构
3. **可视化调试**：运行时在 SceneView 中可视化蓝图执行流程
4. **版本控制增强**：差异对比，冲突解决
5. **多人协作**：锁定机制，实时同步

---

### 相关文档

- [节点激活语义与汇聚设计.md](./节点激活语义与汇聚设计.md) - 节点激活规则和 Flow.Join 设计
- [数据流节点系统设计.md](./数据流节点系统设计.md) - 节点系统的技术实现细节
- [Playbook概念与示例.md](./Playbook概念与示例.md) - 运行时系统说明
- [文档导航.md](./文档导航.md) - 完整文档索引

---

**版本历史**：

- **v1.3** (2026-02-19)
  - 新增"PropertyType 扩展：StructList 结构化列表"章节（第九章）
  - 新增"Trigger 节点统一为条件等待节点"章节（第十章）
  - StructList：扩展 PropertyType 支持结构化列表，Inspector 可视化编辑，导出为 JSON
  - Trigger 统一：Trigger.EnterArea 新增 in 端口，改为条件等待节点，统一 ActionNode 激活语义
  - 新增 TriggerEnterAreaSystem 运行时系统（Order=105）
  - 记录 System 执行顺序表（FlowSystem:10 → 业务:100~199 → TransitionSystem:900）
  - 删除独立设计文档 `Blueprint节点与属性系统优化设计.md`，内容合并至此

- **v1.2** (2026-02-17)
  - 新增“Blueprint 定位与编辑器工具边界”章节（第八章）
  - 明确 Blueprint = 运行时规则图的编辑时描述，不是编辑时操作工具
  - 定义编辑器工具 vs Blueprint 节点的判定规则
  - 区分预设刷怪（路径 A）与运行时随机刷怪（路径 B）两条独立路径
  - 描述位置生成工具的策划工作流（随机/阵型 → 微调 → 固化）
  - 新增反模式：混淆编辑时与运行时

- **v1.1** (2026-02-17)
  - 新增“编辑器预览刷新与一致性策略”章节
  - 明确事件驱动脏刷新、marker/node 粒度优化、节点签名短路与兖底策略
  - 补充 `doc_status` / `last_reviewed` 文档治理元信息

- **v1.0** (2026-02-16)
  - 初始版本
  - 确立场景中心化、职责分离、单向数据流三大核心原则
  - 定义 SceneView 与 Blueprint 的职责边界
  - 描述推荐的工作流模式和最佳实践
  - 警示常见反模式
