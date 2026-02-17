#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 节点执行阶段。
    /// <para>
    /// 对齐 FrameSyncEngine 的 Entity 状态模型：
    /// 每个 Action 在 BlueprintFrame 中有一个 Phase，
    /// System 根据 Phase 决定是否处理该 Action。
    /// </para>
    /// </summary>
    public enum ActionPhase
    {
        /// <summary>未激活——尚未被任何 Transition 触发</summary>
        Idle = 0,

        /// <summary>等待触发——已注册监听条件（如 Trigger.EnterArea），等待条件满足</summary>
        WaitingTrigger,

        /// <summary>执行中——System 每帧 Update 处理</summary>
        Running,

        /// <summary>已完成——正常结束，等待 TransitionSystem 传播至下游</summary>
        Completed,

        /// <summary>已失败——异常结束</summary>
        Failed
    }
}
