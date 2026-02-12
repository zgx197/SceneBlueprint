#nullable enable
using System.Linq;
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Integration
{
    /// <summary>
    /// Step 8: Flow 域内置行动测试——验证 Start/End/Delay/Branch/Join 的注册、端口、属性和分类。
    /// <para>
    /// 测试范围：
    /// - 所有 5 个 Flow 行动均被 AutoDiscover 注册
    /// - Start 只有 out 端口，End 只有 in 端口
    /// - Delay 有 duration 属性且为 Duration 类型
    /// - Branch 有 true/false 两个输出端口
    /// - Join 有 requiredCount 属性
    /// - 所有 Flow 行动的 Category 都是 "Flow"
    /// </para>
    /// </summary>
    public class FlowActionTests
    {
        private ActionRegistry _registry = null!;

        /// <summary>每个测试前初始化 Registry 并自动发现所有行动</summary>
        [SetUp]
        public void Setup()
        {
            _registry = new ActionRegistry();
            _registry.AutoDiscover();
        }

        /// <summary>验证所有 5 个 Flow 行动均被成功注册</summary>
        [Test]
        public void AllFlowActions_AreRegistered()
        {
            Assert.IsTrue(_registry.TryGet("Flow.Start", out _), "Flow.Start 未注册");
            Assert.IsTrue(_registry.TryGet("Flow.End", out _), "Flow.End 未注册");
            Assert.IsTrue(_registry.TryGet("Flow.Delay", out _), "Flow.Delay 未注册");
            Assert.IsTrue(_registry.TryGet("Flow.Branch", out _), "Flow.Branch 未注册");
            Assert.IsTrue(_registry.TryGet("Flow.Join", out _), "Flow.Join 未注册");
        }

        /// <summary>验证 Start 节点只有一个输出端口</summary>
        [Test]
        public void FlowStart_HasOnlyOutPort()
        {
            var def = _registry.Get("Flow.Start");

            Assert.AreEqual(1, def.Ports.Length);
            Assert.AreEqual("out", def.Ports[0].Id);
            Assert.AreEqual(PortDirection.Out, def.Ports[0].Direction);
        }

        /// <summary>验证 End 节点只有一个输入端口</summary>
        [Test]
        public void FlowEnd_HasOnlyInPort()
        {
            var def = _registry.Get("Flow.End");

            Assert.AreEqual(1, def.Ports.Length);
            Assert.AreEqual("in", def.Ports[0].Id);
            Assert.AreEqual(PortDirection.In, def.Ports[0].Direction);
        }

        /// <summary>验证 Delay 节点有 duration 属性且行动类型为 Duration</summary>
        [Test]
        public void FlowDelay_HasDurationProperty()
        {
            var def = _registry.Get("Flow.Delay");

            Assert.IsTrue(def.Properties.Any(p => p.Key == "duration" && p.Type == PropertyType.Float));
            Assert.AreEqual(ActionDuration.Duration, def.Duration);
        }

        /// <summary>验证 Branch 节点有 in/true/false 三个端口</summary>
        [Test]
        public void FlowBranch_HasTrueFalsePorts()
        {
            var def = _registry.Get("Flow.Branch");

            Assert.IsTrue(def.Ports.Any(p => p.Id == "true" && p.Direction == PortDirection.Out));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "false" && p.Direction == PortDirection.Out));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "in" && p.Direction == PortDirection.In));
        }

        /// <summary>验证 Join 节点有 requiredCount 整数属性</summary>
        [Test]
        public void FlowJoin_HasRequiredCountProperty()
        {
            var def = _registry.Get("Flow.Join");

            Assert.IsTrue(def.Properties.Any(p => p.Key == "requiredCount" && p.Type == PropertyType.Int));
        }

        /// <summary>验证所有 Flow 行动的 Category 都是 "Flow"</summary>
        [Test]
        public void AllFlowActions_BelongToFlowCategory()
        {
            var flowActions = _registry.GetByCategory("Flow");

            Assert.GreaterOrEqual(flowActions.Count, 5);
            foreach (var action in flowActions)
                Assert.AreEqual("Flow", action.Category);
        }
    }
}
