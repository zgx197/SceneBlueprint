#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 单个 Action 节点的运行时状态（值类型，存储在 BlueprintFrame.States 数组中）。
    /// <para>
    /// 对齐 FrameSyncEngine 的 Component 概念：
    /// 纯数据，无逻辑，由 System 读写。
    /// </para>
    /// </summary>
    public struct ActionRuntimeState
    {
        /// <summary>当前执行阶段</summary>
        public ActionPhase Phase;

        /// <summary>在当前阶段已执行的 Tick 次数（用于 Duration 型节点计时）</summary>
        public int TicksInPhase;

        /// <summary>通用整型状态槽（System 可自定义用途，避免装箱）</summary>
        public int CustomInt;

        /// <summary>通用浮点状态槽（System 可自定义用途）</summary>
        public float CustomFloat;

        /// <summary>重置为初始状态</summary>
        public void Reset()
        {
            Phase = ActionPhase.Idle;
            TicksInPhase = 0;
            CustomInt = 0;
            CustomFloat = 0f;
        }
    }
}
