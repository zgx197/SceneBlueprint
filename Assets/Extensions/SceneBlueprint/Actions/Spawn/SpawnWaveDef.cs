#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// 波次刷怪行动定义——持续型，在区域内按波次随机刷怪。
    /// <para>
    /// 职责拆分后的设计：
    /// - WaveSpawnConfig（SceneView 层）：只描述怪物池（"这个区域能刷什么怪"）
    /// - Spawn.Wave 节点（Blueprint 层）：描述波次逻辑（"怎么刷、刷几波、间隔多久"）
    /// </para>
    /// <para>
    /// 工作流：
    /// 1. 策划在 SceneView 中创建 AreaMarker，定义刷怪区域
    /// 2. 在 AreaMarker 上添加 WaveSpawnConfig 标注，配置怪物池（monsterId/tag/weight）
    /// 3. Blueprint 中创建 Spawn.Wave 节点，绑定该 AreaMarker，配置波次列表（waves）
    /// 4. 导出时收集 AreaMarker 几何数据 + WaveSpawnConfig 标注 + waves 属性
    /// 5. 运行时 SpawnWaveSystem 按波次配置，从怪物池中按标签筛选、按权重抽取
    /// </para>
    /// <para>
    /// 端口语义：
    /// - in：上游节点完成时启动波次刷怪
    /// - out：所有波次完成后触发后续节点
    /// - onWaveStart：每波开始生成前触发（不阻塞刷怪），可连接事件节点（屏幕震动等）
    /// </para>
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
                Port.Out("out", "全部完成"),           // 所有波次完成后触发
                Port.Out("onWaveStart", "每波开始"),   // 每波开始时触发，不阻塞刷怪
                Port.DataOut("waveIndex",  "当前波次", DataTypes.Int),  // 数据端口：0-based 波次索引
                Port.DataOut("totalWaves", "总波次数", DataTypes.Int),  // 数据端口：配置的波次总数
            },
            Properties = new[]
            {
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 0),
                // 波次配置列表，StructList 类型
                // 侧边 Inspector 用 ReorderableList 编辑，节点画布显示摘要
                // 存储格式：JSON 数组字符串 [{"count":5,"intervalTicks":0,"monsterFilter":"Normal"}, ...]
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
                    order: 1),
            },
            SceneRequirements = new[]
            {
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    required: true, displayName: "刷怪区域"),
            },
            OutputVariables = new[]
            {
                OutputVar.Int("waveIndex", "当前波次"),
            }
        };
    }
}
