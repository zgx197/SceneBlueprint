#nullable enable
using System;

namespace NodeGraph.Core
{
    /// <summary>
    /// 连线，连接一个输出端口到一个输入端口。
    /// 可通过 UserData 附加业务数据（如 TransitionConditionData）。
    /// </summary>
    public class Edge
    {
        /// <summary>连线唯一 ID（GUID）</summary>
        public string Id { get; }

        /// <summary>源端口 ID（通常为 Output）</summary>
        public string SourcePortId { get; }

        /// <summary>目标端口 ID（通常为 Input）</summary>
        public string TargetPortId { get; }

        /// <summary>业务层附加数据</summary>
        public IEdgeData? UserData { get; set; }

        public Edge(string id, string sourcePortId, string targetPortId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SourcePortId = sourcePortId ?? throw new ArgumentNullException(nameof(sourcePortId));
            TargetPortId = targetPortId ?? throw new ArgumentNullException(nameof(targetPortId));
        }

        public override string ToString() => $"Edge({Id}: {SourcePortId} → {TargetPortId})";
    }
}
