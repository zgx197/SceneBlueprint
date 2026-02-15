#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Monster
{
    /// <summary>
    /// Monster.Pool - 怪物池配置节点
    /// <para>
    /// 节点类型：Provider（配置提供者）
    /// 特点：只有 Data 输出端口，无 Flow 端口
    /// 用途：为 Spawn.Execute 等节点提供怪物配置数据
    /// </para>
    /// </summary>
    [ActionType("Monster.Pool")]
    public class MonsterPoolDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Monster.Pool",
            DisplayName = "怪物池",
            Category = "Monster",
            Description = "配置一组怪物模板，用于随机或按权重选择",
            ThemeColor = new Color4(0.9f, 0.5f, 0.3f), // 橙红色 - 怪物
            Duration = ActionDuration.Instant, // Provider 节点是 Instant

            Ports = new[]
            {
                // Provider 节点只有 Data 输出
                Port.DataOut("monsters", "怪物配置", DataTypes.MonsterConfigArray,
                    description: "怪物配置列表，可被 Spawn 节点使用")
            },

            Properties = new[]
            {
                // 怪物模板列表（可以是多个模板的引用）
                Prop.AssetRef("templates", "怪物模板", order: 0),

                // 选择模式：随机 / 顺序 / 按权重
                Prop.Enum("selectionMode", "选择模式",
                    new[] { "Random", "Sequential", "Weighted" },
                    defaultValue: "Random", order: 1),

                // 权重配置（当选择模式为 Weighted 时使用）
                Prop.String("weights", "权重配置", defaultValue: "1,1,1", order: 2),

                // 等级范围
                Prop.Int("minLevel", "最小等级", defaultValue: 1, min: 1, max: 100, order: 3),
                Prop.Int("maxLevel", "最大等级", defaultValue: 1, min: 1, max: 100, order: 4)
            }
        };
    }
}
