#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Core.Export;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图世界状态快照——运行时解释器的核心数据容器。
    /// <para>
    /// 对齐 FrameSyncEngine.Frame 的设计理念：
    /// - Frame 是所有运行时状态的唯一持有者（Single Source of Truth）
    /// - System 通过 Frame 读写状态，自身不持有可变数据
    /// - 静态数据（Actions/Transitions/Properties）加载后不变
    /// - 动态数据（States/Blackboard/Events）每帧由 System 驱动变化
    /// </para>
    /// <para>
    /// 后续迁移路径：
    /// BlueprintFrame 的数据搬到 FrameSyncEngine.Frame 的 Component 上，
    /// 静态数据通过 RuntimeConfig 传入，动态数据通过代码生成的 Component 存储。
    /// </para>
    /// </summary>
    public class BlueprintFrame
    {
        // ══════════════════════════════════════════
        //  静态数据（由 BlueprintLoader 初始化，运行时不变）
        // ══════════════════════════════════════════

        /// <summary>蓝图 ID</summary>
        public string BlueprintId { get; internal set; } = "";

        /// <summary>蓝图名称</summary>
        public string BlueprintName { get; internal set; } = "";

        /// <summary>Action 数量</summary>
        public int ActionCount => Actions.Length;

        /// <summary>
        /// 原始 Action 数据（索引即为 ActionIndex）。
        /// 用于 System 读取节点的 TypeId / Properties / SceneBindings。
        /// </summary>
        public ActionEntry[] Actions { get; internal set; } = Array.Empty<ActionEntry>();

        /// <summary>
        /// 原始 Transition 数据。
        /// TransitionSystem 根据此表进行端口事件路由。
        /// </summary>
        public TransitionEntry[] Transitions { get; internal set; } = Array.Empty<TransitionEntry>();

        // ── 索引表（加速查询）──

        /// <summary>ActionId → ActionIndex 快速查找</summary>
        public Dictionary<string, int> ActionIdToIndex { get; internal set; } = new();

        /// <summary>
        /// 出边索引：ActionIndex → 从该 Action 出发的 Transition 索引列表。
        /// TransitionSystem 用于快速查找"某个 Action 完成后应该触发哪些下游"。
        /// </summary>
        public Dictionary<int, List<int>> OutgoingTransitions { get; internal set; } = new();

        /// <summary>
        /// TypeId → ActionIndex 列表。
        /// 特定 System 用于快速遍历自己需要处理的 Action 子集。
        /// 例如 SpawnPresetSystem 只关心 TypeId == "Spawn.Preset" 的节点。
        /// </summary>
        public Dictionary<string, List<int>> ActionsByTypeId { get; internal set; } = new();

        /// <summary>Flow.Start 节点的 ActionIndex（-1 表示不存在）</summary>
        public int StartActionIndex { get; internal set; } = -1;

        // ══════════════════════════════════════════
        //  动态数据（每帧由 System 读写）
        // ══════════════════════════════════════════

        /// <summary>
        /// 每个 Action 的运行时状态（索引与 Actions 一一对应）。
        /// 对齐 FrameSyncEngine 的 Component 数组。
        /// </summary>
        public ActionRuntimeState[] States { get; internal set; } = Array.Empty<ActionRuntimeState>();

        /// <summary>全局黑板变量</summary>
        public Blackboard Blackboard { get; internal set; } = new();

        /// <summary>
        /// 待处理的端口触发事件队列。
        /// System 产生事件 → 放入此队列 → TransitionSystem 消费并激活下游。
        /// </summary>
        public List<PortEvent> PendingEvents { get; } = new();

        /// <summary>当前已执行的 Tick 数</summary>
        public int TickCount { get; internal set; }

        /// <summary>蓝图是否已执行完毕（所有 Flow.End 已到达，或无活跃 Action）</summary>
        public bool IsCompleted { get; internal set; }

        // ══════════════════════════════════════════
        //  查询辅助方法
        // ══════════════════════════════════════════

        /// <summary>根据 ActionId 获取 ActionIndex（-1 表示未找到）</summary>
        public int GetActionIndex(string actionId)
            => ActionIdToIndex.TryGetValue(actionId, out var idx) ? idx : -1;

        /// <summary>根据 ActionIndex 获取 TypeId</summary>
        public string GetTypeId(int actionIndex)
            => (actionIndex >= 0 && actionIndex < Actions.Length) ? Actions[actionIndex].TypeId : "";

        /// <summary>根据 ActionIndex 获取 Action 的属性值</summary>
        public string GetProperty(int actionIndex, string key)
        {
            if (actionIndex < 0 || actionIndex >= Actions.Length) return "";
            var props = Actions[actionIndex].Properties;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].Key == key) return props[i].Value;
            }
            return "";
        }

        /// <summary>根据 ActionIndex 获取 SceneBindings</summary>
        public SceneBindingEntry[] GetSceneBindings(int actionIndex)
            => (actionIndex >= 0 && actionIndex < Actions.Length)
                ? Actions[actionIndex].SceneBindings
                : Array.Empty<SceneBindingEntry>();

        /// <summary>获取指定 TypeId 的所有 ActionIndex</summary>
        public List<int> GetActionIndices(string typeId)
            => ActionsByTypeId.TryGetValue(typeId, out var list) ? list : _emptyList;

        /// <summary>获取某个 Action 的所有出边 Transition 索引</summary>
        public List<int> GetOutgoingTransitionIndices(int actionIndex)
            => OutgoingTransitions.TryGetValue(actionIndex, out var list) ? list : _emptyList;

        /// <summary>检查是否有任何 Action 处于活跃状态（Running、WaitingTrigger 或 Listening）</summary>
        public bool HasActiveActions()
        {
            for (int i = 0; i < States.Length; i++)
            {
                var phase = States[i].Phase;
                if (phase == ActionPhase.Running ||
                    phase == ActionPhase.WaitingTrigger ||
                    phase == ActionPhase.Listening)
                    return true;
            }
            return false;
        }

        // ── 事件操作 ──

        /// <summary>发射端口事件（Action 完成后，通知 TransitionSystem 路由至下游）</summary>
        public void EmitPortEvent(int fromIndex, string fromPortId, int toIndex, string toPortId)
        {
            PendingEvents.Add(new PortEvent(fromIndex, fromPortId, toIndex, toPortId));
        }

        /// <summary>清空事件队列（每轮 Tick 由 Runner 在消费完毕后调用）</summary>
        public void ClearEvents() => PendingEvents.Clear();

        private static readonly List<int> _emptyList = new();
    }
}
