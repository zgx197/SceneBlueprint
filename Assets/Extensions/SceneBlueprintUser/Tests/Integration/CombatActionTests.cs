#nullable enable
using System.Linq;
using NUnit.Framework;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Tests.Integration
{
    /// <summary>
    /// Step 9: Spawn 域行动集成测试——验证 Wave/Preset 的注册、端口、属性和分类。
    /// <para>
    /// 测试范围：
    /// - Spawn.Wave 和 Spawn.Preset 均被 AutoDiscover 注册
    /// - Wave 为 Duration 类型，Preset 为 Instant 类型
    /// - 属性完整性（monsterTemplate, waveCount, spawnArea, presetPoints）
    /// </para>
    /// </summary>
    [TestFixture]
    public class SpawnActionTests
    {
        private ActionRegistry _registry = null!;

        /// <summary>每个测试前初始化 Registry 并自动发现所有行动</summary>
        [SetUp]
        public void Setup()
        {
            _registry = new ActionRegistry();
            _registry.AutoDiscover();
        }

        /// <summary>验证 Spawn.Wave 已注册且分类为 Spawn、类型为 Duration</summary>
        [Test]
        public void SpawnWave_IsRegistered()
        {
            Assert.IsTrue(_registry.TryGet("Spawn.Wave", out var def));
            Assert.AreEqual("Spawn", def.Category);
            Assert.AreEqual(ActionDuration.Duration, def.Duration);
        }

        /// <summary>验证 Spawn.Wave 有正确的端口（in, out, onWaveComplete）</summary>
        [Test]
        public void SpawnWave_HasCorrectPorts()
        {
            var def = _registry.Get("Spawn.Wave");

            Assert.IsTrue(def.Ports.Any(p => p.Id == "in" && p.Direction == PortDirection.Input));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "out" && p.Direction == PortDirection.Output));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "onWaveComplete" && p.Direction == PortDirection.Output));
        }

        /// <summary>验证 Spawn.Wave 拥有必需的属性</summary>
        [Test]
        public void SpawnWave_HasRequiredProperties()
        {
            var def = _registry.Get("Spawn.Wave");

            Assert.IsTrue(def.Properties.Any(p => p.Key == "monsterTemplate"), "缺少 monsterTemplate 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "waveCount"), "缺少 waveCount 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "monstersPerWave"), "缺少 monstersPerWave 属性");
            Assert.IsTrue(def.Properties.Any(p => p.Key == "spawnArea"), "缺少 spawnArea 属性");
        }

        /// <summary>验证 Spawn.Preset 已注册且分类为 Spawn、类型为 Instant</summary>
        [Test]
        public void SpawnPreset_IsRegistered()
        {
            Assert.IsTrue(_registry.TryGet("Spawn.Preset", out var def));
            Assert.AreEqual("Spawn", def.Category);
            Assert.AreEqual(ActionDuration.Instant, def.Duration);
        }

        /// <summary>验证 Spawn.Preset 有正确的端口（in, out）</summary>
        [Test]
        public void SpawnPreset_HasCorrectPorts()
        {
            var def = _registry.Get("Spawn.Preset");

            Assert.AreEqual(2, def.Ports.Length); // in + out
            Assert.IsTrue(def.Ports.Any(p => p.Id == "in"));
            Assert.IsTrue(def.Ports.Any(p => p.Id == "out"));
        }

        /// <summary>验证 Spawn.Preset 有场景绑定属性（presetPoints）</summary>
        [Test]
        public void SpawnPreset_HasSceneBinding()
        {
            var def = _registry.Get("Spawn.Preset");
            var binding = def.Properties.First(p => p.Key == "presetPoints");

            Assert.AreEqual(PropertyType.SceneBinding, binding.Type);
            Assert.AreEqual(BindingType.Transform, binding.SceneBindingType);
        }

        /// <summary>验证所有 Spawn 行动的 Category 都是 "Spawn"（至少 2 个）</summary>
        [Test]
        public void AllSpawnActions_BelongToSpawnCategory()
        {
            var spawnActions = _registry.GetByCategory("Spawn");

            Assert.GreaterOrEqual(spawnActions.Count, 2, "Spawn 分类至少应有 2 个节点（Wave + Preset）");
            foreach (var action in spawnActions)
                Assert.AreEqual("Spawn", action.Category);
        }
    }
}
