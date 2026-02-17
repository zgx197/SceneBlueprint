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
    /// 工作流：
    /// 1. 策划在 SceneView 中创建 AreaMarker，使用位置生成工具铺设子 PointMarker
    /// 2. 在每个 PointMarker 上添加 SpawnAnnotation 标注怪物信息
    /// 3. Blueprint 中创建 Spawn.Preset 节点，绑定该 AreaMarker
    /// 4. 导出时自动展开子 PointMarker，收集 SpawnAnnotation 数据
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
            Description = "在场景预设点位瞬时放置怪物（绑定 AreaMarker，自动收集子点位 + 标注）",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色 - 与 Spawn.Wave 统一
            Duration = ActionDuration.Instant, // 瞬时型——一次性放置
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },
            Properties = new[]
            {
                // 刷怪区域——绑定 AreaMarker，导出时自动收集其子 PointMarker + SpawnAnnotation
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 0)
            },

            // ─── 场景标记需求 ───
            SceneRequirements = new[]
            {
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    required: true, displayName: "刷怪区域"),
            }
        };
    }
}
