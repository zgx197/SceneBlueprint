#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// 波次刷怪行动定义——持续型，在区域内按波次随机刷怪。
    /// <para>
    /// 与预设怪不同：
    /// - <b>Spawn.Preset</b>：瞬时型，在策划预设的精确点位放置怪物
    /// - <b>Spawn.Wave</b>：持续型，按波次在区域内随机位置生成怪物
    /// </para>
    /// <para>
    /// 工作流：
    /// 1. 策划在 SceneView 中创建 AreaMarker，定义刷怪区域
    /// 2. 在 AreaMarker 上添加 WaveSpawnConfig 标注，配置怪物池和波次规则
    /// 3. Blueprint 中创建 Spawn.Wave 节点，绑定该 AreaMarker
    /// 4. 导出时收集 AreaMarker 几何数据 + WaveSpawnConfig 标注
    /// 5. 运行时按波次在区域内随机位置生成怪物
    /// </para>
    /// <para>典型用途：副本关卡波次挑战、区域防守战、触发式刷怪。</para>
    /// <para>节点拓扑：[前置] ─in→ [Spawn.Wave] ─out→ [后续]</para>
    /// <para>out 端口在所有波次全部完成后触发。</para>
    /// </summary>
    [ActionType("Spawn.Wave")]
    public class SpawnWaveActionDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Spawn.Wave",
            DisplayName = "波次刷怪",
            Category = "Spawn",
            Description = "在区域内按波次随机生成怪物（绑定带 WaveSpawnConfig 的 AreaMarker）",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f),
            Duration = ActionDuration.Duration, // 持续型——多波次需要时间
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "完成") // 所有波次完成后触发
            },
            Properties = new[]
            {
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 0)
            },
            SceneRequirements = new[]
            {
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    required: true, displayName: "刷怪区域"),
            }
        };
    }
}
