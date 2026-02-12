#nullable enable
using NUnit.Framework;
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Unit.Core
{
    /// <summary>
    /// Step 1: 数据类定义测试——验证 ActionDefinition、PortDefinition 的基本创建和字段正确性。
    /// <para>
    /// 测试范围：
    /// - ActionDefinition 创建后字段值是否正确
    /// - ActionDefinition 默认值是否合理
    /// - Port 工厂方法（FlowIn/FlowOut/EventOut）生成的端口方向和容量是否正确
    /// </para>
    /// </summary>
    public class ActionDefinitionTests
    {
        /// <summary>验证创建完整的 ActionDefinition 后所有字段值正确</summary>
        [Test]
        public void ActionDefinition_Create_HasCorrectFields()
        {
            var def = new ActionDefinition
            {
                TypeId = "Combat.Spawn",
                DisplayName = "刷怪",
                Category = "Combat",
                Duration = ActionDuration.Duration,
                ThemeColor = new Color4(0.2f, 0.7f, 0.3f),
                Ports = new[] { Port.FlowIn("in"), Port.FlowOut("out") },
                Properties = new[] { Prop.Int("count", "数量", defaultValue: 5) }
            };

            Assert.AreEqual("Combat.Spawn", def.TypeId);
            Assert.AreEqual("刷怪", def.DisplayName);
            Assert.AreEqual("Combat", def.Category);
            Assert.AreEqual(ActionDuration.Duration, def.Duration);
            Assert.AreEqual(2, def.Ports.Length);
            Assert.AreEqual(1, def.Properties.Length);
            Assert.AreEqual(5, def.Properties[0].DefaultValue);
        }

        /// <summary>验证 ActionDefinition 的默认值是否合理（空字符串、Instant、空数组）</summary>
        [Test]
        public void ActionDefinition_DefaultValues_AreReasonable()
        {
            var def = new ActionDefinition();

            Assert.AreEqual("", def.TypeId);
            Assert.AreEqual("", def.DisplayName);
            Assert.AreEqual("", def.Category);
            Assert.AreEqual(ActionDuration.Instant, def.Duration);
            Assert.AreEqual(0, def.Ports.Length);
            Assert.AreEqual(0, def.Properties.Length);
        }

        /// <summary>验证 FlowIn 工厂生成的端口：方向=In，容量=Multiple</summary>
        [Test]
        public void PortDefinition_FlowIn_HasCorrectDirection()
        {
            var port = Port.FlowIn("in", "输入");

            Assert.AreEqual("in", port.Id);
            Assert.AreEqual("输入", port.DisplayName);
            Assert.AreEqual(PortDirection.In, port.Direction);
            Assert.AreEqual(PortCapacity.Multiple, port.Capacity);
        }

        /// <summary>验证 FlowOut 工厂生成的端口：方向=Out，容量=Single</summary>
        [Test]
        public void PortDefinition_FlowOut_HasCorrectDirection()
        {
            var port = Port.FlowOut("out", "输出");

            Assert.AreEqual("out", port.Id);
            Assert.AreEqual(PortDirection.Out, port.Direction);
            Assert.AreEqual(PortCapacity.Single, port.Capacity);
        }

        /// <summary>验证 EventOut 工厂生成的端口：方向=Out，容量=Multiple（事件可连多条线）</summary>
        [Test]
        public void PortDefinition_EventOut_HasMultipleCapacity()
        {
            var port = Port.EventOut("onComplete", "完成时");

            Assert.AreEqual(PortDirection.Out, port.Direction);
            Assert.AreEqual(PortCapacity.Multiple, port.Capacity);
        }
    }
}
