#nullable enable
using NodeGraph.Math;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  行动定义 (ActionDefinition)
    //
    //  ActionDefinition 是场景蓝图系统的核心概念，类似于 GAS 中的
    //  GameplayAbility。它用纯数据描述“一种行动是什么样子”：
    //    - 它有什么属性（Properties）
    //    - 它有什么端口（Ports）
    //    - 它属于什么分类（Category）
    //    - 它是瞬时的还是持续的（Duration）
    //
    //  整体架构：
    //
    //  ActionDefinition    ───  “这是什么类型的行动”（元数据/模板）
    //       │
    //       ├── PortDefinition[]      ───  端口声明（决定节点能连哪些线）
    //       ├── PropertyDefinition[]  ───  属性声明（决定 Inspector 长什么样）
    //       └── 元数据 (TypeId, 分类, 颜色…)
    //
    //  ActionNodeData      ───  “这个具体节点的数据”（实例数据）
    //       │
    //       ├── ActionTypeId          ───  指向哪个 ActionDefinition
    //       └── PropertyBag           ───  属性的实际值
    //
    //  与 GAS 的映射：
    //    ActionDefinition  ↔  GameplayAbility 的 CDO (Class Default Object)
    //    ActionNodeData    ↔  GameplayAbility 的实例
    //    ActionRegistry    ↔  AbilitySystemComponent 的注册表
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 行动持续类型——决定行动的生命周期行为。
    /// <para>
    /// 生命周期：Inactive → Activated → Running → Completed / Cancelled
    /// </para>
    /// </summary>
    public enum ActionDuration
    {
        /// <summary>
        /// 瞬时行动——执行一次就完成。
        /// <para>典型：放置预设怪、切换灯光、触发事件</para>
        /// </summary>
        Instant,
        /// <summary>
        /// 持续行动——有运行状态，需要等待完成。
        /// <para>典型：节奏刷怪（多波）、摄像机跟踪、延时等待</para>
        /// </summary>
        Duration,
        /// <summary>
        /// 被动行动——条件满足时自动响应。
        /// <para>典型：玩家进入区域时触发、HP 低于阈值时响应</para>
        /// </summary>
        Passive
    }

    /// <summary>
    /// 行动定义——行动类型的元数据描述。
    /// <para>
    /// 用数据声明一种行动“长什么样、有哪些属性、能连哪些线”。
    /// 编辑器根据这个定义自动生成节点外观、Inspector 面板、搜索菜单等。
    /// </para>
    /// </summary>
    /// <example>
    /// 创建方式（通过 IActionDefinitionProvider 实现）：
    /// <code>
    /// [ActionType("Combat.Spawn")]
    /// public class SpawnActionDef : IActionDefinitionProvider
    /// {
    ///     public ActionDefinition Define() => new ActionDefinition
    ///     {
    ///         TypeId = "Combat.Spawn",
    ///         DisplayName = "刷怪",
    ///         Category = "Combat",
    ///         Duration = ActionDuration.Duration,
    ///         Ports = new[] { Port.FlowIn("in"), Port.FlowOut("out") },
    ///         Properties = new[] { Prop.Int("count", "数量", defaultValue: 5) }
    ///     };
    /// }
    /// </code>
    /// </example>
    public class ActionDefinition
    {
        // ─── 元数据 ───

        /// <summary>
        /// 全局唯一类型 ID，格式为 "域.行动名"。
        /// <para>示例："Combat.Spawn", "Presentation.Camera", "Flow.Start"</para>
        /// <para>在整个 ActionRegistry 中必须唯一，是查找和引用行动类型的主键。</para>
        /// </summary>
        public string TypeId { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的名称，如 "刷怪", "摄像机控制", "延迟"
        /// <para>建议使用中文，让策划可以直观理解。搜索窗可通过此名模糊搜索。</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 行动分类——用于搜索窗分组和 Registry 查询。
        /// <para>预定义分类："Flow"(流程), "Combat"(战斗), "Presentation"(表现)。
        /// 可自由扩展新分类。</para>
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>描述文本——在搜索窗悬停时显示，帮助策划理解该行动的用途</summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 节点主题色——编辑器中节点头部的颜色，用于视觉区分不同类型的行动。
        /// <para>使用 NodeGraph.Math.Color4，无 Unity 依赖。</para>
        /// </summary>
        public Color4 ThemeColor { get; set; } = Color4.Gray;

        /// <summary>图标标识（可选）——用于在节点头部或搜索窗中显示小图标</summary>
        public string? Icon { get; set; }

        // ─── 端口声明 ───

        /// <summary>
        /// 端口定义列表——声明该行动节点有哪些输入和输出端口。
        /// <para>端口决定了节点能连哪些线。使用 <see cref="Port"/> 工厂创建。</para>
        /// </summary>
        public PortDefinition[] Ports { get; set; } = System.Array.Empty<PortDefinition>();

        // ─── 属性声明 ───

        /// <summary>
        /// 属性定义列表——声明该行动有哪些可编辑字段。
        /// <para>Inspector 会根据这些定义自动生成 UI 控件。使用 <see cref="Prop"/> 工厂创建。</para>
        /// </summary>
        public PropertyDefinition[] Properties { get; set; } = System.Array.Empty<PropertyDefinition>();

        // ─── 行为标记 ───

        /// <summary>
        /// 行动持续类型——决定运行时的生命周期行为。
        /// <para>默认为 Instant（瞬时完成）。</para>
        /// </summary>
        public ActionDuration Duration { get; set; } = ActionDuration.Instant;
    }
}
