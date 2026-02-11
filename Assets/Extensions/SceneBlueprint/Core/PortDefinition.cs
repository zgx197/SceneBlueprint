#nullable enable
using System;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  端口定义 (PortDefinition)
    //
    //  端口是行动节点上的连接点，用于表达行动之间的执行流。
    //  每个端口有方向（输入/输出）和容量（单连接/多连接）。
    //
    //  设计思路：
    //  - 输入端口通常是 Multiple（允许多个前置行动连入）
    //  - 流程输出端口通常是 Single（只走一条路径）
    //  - 事件输出端口是 Multiple（一个事件可以触发多个后续行动）
    //
    //  示例：
    //    Spawn 节点有 4 个端口：
    //      in(输入)  out(输出)  onWaveComplete(波次完成)  onAllComplete(全部完成)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 端口方向——决定连线从哪边进出
    /// </summary>
    public enum PortDirection
    {
        /// <summary>输入端口——接收来自上游节点的连线</summary>
        In,
        /// <summary>输出端口——向下游节点发出连线</summary>
        Out
    }

    /// <summary>
    /// 端口容量——决定一个端口能连几条线
    /// </summary>
    public enum PortCapacity
    {
        /// <summary>单连接——只能连一条线（如主流程输出端口）</summary>
        Single,
        /// <summary>多连接——可以连多条线（如输入端口、事件端口）</summary>
        Multiple
    }

    /// <summary>
    /// 端口定义——声明一个行动节点上的输入/输出端口。
    /// <para>
    /// 端口定义是 <see cref="ActionDefinition"/> 的一部分，
    /// 在 ActionDefinition.Ports 数组中声明该行动有哪些端口。
    /// </para>
    /// <para>
    /// 端口 ID 在同一个 ActionDefinition 内必须唯一，
    /// 它会作为连线数据中的 FromPortId / ToPortId 使用。
    /// </para>
    /// </summary>
    public class PortDefinition
    {
        /// <summary>
        /// 端口唯一 ID，如 "in", "out", "onWaveComplete"
        /// <para>在同一个 ActionDefinition 内必须唯一</para>
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的端口名，如 "输入", "输出", "波次完成"
        /// <para>为空时编辑器可回退显示 Id</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>端口方向——In(输入) 或 Out(输出)</summary>
        public PortDirection Direction { get; set; }

        /// <summary>端口容量——Single(单连接) 或 Multiple(多连接)</summary>
        public PortCapacity Capacity { get; set; }
    }

    /// <summary>
    /// 端口便捷工厂——提供常用端口的快捷创建方法。
    /// <para>
    /// 使用示例：
    /// <code>
    /// Ports = new[] {
    ///     Port.FlowIn("in"),                      // 标准输入
    ///     Port.FlowOut("out"),                     // 标准输出
    ///     Port.EventOut("onWaveComplete", "波次完成") // 事件输出（可连多条线）
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public static class Port
    {
        /// <summary>
        /// 创建流入端口（多连接）。
        /// <para>输入端口默认允许多个上游节点连入（Multiple），
        /// 这样同一个行动可以被多条路径触发。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "in"</param>
        /// <param name="displayName">显示名，为空则使用 id</param>
        public static PortDefinition FlowIn(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Direction = PortDirection.In,
                Capacity = PortCapacity.Multiple
            };
        }

        /// <summary>
        /// 创建流出端口（单连接）。
        /// <para>标准流程输出端口只允许连一条线，表示唯一的后续路径。
        /// 如果需要多条路径（如 Branch 的 true/false），应创建多个 FlowOut 端口。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "out"</param>
        /// <param name="displayName">显示名，为空则使用 id</param>
        public static PortDefinition FlowOut(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Direction = PortDirection.Out,
                Capacity = PortCapacity.Single
            };
        }

        /// <summary>
        /// 创建事件输出端口（多连接）。
        /// <para>事件端口允许连多条线，一个事件可以同时触发多个后续行动。
        /// 典型用途：onWaveComplete（波次完成时可同时触发增援和摄像机切换）。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "onWaveComplete"</param>
        /// <param name="displayName">显示名，如 "波次完成"</param>
        public static PortDefinition EventOut(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Direction = PortDirection.Out,
                Capacity = PortCapacity.Multiple
            };
        }
    }
}
