#nullable enable
using NodeGraph.Core;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// SceneBlueprint 的连线数据，实现 <see cref="IEdgeData"/> 接口。
    /// <para>
    /// 扩展连线信息，包括端口类型（Control/Event/Data）和数据类型。
    /// 用于编辑器连线验证和导出时生成 Playbook 数据。
    /// </para>
    /// </summary>
    public class EdgeData : IEdgeData
    {
        /// <summary>
        /// 连线类型——对应源端口的 PortKind
        /// </summary>
        public PortKind Kind { get; set; } = PortKind.Control;

        /// <summary>
        /// 数据类型（仅当 Kind == Data 时有效）
        /// <para>存储此连线传递的数据类型，如 "Vector3[]", "EntityRef[]" 等</para>
        /// </summary>
        public string DataType { get; set; } = "";

        /// <summary>
        /// 条件数据（可选，用于未来扩展条件连线）
        /// <para>预留字段，用于支持"条件满足时才激活"的连线</para>
        /// </summary>
        public string Condition { get; set; } = "";
    }
}
