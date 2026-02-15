# AI Director 设计

> 版本：v1.0  
> 日期：2026-02-12  
> 状态：设计阶段（Phase 6-7，当前不实现，已废弃）  
> 父文档：[场景蓝图系统总体设计](../场景蓝图系统总体设计.md)
> doc_status: deprecated  
> last_reviewed: 2026-02-15

---

## 1. 概述

AI Director（场景导演 AI）是运行时的智能决策系统，负责在蓝图定义的"可能性空间"内做出最优选择，让自动战斗从"机械回放"变成"精心编排的战斗表演"。

灵感来源：**Left 4 Dead 的 AI Director**。

```
场景蓝图   = 剧本（预先编排好的情节结构）
AI Director = 导演（根据现场状况调整拍摄）
运行时引擎 = 演员（执行具体动作）
```

### 1.1 核心原则

**蓝图定义可能性空间，AI Director 在空间内做最优选择。**

```
蓝图说：                            Director 决定：
"这里刷一波怪"                      "刷6只，从左侧，带1个精英"
"然后Boss出场"                      "现在出场，配慢动作特写"
"可以刷增援"                        "玩家太强了，刷2波增援而不是1波"
```

### 1.2 为什么适合自动战斗

自动战斗 = AI Director 有 **100% 控制权**，不会被玩家操作干扰：
- 摄像机可以完美编排（不会被玩家乱跑打断）
- 场景感知可以更激进地调整（不担心玩家觉得"不公平"）
- 导出的数据可以在帧同步引擎中精确执行
- 蓝图 + Director = 策划可以创造"类过场动画"级别的战斗体验

---

## 2. 决策模型：Utility AI

### 2.1 为什么选 Utility AI

| AI 范式 | 适用场景 | 是否适合 Director |
|---------|---------|-----------------|
| 行为树 | 个体 NPC 行为 | ❌ 适合个体，不适合全局导演 |
| 状态机 | 固定状态转换 | ❌ 太僵硬，难表达微妙权衡 |
| ML/RL | 需要大量训练数据 | ❌ 帧同步要求确定性，不适合 |
| **Utility AI** | 多因素权衡决策 | ✅ 确定性、可调参、直觉友好 |

Utility AI 核心思想：
1. 每个决策有多个选项
2. 每个选项根据当前状态计算一个"分数"（Utility Score）
3. 选分数最高的

### 2.2 帧同步兼容性

Utility AI 天然满足确定性：
- 输入是确定的（CombatPerception 是确定性帧数据）
- 评估函数是纯数学（没有随机数，或使用确定性伪随机）
- 输出是确定的（相同输入 = 相同决策）

```
确定性保证：
  Director.Evaluate(perception, seed) → 始终返回相同结果
  所有浮点运算使用定点数（FP）
  伪随机用确定性种子（帧号 + 行动ID）
```

---

## 3. 感知层（CombatPerception）

Director 的输入——每 N 帧更新一次的战斗状态快照。

### 3.1 数据结构

```csharp
public struct CombatPerception
{
    // ─── 强度感知 ───
    public float Intensity;            // 当前战斗强度 0~1
    public float IntensityTrend;       // 强度趋势（>0 上升，<0 下降）
    public float TimeSinceLastPeak;    // 距上次高潮多久（秒）

    // ─── 玩家侧 ───
    public float TeamHPRatio;          // 队伍总血量比例 0~1
    public float TeamDPS;              // 队伍当前输出（每秒伤害）
    public float AvgKillTime;          // 平均单怪击杀耗时（秒）

    // ─── 敌方侧 ───
    public int AliveEnemyCount;        // 存活敌人数
    public float TotalThreat;          // 总威胁值
    public float SpatialSpread;        // 敌人空间分散度 0~1

    // ─── 节奏 ───
    public float CombatDuration;       // 当前遭遇持续时间（秒）
    public int WavesCompleted;         // 已完成波数
    public float TimeSinceLastSpawn;   // 距上次刷怪多久（秒）
}
```

### 3.2 感知维度详解

| 感知维度 | 数据来源 | Director 如何利用 |
|---------|---------|-----------------|
| **战斗强度** | DPS、击杀率、存活敌人数 综合 | 追踪情绪曲线，决定加压或减压 |
| **空间感知** | 玩家阵型位置和朝向 | 决定增援从哪个方向刷出（包抄/正面/背后） |
| **威胁感知** | 存活敌人分布 | 新刷怪避开已有怪物位置，避免堆叠 |
| **节奏感知** | 战斗时长和击杀频率 | 战斗太久→加速推进；太快→插入喘息 |
| **观赏性感知** | 镜头构图、视觉效果 | 确保怪物不刷在镜头外，Boss 登场在画面中心 |

---

## 4. 情绪曲线

这是 L4D AI Director 最核心的设计——Director 追踪一条理想的情绪曲线。

### 4.1 理想曲线

```
情绪强度
  ↑
  │        ╱╲         ╱╲╱╲
  │      ╱    ╲      ╱      ╲        ╱╲
  │    ╱        ╲  ╱          ╲    ╱    ╲
  │  ╱            ╲             ╲╱        ╲
  │╱                                        ╲
  └────────────────────────────────────────────→ 时间
   开场   建压   高潮  喘息  建压  高潮Boss  结尾

理想曲线 = 波浪形
  高潮不能太久（疲劳）
  低谷不能太久（无聊）
  Boss 前要有"暴风雨前的平静"
  Boss 战本身是最大高潮
```

### 4.2 曲线驱动决策

```
Director 核心循环（每帧）：
  1. 更新 CombatPerception
  2. 计算当前 intensity（实际强度）
  3. 查询理想曲线，得到 desiredIntensity（期望强度）
  4. gap = desiredIntensity - currentIntensity
  5. gap > 0 → 倾向加压（多刷、快推进、加精英）
     gap < 0 → 倾向减压（少刷、延迟推进、给补给）
  6. 将倾向转化为 Utility Score 的权重调整
```

### 4.3 曲线数据格式

```csharp
public class IntensityCurve
{
    /// <summary>时间点 → 期望强度（0~1）的关键帧序列</summary>
    public List<CurveKeyframe> Keyframes;
}

public struct CurveKeyframe
{
    public float Time;       // 归一化时间 0~1（0=遭遇开始，1=遭遇结束）
    public float Intensity;  // 期望强度 0~1
    public float Tolerance;  // 容差范围（实际强度偏差在此范围内不调整）
}
```

策划可以为不同类型的遭遇配置不同的曲线模板（如"渐进式"、"闪电战"、"Boss 战"等）。

---

## 5. 决策层（Utility 评估）

### 5.1 评估流程

当 BlueprintRunner 执行到一个 Action 时，向 Director 查询参数建议：

```
SpawnAction 声明了可调参数范围：
  perWave:   { base: 5, min: 3, max: 10, influence: 0.8 }
  interval:  { base: 2, min: 1, max: 5,  influence: 0.6 }
  spawnSide: { options: [Left, Right, Behind, Surround], influence: 1.0 }

Director 基于感知评估每个选项的 Utility Score：

EvaluateSpawnCount(perception):
  if intensity > 0.8  → 倾向少刷（给喘息）    score_low = 0.8
  if intensity < 0.3  → 倾向多刷（加压）       score_high = 0.9
  if teamHPRatio < 0.3 → 倾向少刷（别逼死）    score_low += 0.5
  → 选分数最高的 count 值

EvaluateSpawnSide(perception):
  if spatialSpread < 0.3 → 倾向包抄（Surround）  制造紧张感
  if spatialSpread > 0.7 → 倾向集中（Left/Right） 给玩家聚焦目标
  → 选分数最高的方向
```

### 5.2 directorInfluence 参数

这是策划和 AI 之间的**权限边界**控制：

```csharp
// 在 PropertyDefinition 中声明
public bool DirectorControllable;   // 是否允许 Director 调整
public float DirectorInfluence;     // 0~1
```

| directorInfluence | 含义 |
|-------------------|------|
| `0.0` | 完全由策划控制，Director 不可修改 |
| `0.3` | 策划基本控制，Director 微调 |
| `0.6` | Director 有较大调整空间 |
| `0.8` | Director 主导，策划提供基准 |
| `1.0` | 完全交给 Director 决定 |

实际值的计算：

```
finalValue = base + (directorSuggested - base) * directorInfluence
finalValue = clamp(finalValue, min, max)
```

示例：

```
Boss 出场时刻的摄像机       → influence: 0.0（策划完全控制）
普通小怪波次的刷怪数量      → influence: 0.8（AI 大幅调整）
精英怪的刷怪方向            → influence: 1.0（完全交给AI）
Boss 增援的时机             → influence: 0.3（策划基本控制，AI微调）
```

---

## 6. 蓝图属性的范围值

### 6.1 导出格式扩展

为支持 Director，属性值从固定值变成范围描述：

```json
// 固定值（directorInfluence = 0 或 Phase 1）
{ "Key": "perWave", "ValueType": "int", "Value": "5" }

// 范围值（Phase 2+，Director 可调整）
{
  "Key": "perWave",
  "ValueType": "directedInt",
  "Value": "{ \"base\": 5, \"min\": 3, \"max\": 10, \"influence\": 0.8 }"
}
```

运行时读到 `directedInt` / `directedFloat` 类型就知道要查询 Director。`influence` 为 0 则直接用 `base` 值。

### 6.2 向前兼容

Phase 1 的蓝图数据（固定值）在 Phase 2+ 中完全兼容：
- 运行时遇到普通 `int`/`float` 类型 → 当作固定值，不查 Director
- 运行时遇到 `directedInt`/`directedFloat` → 查 Director
- 策划可以逐步开放参数给 AI，不需要一次性改完

---

## 7. 导出数据

Director 配置数据独立于蓝图数据导出：

### 7.1 DirectorProfile

```csharp
public class DirectorProfile
{
    public string ProfileId;                // 如 "progressive", "blitz", "boss_fight"
    public string ProfileName;              // "渐进式", "闪电战", "Boss战"

    // 情绪曲线
    public IntensityCurve IntensityCurve;

    // Utility 评估权重
    public float IntensityWeight;           // 强度追踪的权重
    public float SurvivalWeight;            // 生存保护的权重（低血时减压）
    public float PacingWeight;              // 节奏控制的权重（避免太长/太短）
    public float SpectacleWeight;           // 观赏性的权重（镜头构图、特效时机）
}
```

### 7.2 蓝图与 Director 的关联

```json
// 在 SceneBlueprintData 中引用
{
  "BlueprintId": "boss_room_01",
  "DirectorProfileId": "boss_fight",
  ...
}
```

每张蓝图可以指定使用哪个 Director 配置。不指定则使用默认配置（或不启用 Director）。

---

## 8. 整体系统交互

```
┌──────────────────────────────────────────────────────────────┐
│  编辑时：策划的工作                                           │
│                                                               │
│  场景蓝图编辑器                                               │
│  ├─ 编排行动流程（节点+连线）        ← 结构                   │
│  ├─ 设置参数范围（min/max/base）     ← 可能性空间             │
│  └─ 设置 directorInfluence          ← AI 权限                │
│                                                               │
│  Director 配置编辑器（可选，后续实现）                          │
│  ├─ 理想情绪曲线模板                                          │
│  ├─ Utility 评估函数的权重调参                                 │
│  └─ 感知维度的权重配置                                        │
├──────────────────────────────────────────────────────────────┤
│  导出数据                                                     │
│  ├─ SceneBlueprintData   ← 行动 + 过渡 + 参数范围            │
│  └─ DirectorProfile      ← 情绪曲线 + Utility 权重           │
├──────────────────────────────────────────────────────────────┤
│  运行时                                                       │
│  ├─ BlueprintRunner      ← 按图执行行动                      │
│  ├─ SceneDirector (AI)   ← 感知战况、评估Utility、给出建议    │
│  └─ ActionHandlers       ← 接受 Runner 指令 + Director 建议  │
└──────────────────────────────────────────────────────────────┘
```

---

## 9. 实施阶段

| 阶段 | 内容 | 蓝图行为 | 依赖 |
|------|------|---------|------|
| **Phase 1**（当前） | 无 Director | 所有参数用 `base` 固定值 | — |
| **Phase 6** | 基础 Director | 支持 `min/max` 范围，Director 根据简单规则选值 | Phase 3（数据导出） |
| **Phase 7** | 完整 Utility AI | 情绪曲线、多维感知、精细评估 | Phase 6 |

因为 `directorInfluence: 0.0` 等于"不用 AI"，Phase 1 的蓝图数据在 Phase 6/7 中**完全向前兼容**。策划可以先按固定值设计，后续逐步开放给 AI。

---

## 10. 运行时实现要点（备忘）

以下内容由运行时团队实现，编辑器侧不涉及：

### 10.1 SceneDirector 接口

```csharp
public interface ISceneDirector
{
    /// <summary>更新感知数据</summary>
    void UpdatePerception(CombatPerception perception);

    /// <summary>查询参数建议</summary>
    T Evaluate<T>(string actionId, string propertyKey, T baseValue, T min, T max, float influence);

    /// <summary>查询是否应该推进到下一阶段</summary>
    bool ShouldAdvance(string actionId);
}
```

### 10.2 无 Director 时的降级

```csharp
// 如果运行时没有注册 Director，所有查询返回 base 值
public class NullDirector : ISceneDirector
{
    public void UpdatePerception(CombatPerception p) { }
    public T Evaluate<T>(string actionId, string key, T baseValue, T min, T max, float influence) 
        => baseValue;
    public bool ShouldAdvance(string actionId) => false;
}
```

---

## 11. 相关文档

- [场景蓝图系统总体设计](../场景蓝图系统总体设计.md)
- [Action与属性系统设计](../Action与属性系统设计.md)
- [数据导出与运行时契约](../数据导出与运行时契约.md)
