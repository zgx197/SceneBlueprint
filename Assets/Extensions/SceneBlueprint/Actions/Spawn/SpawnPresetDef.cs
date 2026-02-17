#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// 放置预设怪行动定义——瞬时型，在场景预设点位一次性放置怪物。
    /// <para>
    /// 与波次刷怪不同：
    /// - <b>Spawn.Wave</b>：持续型，按节奏多波刷怪，区域内随机位置
    /// - <b>Spawn.Preset</b>：瞬时型，一次性在策划预设的精确点位放置怪物
    /// </para>
    /// <para>
    /// 双绑定槽设计：
    /// - <b>spawnArea</b>（可选）：绑定 AreaMarker，导出时自动收集其子 PointMarker
    /// - <b>spawnPoints</b>（可选）：直接绑定独立的 PointMarker（不在任何 AreaMarker 下）
    /// - 至少一个非空；两者都有时合并所有点位
    /// - 每个 PointMarker 上的 SpawnAnnotation 数据会被自动收集到导出数据中
    /// </para>
    /// <para>典型用途：Boss 出场、埋伏怪、NPC 守卫等需要精确位置的场景。</para>
    /// <para>节点拓扑：[前置] ─in→ [Spawn.Preset] ─out→ [后续]</para>
    /// </summary>
    [ActionType("Spawn.Preset")]
    public class PlacePresetActionDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Spawn.Preset",
            DisplayName = "放置预设怪",
            Category = "Spawn",
            Description = "在场景预设点位瞬时放置怪物（支持区域批量 + 独立点位）",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色 - 与 Spawn.Wave 统一
            Duration = ActionDuration.Instant, // 瞬时型——一次性放置
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },
            Properties = new[]
            {
                // 怪物模板——全局默认配置，PointMarker 无 SpawnAnnotation 时回退使用
                Prop.AssetRef("template", "怪物模板（默认）", order: 0),

                // 刷怪区域——绑定 AreaMarker，导出时自动收集其子 PointMarker + Annotation
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 1),

                // 独立点位——直接绑定不在 AreaMarker 下的 PointMarker
                Prop.SceneBinding("spawnPoints", "独立点位", BindingType.Transform, order: 2)
            },

            // ─── 场景标记需求 ───
            SceneRequirements = new[]
            {
                // 刷怪区域——可选，绑定后自动收集子 PointMarker
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    presetId: "Combat.SpawnArea", required: false),

                // 独立点位——可选，允许多个（与 spawnArea 至少一个非空）
                new MarkerRequirement("spawnPoints", MarkerTypeIds.Point,
                    presetId: "Combat.PresetPoint", required: false, allowMultiple: true, minCount: 0),
            }
        };
    }
}
