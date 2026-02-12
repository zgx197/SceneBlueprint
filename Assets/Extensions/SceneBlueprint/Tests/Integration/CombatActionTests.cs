#nullable enable
using System.Linq;
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Integration
{
    /// <summary>
    /// Step 9: Combat 域行动测试——验证 Spawn 和 PlacePreset 的注册、端口、属性和分类。
    /// <para>
    /// 测试范围：
    /// - Spawn 注册、端口（in/out/onWaveComplete/onAllComplete）、属性、VisibleWhen
    /// - PlacePreset 注册、端口、SceneBinding 绑定类型
    /// - 所有 Combat 行动的 Category 都是 "Combat"
    /// </para>
    /// </summary>
    public class CombatActionTests
    {
        private ActionRegistry _registry = null!;

        /// <summary>每个测试前初始化 Registry 并自动发现所有行动</summary>
        [SetUp]
        public void Setup()
        {
            _registry = new ActionRegistry();
            _registry.AutoDiscover();
        }

        /// <summary>验证 Spawn 已注册且分类为 Combat、类型为 Duration</summary>
        [Test]
        public void CombatSpawn_IsRegistered()
        {
            Assert.IsTrue(_registry.TryGet("Combat.Spawn", out var def));
            Assert.AreEqual("Combat", def.Category);
            Assert.AreEqual(ActionDuration.Duration, def.Duration);
        }

        /// <summary>验证 Spawn 有 4 个端口：in/out/onWaveComplete/onAllComplete</summary>
        [Test]
        public void CombatSpawn_HasCorrectPorts()
        {
            var def = _registry.Get("Combat.Spawn");

            Assert.IsTrue(def.Ports.Any(p => p.Id == "in" && p.Direction == PortDirection.In));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "out" && p.Direction == PortDirection.Out));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "onWaveComplete" && p.Direction == PortDirection.Out));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "onAllComplete" && p.Direction == PortDirection.Out));
        }

        /// <summary>验证 Spawn 包含所有必要属性：template/tempoType/monstersPerWave/spawnArea</summary>
        [Test]
        public void CombatSpawn_HasRequiredProperties()
        {
            var def = _registry.Get("Combat.Spawn");

            Assert.IsTrue(def.Properties.Any(p => p.Key == "template"), "缺少 template 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "tempoType"), "缺少 tempoType 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "monstersPerWave"), "缺少 monstersPerWave 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "spawnArea"), "缺少 spawnArea 属性");
        }

        /// <summary>验证 Spawn 的 interval 属性的 VisibleWhen 为 "tempoType == Interval"</summary>
        [Test]
        public void CombatSpawn_IntervalProperty_HasVisibleWhen()
        {
            var def = _registry.Get("Combat.Spawn");
            var interval = def.Properties.First(p => p.Key == "interval");

            Assert.AreEqual("tempoType == Interval", interval.VisibleWhen);
        }

        /// <summary>验证 PlacePreset 已注册且分类为 Combat、类型为 Instant</summary>
        [Test]
        public void CombatPlacePreset_IsRegistered()
        {
            Assert.IsTrue(_registry.TryGet("Combat.PlacePreset", out var def));
            Assert.AreEqual("Combat", def.Category);
            Assert.AreEqual(ActionDuration.Instant, def.Duration);
        }

        /// <summary>验证 PlacePreset 有 2 个端口：in + out</summary>
        [Test]
        public void CombatPlacePreset_HasCorrectPorts()
        {
            var def = _registry.Get("Combat.PlacePreset");

            Assert.AreEqual(2, def.Ports.Length); // in + out
            Assert.IsTrue(def.Ports.Any(p => p.Id == "in"));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "out"));
        }

        /// <summary>验证 PlacePreset 的 presetPoints 属性为 SceneBinding 类型且绑定为 Transform</summary>
        [Test]
        public void CombatPlacePreset_HasSceneBinding()
        {
            var def = _registry.Get("Combat.PlacePreset");
            var binding = def.Properties.First(p => p.Key == "presetPoints");

            Assert.AreEqual(PropertyType.SceneBinding, binding.Type);
            Assert.AreEqual(BindingType.Transform, binding.SceneBindingType);
        }

        /// <summary>验证所有 Combat 行动的 Category 都是 "Combat"（至少 2 个）</summary>
        [Test]
        public void AllCombatActions_BelongToCombatCategory()
        {
            var combatActions = _registry.GetByCategory("Combat");

            Assert.GreaterOrEqual(combatActions.Count, 2);
            foreach (var action in combatActions)
                Assert.AreEqual("Combat", action.Category);
        }
    }
}
