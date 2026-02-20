#nullable enable
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 专用的连接策略。
    /// <para>
    /// 在 NodeGraph 默认策略的基础上，增强了以下功能：
    /// 1. 严格的端口类型检查（Flow/Event/Data 必须匹配）
    /// 2. Data 端口的类型兼容性验证
    /// 3. 更详细的错误提示
    /// </para>
    /// </summary>
    public class SceneBlueprintConnectionPolicy : DefaultConnectionPolicy
    {
        private readonly ConnectionValidator _validator;

        public SceneBlueprintConnectionPolicy()
        {
            _validator = new ConnectionValidator();
        }

        public override ConnectionResult CanConnect(Graph graph, NodeGraph.Core.Port source, NodeGraph.Core.Port target)
        {
            // 先执行 NodeGraph 的基础验证（方向、作用域、环检测等）
            var baseResult = base.CanConnect(graph, source, target);
            if (baseResult != ConnectionResult.Success)
                return baseResult;

            // 转换为 PortDefinition 进行 SceneBlueprint 专用验证
            var sourcePortDef = ConvertToPortDefinition(source);
            var targetPortDef = ConvertToPortDefinition(target);

            // 使用 ConnectionValidator 进行详细验证
            var validationResult = _validator.ValidateConnection(sourcePortDef, targetPortDef);

            if (!validationResult.IsValid)
            {
                // 将验证结果映射到 ConnectionResult
                if (validationResult.Message.Contains("类型不匹配"))
                    return ConnectionResult.KindMismatch;
                if (validationResult.Message.Contains("数据类型不兼容"))
                    return ConnectionResult.DataTypeMismatch;
                if (validationResult.Message.Contains("方向"))
                    return ConnectionResult.SameDirection;

                return ConnectionResult.CustomRejected;
            }

            return ConnectionResult.Success;
        }

        /// <summary>
        /// 将 NodeGraph.Port 转换为 SceneBlueprint.PortDefinition
        /// </summary>
        private SceneBlueprint.Core.PortDefinition ConvertToPortDefinition(NodeGraph.Core.Port port)
        {
            return new SceneBlueprint.Core.PortDefinition
            {
                Id = port.SemanticId,
                DisplayName = port.Name,
                Kind = port.Kind,
                Direction = port.Direction,
                Capacity = port.Capacity,
                DataType = port.DataType
            };
        }

        /// <summary>
        /// 获取连接失败的友好提示信息
        /// </summary>
        public static string GetConnectionErrorMessage(ConnectionResult result)
        {
            return result switch
            {
                ConnectionResult.Success => "",
                ConnectionResult.SameNode => "不能连接同一个节点的端口",
                ConnectionResult.SameDirection => "端口方向不匹配（必须是 输出 → 输入）",
                ConnectionResult.KindMismatch => "端口类型不匹配（Flow 只能连 Flow，Event 只能连 Event，Data 只能连 Data）",
                ConnectionResult.DataTypeMismatch => "数据类型不兼容",
                ConnectionResult.CapacityExceeded => "端口已达到连接上限",
                ConnectionResult.CycleDetected => "连接会形成循环引用",
                ConnectionResult.DuplicateEdge => "已存在相同的连接",
                ConnectionResult.CustomRejected => "连接被策略拒绝",
                _ => "未知错误"
            };
        }
    }
}
