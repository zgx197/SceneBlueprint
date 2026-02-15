#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Location
{
    /// <summary>
    /// Location.RandomArea - 在指定区域内生成随机位置点
    /// <para>
    /// 节点类型：Provider（配置提供者）
    /// 特点：只有 Data 输出端口，无 Flow 端口
    /// 用途：为 Spawn.Execute 等节点提供位置数据
    /// </para>
    /// </summary>
    [ActionType("Location.RandomArea")]
    public class LocationRandomAreaDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Location.RandomArea",
            DisplayName = "随机区域位置",
            Category = "Location",
            Description = "在指定区域内生成随机位置点",
            ThemeColor = new Color4(0.4f, 0.6f, 0.9f), // 浅蓝色 - 位置/空间
            Duration = ActionDuration.Instant, // Provider 节点通常是 Instant

            Ports = new[]
            {
                // Provider 节点没有 Flow 端口，只有 Data 输出
                Port.DataOut("positions", "位置列表", DataTypes.Vector3Array,
                    description: "在区域内随机生成的位置点列表")
            },

            Properties = new[]
            {
                // 刷怪区域绑定
                Prop.SceneBinding("area", "区域", BindingType.Area, order: 0),

                // 生成位置点的数量
                Prop.Int("count", "位置数量", defaultValue: 5, min: 1, max: 100, order: 1),

                // 随机种子（可选，用于可重复的随机）
                Prop.Int("seed", "随机种子", defaultValue: 0, min: 0, max: 999999, order: 2),

                // 最小间距（避免位置点过于密集）
                Prop.Float("minSpacing", "最小间距", defaultValue: 2f, min: 0f, max: 50f, order: 3)
            },

            SceneRequirements = new[]
            {
                new MarkerRequirement("area", MarkerTypeIds.Area,
                    displayName: "刷怪区域",
                    defaultTag: "Location.Area",
                    required: true)
            }
        };
    }
}
