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
            Description = "在场景预设点位瞬时放置怪物",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色 - 与 Spawn.Wave 统一
            Duration = ActionDuration.Instant, // 瞬时型——一次性放置
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },
            Properties = new[]
            {
                // 怪物模板——引用怪物配置资产
                Prop.AssetRef("template", "怪物模板", order: 0),

                // 预设点组——场景中的一组 Transform 点位，怪物会被放置在这些位置
                // 使用 BindingType.Transform 因为需要精确的位置和朝向
                Prop.SceneBinding("presetPoints", "预设点组", BindingType.Transform, order: 1)
            },

            // ─── 场景标记需求 ───
            SceneRequirements = new[]
            {
                // 放置点位——必需，允许多个点位（创建参数由 MarkerPresetSO 控制）
                new MarkerRequirement("presetPoints", MarkerTypeIds.Point,
                    presetId: "Combat.PresetPoint", required: true, allowMultiple: true, minCount: 1),
            }
        };
    }
}
