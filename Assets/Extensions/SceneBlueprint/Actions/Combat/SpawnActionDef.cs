#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Combat
{
    /// <summary>
    /// 节奏刷怪行动定义——持续型，支持多波次刷怪。
    /// <para>
    /// 这是场景蓝图中最常用的战斗行动。支持三种节奏类型：
    /// - <b>Instant</b>：一次性刷出所有怪物
    /// - <b>Interval</b>：按固定间隔逐波刷出
    /// - <b>Burst</b>：快速连续刷出多波
    /// </para>
    /// <para>
    /// 属性之间有条件可见性关系：
    /// - interval 只在 tempoType == Interval 时显示
    /// - totalWaves 只在 tempoType != Instant 时显示
    /// </para>
    /// <para>节点拓扑：
    ///                              ┌─ onWaveComplete → [每波完成后的处理]
    /// [前置] ─in→ [Spawn] ─out→
    ///                              └─ onAllComplete  → [全部完成后的处理]
    /// </para>
    /// </summary>
    [ActionType("Combat.Spawn")]
    public class SpawnActionDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Combat.Spawn",
            DisplayName = "刷怪",
            Category = "Combat",
            Description = "在指定区域按节奏刷出多波怪物",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色——表示“战斗/刷怪”
            Duration = ActionDuration.Duration, // 持续型——多波刷怪需要时间

            // ─── 端口声明 ───
            Ports = new[]
            {
                Port.FlowIn("in", "输入"),              // 主流程输入
                Port.FlowOut("out", "输出"),             // 主流程输出（全部完成后）
                Port.EventOut("onWaveComplete", "波次完成"), // 每波完成时触发（可连多条线）
                Port.EventOut("onAllComplete", "全部完成")   // 所有波次完成时触发
            },

            // ─── 属性声明 ───
            Properties = new[]
            {
                // 怪物模板——引用怪物配置资产
                Prop.AssetRef("template", "怪物模板", order: 0),

                // 节奏类型——决定刷怪的时间节奏
                Prop.Enum("tempoType", "节奏类型",
                    new[] { "Instant", "Interval", "Burst" },
                    defaultValue: "Interval", order: 1),

                // 刷怪间隔——只在 Interval 模式下显示
                Prop.Float("interval", "刷怪间隔(秒)", defaultValue: 2f, min: 0.1f, max: 30f,
                    visibleWhen: "tempoType == Interval", category: "节奏", order: 2),

                // 总波数——只在非 Instant 模式下显示（Instant 只有一波）
                Prop.Int("totalWaves", "总波数", defaultValue: 3, min: 1, max: 50,
                    visibleWhen: "tempoType != Instant", category: "节奏", order: 3),

                // 每波数量——每波刷出多少只怪物
                Prop.Int("monstersPerWave", "每波数量", defaultValue: 5, min: 1, max: 20,
                    order: 4),

                // 最大存活数——场上同时存活的怪物上限，超出时暂停刷怪
                Prop.Int("maxAlive", "最大存活数", defaultValue: 10, min: 1, max: 50,
                    category: "约束", order: 5),

                // 刷怪区域——场景中的多边形区域，怪物随机刷新在此范围内
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 6)
            },

            // ─── 场景标记需求 ───
            SceneRequirements = new[]
            {
                // 刷怪区域——必需，一个区域标记（创建参数由 MarkerPresetSO 控制）
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    presetId: "Combat.SpawnArea", required: true),

                // 刷怪点——可选，允许多个点位标记
                new MarkerRequirement("spawnPoints", MarkerTypeIds.Point,
                    presetId: "Combat.SpawnPoint", allowMultiple: true, minCount: 1),
            }
        };
    }
}
