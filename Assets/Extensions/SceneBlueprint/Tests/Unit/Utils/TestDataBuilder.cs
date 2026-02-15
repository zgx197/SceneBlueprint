#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Utils
{
    /// <summary>
    /// 测试数据构建器——提供标准化的测试数据创建方法。
    /// <para>
    /// 使用构建器模式创建各种测试对象，避免在测试中重复编写创建逻辑。
    /// 支持链式调用和自定义配置，提高测试代码的可读性和可维护性。
    /// </para>
    /// </summary>
    public static class TestDataBuilder
    {
        // ─── ActionDefinition 构建器 ───

        /// <summary>创建基本的 ActionDefinition，可选择性覆盖默认值</summary>
        public static ActionDefinition CreateActionDefinition(
            string typeId = "Test.Action",
            string displayName = "测试行动",
            string category = "Test",
            ActionDuration duration = ActionDuration.Instant,
            PortDefinition[]? ports = null,
            PropertyDefinition[]? properties = null)
        {
            return new ActionDefinition
            {
                TypeId = typeId,
                DisplayName = displayName,
                Category = category,
                Description = $"{displayName}的描述",
                ThemeColor = new Color4(0.5f, 0.5f, 0.5f),
                Duration = duration,
                Ports = ports ?? new[] { Port.In("in"), Port.Event("out") },
                Properties = properties ?? System.Array.Empty<PropertyDefinition>()
            };
        }

        /// <summary>创建带有指定属性的 ActionDefinition</summary>
        public static ActionDefinition CreateActionWithProperties(
            string typeId,
            params PropertyDefinition[] properties)
        {
            return CreateActionDefinition(typeId: typeId, properties: properties);
        }

        /// <summary>创建 Flow 域的标准行动定义</summary>
        public static ActionDefinition CreateFlowAction(string subType = "Test") =>
            CreateActionDefinition($"Flow.{subType}", $"流程{subType}", "Flow");

        /// <summary>创建 Combat 域的标准行动定义</summary>
        public static ActionDefinition CreateCombatAction(string subType = "Test") =>
            CreateActionDefinition($"Combat.{subType}", $"战斗{subType}", "Combat", ActionDuration.Duration);

        // ─── PropertyBag 构建器 ───

        /// <summary>创建空的 PropertyBag</summary>
        public static PropertyBag CreateEmptyPropertyBag() => new PropertyBag();

        /// <summary>创建包含指定键值对的 PropertyBag</summary>
        public static PropertyBag CreatePropertyBag(params (string key, object value)[] values)
        {
            var bag = new PropertyBag();
            foreach (var (key, value) in values)
                bag.Set(key, value);
            return bag;
        }

        /// <summary>创建包含各种类型数据的 PropertyBag（用于序列化测试）</summary>
        public static PropertyBag CreateMixedTypePropertyBag() =>
            CreatePropertyBag(
                ("name", "测试名称"),
                ("count", 42),
                ("rate", 3.14f),
                ("active", true)
            );

        // ─── PropertyDefinition 构建器 ───

        /// <summary>创建标准的字符串属性定义</summary>
        public static PropertyDefinition CreateStringProperty(
            string key = "testString",
            string displayName = "测试字符串",
            string defaultValue = "default") =>
            Prop.String(key, displayName, defaultValue: defaultValue);

        /// <summary>创建标准的整数属性定义</summary>
        public static PropertyDefinition CreateIntProperty(
            string key = "testInt",
            string displayName = "测试整数",
            int defaultValue = 0,
            int? min = null,
            int? max = null) =>
            Prop.Int(key, displayName, defaultValue: defaultValue, min: min, max: max);

        /// <summary>创建标准的浮点数属性定义</summary>
        public static PropertyDefinition CreateFloatProperty(
            string key = "testFloat",
            string displayName = "测试浮点数",
            float defaultValue = 0f,
            float? min = null,
            float? max = null) =>
            Prop.Float(key, displayName, defaultValue: defaultValue, min: min, max: max);

        /// <summary>创建带有 VisibleWhen 条件的属性定义</summary>
        public static PropertyDefinition CreateConditionalProperty(
            string key = "conditionalProp",
            string visibleWhen = "mode == Test") =>
            Prop.String(key, "条件属性", visibleWhen: visibleWhen);

        // ─── ActionNodeData 构建器 ───

        /// <summary>从 ActionDefinition 创建 ActionNodeData</summary>
        public static ActionNodeData CreateNodeData(ActionDefinition definition) =>
            ActionNodeData.CreateFromDefinition(definition);

        /// <summary>创建带有自定义属性值的 ActionNodeData</summary>
        public static ActionNodeData CreateNodeDataWithProperties(
            ActionDefinition definition,
            params (string key, object value)[] propertyValues)
        {
            var nodeData = ActionNodeData.CreateFromDefinition(definition);
            foreach (var (key, value) in propertyValues)
                nodeData.Properties.Set(key, value);
            return nodeData;
        }

        // ─── ActionRegistry 构建器 ───

        /// <summary>创建空的 ActionRegistry</summary>
        public static ActionRegistry CreateEmptyRegistry() => new ActionRegistry();

        /// <summary>创建已进行 AutoDiscover 的 ActionRegistry</summary>
        public static ActionRegistry CreateDiscoveredRegistry()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();
            return registry;
        }

        /// <summary>创建包含指定行动定义的 ActionRegistry</summary>
        public static ActionRegistry CreateRegistryWithActions(params ActionDefinition[] definitions)
        {
            var registry = new ActionRegistry();
            foreach (var def in definitions)
                registry.Register(def);
            return registry;
        }

        // ─── 复杂场景数据构建器 ───

        /// <summary>创建完整的 Spawn 行动数据（用于集成测试）</summary>
        public static ActionNodeData CreateSpawnNodeData()
        {
            var definition = new ActionDefinition
            {
                TypeId = "Combat.Spawn",
                DisplayName = "刷怪",
                Category = "Combat",
                Duration = ActionDuration.Duration,
                Properties = new[]
                {
                    Prop.AssetRef("template", "怪物模板"),
                    Prop.Enum("tempoType", "节奏类型", new[] { "Instant", "Interval", "Burst" }, "Interval"),
                    Prop.Float("interval", "间隔", defaultValue: 2f, visibleWhen: "tempoType == Interval"),
                    Prop.Int("totalWaves", "总波数", defaultValue: 3, visibleWhen: "tempoType != Instant"),
                    Prop.Int("monstersPerWave", "每波数量", defaultValue: 5)
                }
            };
            
            return CreateNodeDataWithProperties(definition,
                ("template", "elite_monster_01"),
                ("tempoType", "Interval"),
                ("interval", 2.5f),
                ("totalWaves", 3),
                ("monstersPerWave", 8)
            );
        }

        /// <summary>创建完整的流程测试场景数据</summary>
        public static (ActionRegistry registry, ActionNodeData[] nodes) CreateFullFlowTestData()
        {
            var registry = CreateDiscoveredRegistry();
            
            var startNode = CreateNodeData(registry.Get("Flow.Start"));
            var delayNode = CreateNodeDataWithProperties(registry.Get("Flow.Delay"), ("duration", 1.5f));
            var spawnNode = CreateSpawnNodeData();
            var endNode = CreateNodeData(registry.Get("Flow.End"));
            
            return (registry, new[] { startNode, delayNode, spawnNode, endNode });
        }

        // ─── 边界条件和异常数据构建器 ───

        /// <summary>创建具有边界值的属性数据</summary>
        public static PropertyBag CreateBoundaryValuePropertyBag() =>
            CreatePropertyBag(
                ("minInt", int.MinValue),
                ("maxInt", int.MaxValue),
                ("minFloat", float.MinValue),
                ("maxFloat", float.MaxValue),
                ("emptyString", ""),
                ("nullString", (string?)null)
            );

        /// <summary>创建无效的 ActionDefinition（用于异常测试）</summary>
        public static ActionDefinition CreateInvalidActionDefinition() =>
            new ActionDefinition
            {
                TypeId = "", // 无效的空 TypeId
                DisplayName = "",
                Category = "",
                Duration = ActionDuration.Instant,
                Ports = System.Array.Empty<PortDefinition>(),
                Properties = System.Array.Empty<PropertyDefinition>()
            };

        // ─── 性能测试数据构建器 ───

        /// <summary>创建大量测试数据（用于性能测试）</summary>
        public static ActionDefinition[] CreateLargeActionSet(int count = 100)
        {
            var actions = new ActionDefinition[count];
            for (int i = 0; i < count; i++)
            {
                actions[i] = CreateActionDefinition(
                    typeId: $"Perf.Action{i:D3}",
                    displayName: $"性能测试行动{i}",
                    category: "Performance"
                );
            }
            return actions;
        }

        /// <summary>创建包含大量属性的 PropertyBag</summary>
        public static PropertyBag CreateLargePropertyBag(int count = 50)
        {
            var bag = new PropertyBag();
            for (int i = 0; i < count; i++)
            {
                bag.Set($"prop{i}", $"value{i}");
                bag.Set($"num{i}", i);
                bag.Set($"flag{i}", i % 2 == 0);
                bag.Set($"rate{i}", i * 0.1f);
            }
            return bag;
        }
    }
}
