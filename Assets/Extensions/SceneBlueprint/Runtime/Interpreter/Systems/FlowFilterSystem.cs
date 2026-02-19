#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 条件过滤系统——处理 Flow.Filter 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 从 Blackboard 的 _activatedBy.{myId} 获取来源节点 ID（自动推断）
    /// 2. 拼接 Blackboard key = "{sourceId}.{key}" 读取变量值
    /// 3. 与目标值做比较（支持数字和字符串）
    /// 4. 条件满足 → 手动发射 pass 端口事件；不满足 → 发射 reject 端口事件
    /// 5. 进入 Listening 状态等待下一次事件（支持多次重入）
    /// 6. TransitionPropagated=true 防止 TransitionSystem 对本次完成重复传播出边
    /// </para>
    /// <para>
    /// 生命周期：Idle → Running → Listening → (收到新事件) → Running → Listening → ...
    /// 节点自身不会主动进入 Completed，由蓝图结束时统一清理。
    /// </para>
    /// </summary>
    public class FlowFilterSystem : BlueprintSystemBase
    {
        public override string Name => "FlowFilterSystem";
        public override int Order => 15; // FlowSystem(10) 之后，业务 System 之前

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices("Flow.Filter");
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                ProcessFilter(frame, idx, ref state);
            }
        }

        private static void ProcessFilter(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            var myActionId = frame.Actions[actionIndex].Id;

            // 读取属性
            var key = frame.GetProperty(actionIndex, "key");
            var op = frame.GetProperty(actionIndex, "op");
            var targetValue = frame.GetProperty(actionIndex, "value");

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[FlowFilterSystem] Flow.Filter (index={actionIndex}) key 为空，走 reject");
                EmitPortEvents(frame, actionIndex, "reject");
                // 进入 Listening 等待下一次事件，TransitionPropagated 防止出边重复传播
                state.Phase = ActionPhase.Listening;
                state.TransitionPropagated = true;
                return;
            }

            // 自动推断来源节点 ID
            var sourceId = frame.Blackboard.Get<string>($"_activatedBy.{myActionId}");
            string bbKey;
            if (!string.IsNullOrEmpty(sourceId))
            {
                bbKey = $"{sourceId}.{key}";
            }
            else
            {
                // 回退：直接用 key 作为 Blackboard key（允许手动写完整 key）
                bbKey = key;
                Debug.LogWarning($"[FlowFilterSystem] Flow.Filter (index={actionIndex}) 无法推断来源节点，" +
                                 $"直接使用 key=\"{key}\" 查找 Blackboard");
            }

            // 从 Blackboard 读取值
            object? bbValue = null;
            frame.Blackboard.TryGet<object>(bbKey, out bbValue);

            // 执行条件比较
            bool conditionMet = EvaluateCondition(bbValue, op, targetValue);

            string portId = conditionMet ? "pass" : "reject";
            Debug.Log($"[FlowFilterSystem] Flow.Filter (index={actionIndex}): " +
                      $"Blackboard[\"{bbKey}\"]={bbValue ?? "null"} {op} \"{targetValue}\" → {conditionMet} → {portId}");

            // 手动发射对应端口的出边事件
            EmitPortEvents(frame, actionIndex, portId);

            // 进入 Listening 状态等待下一次事件（支持多次重入）
            // TransitionPropagated=true 防止 TransitionSystem 对本次执行重复传播出边
            state.Phase = ActionPhase.Listening;
            state.TransitionPropagated = true;
        }

        /// <summary>
        /// 发射指定端口的所有出边事件。
        /// </summary>
        private static void EmitPortEvents(BlueprintFrame frame, int actionIndex, string portId)
        {
            var transitionIndices = frame.GetOutgoingTransitionIndices(actionIndex);
            for (int t = 0; t < transitionIndices.Count; t++)
            {
                var transition = frame.Transitions[transitionIndices[t]];
                if (transition.FromPortId == portId)
                {
                    var toIndex = frame.GetActionIndex(transition.ToActionId);
                    if (toIndex >= 0)
                    {
                        frame.PendingEvents.Add(new PortEvent(
                            actionIndex, portId, toIndex, transition.ToPortId));
                    }
                }
            }
        }

        /// <summary>
        /// 条件比较：先尝试数字比较，失败则字符串比较。
        /// </summary>
        private static bool EvaluateCondition(object? bbValue, string op, string targetValue)
        {
            if (bbValue == null)
                return op == "!="; // null != 任何值 为 true

            string bbStr = bbValue.ToString() ?? "";

            // 尝试数字比较
            if (double.TryParse(bbStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double bbNum) &&
                double.TryParse(targetValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double targetNum))
            {
                return op switch
                {
                    "==" => Math.Abs(bbNum - targetNum) < 0.0001,
                    "!=" => Math.Abs(bbNum - targetNum) >= 0.0001,
                    ">"  => bbNum > targetNum,
                    "<"  => bbNum < targetNum,
                    ">=" => bbNum >= targetNum,
                    "<=" => bbNum <= targetNum,
                    _    => false
                };
            }

            // 回退到字符串比较（仅支持 == 和 !=）
            return op switch
            {
                "==" => bbStr == targetValue,
                "!=" => bbStr != targetValue,
                _    => false
            };
        }
    }
}
