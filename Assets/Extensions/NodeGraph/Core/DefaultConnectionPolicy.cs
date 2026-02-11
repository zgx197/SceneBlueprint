#nullable enable
using System.Linq;

namespace NodeGraph.Core
{
    /// <summary>
    /// 默认连接策略。按顺序检查：
    /// 1. 同一节点 → SameNode
    /// 2. 同方向 → SameDirection
    /// 3. Kind 不匹配 → KindMismatch
    /// 4. 数据类型不兼容 → DataTypeMismatch
    /// 5. 已存在相同连接 → DuplicateEdge
    /// 6. 容量超限 → CapacityExceeded
    /// 7. DAG 模式下环检测 → CycleDetected
    /// 8. 全部通过 → Success
    /// 
    /// 业务层可继承并 override CanConnect 添加自定义规则。
    /// </summary>
    public class DefaultConnectionPolicy : IConnectionPolicy
    {
        public virtual ConnectionResult CanConnect(Graph graph, Port source, Port target)
        {
            // 1. 不能连接同一节点的端口
            if (source.NodeId == target.NodeId)
                return ConnectionResult.SameNode;

            // 2. 方向必须不同（一个 Input 一个 Output）
            if (source.Direction == target.Direction)
                return ConnectionResult.SameDirection;

            // 确保 source 是 Output、target 是 Input（自动纠正顺序由 Graph 层处理）
            var outPort = source.Direction == PortDirection.Output ? source : target;
            var inPort = source.Direction == PortDirection.Input ? source : target;

            // 3. Kind 必须匹配
            if (outPort.Kind != inPort.Kind)
                return ConnectionResult.KindMismatch;

            // 4. 数据类型兼容性检查
            if (!graph.Settings.TypeCompatibility.IsCompatible(outPort.DataType, inPort.DataType))
                return ConnectionResult.DataTypeMismatch;

            // 5. 不允许重复连接
            if (graph.Edges.Any(e => e.SourcePortId == outPort.Id && e.TargetPortId == inPort.Id))
                return ConnectionResult.DuplicateEdge;

            // 6. 容量检查
            if (outPort.Capacity == PortCapacity.Single &&
                graph.GetEdgesForPort(outPort.Id).Any())
                return ConnectionResult.CapacityExceeded;

            if (inPort.Capacity == PortCapacity.Single &&
                graph.GetEdgesForPort(inPort.Id).Any())
                return ConnectionResult.CapacityExceeded;

            // 7. DAG 模式下环检测
            if (graph.Settings.Topology == GraphTopologyPolicy.DAG)
            {
                var sourceNode = graph.FindNode(outPort.NodeId);
                var targetNode = graph.FindNode(inPort.NodeId);
                if (sourceNode != null && targetNode != null)
                {
                    if (GraphAlgorithms.WouldCreateCycle(graph, outPort.NodeId, inPort.NodeId))
                        return ConnectionResult.CycleDetected;
                }
            }

            return ConnectionResult.Success;
        }
    }
}
