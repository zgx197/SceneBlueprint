#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Diagnostics
{
    /// <summary>
    /// 某一帧结束时的蓝图状态快照。
    /// <para>
    /// 由 <see cref="BlueprintFrameHistory"/> 的对象池管理，外部只读，不要持久持有引用。
    /// </para>
    /// </summary>
    public class BlueprintFrameSnapshot
    {
        /// <summary>对应的 TickCount（与 BlueprintFrame.TickCount 一致）</summary>
        public int TickCount;

        /// <summary>记录时的 UnityEngine.Time.time（秒），用于换算真实时间轴</summary>
        public float Timestamp;

        /// <summary>
        /// ActionRuntimeState 数组的完整值拷贝。
        /// struct 数组 Array.Copy 天然深拷贝，无引用共享问题。
        /// </summary>
        public ActionRuntimeState[] States = Array.Empty<ActionRuntimeState>();

        /// <summary>
        /// 本帧结束时排队等待下帧处理的端口事件快照。
        /// 代表"本帧执行后，哪些下游连线将在下一帧被激活"。
        /// </summary>
        public PortEvent[] PendingEventsSnapshot = Array.Empty<PortEvent>();

        /// <summary>
        /// 与上一帧相比，Phase 发生变化的节点列表。
        /// 空数组表示本帧无任何节点状态迁移。
        /// </summary>
        public StateDiff[] Diffs = Array.Empty<StateDiff>();

        // ── 内部对象池标记 ──
        internal bool InUse;

        /// <summary>
        /// 从 BlueprintFrame 捕获当前状态（复用已分配的数组，尽量不产生 GC）。
        /// </summary>
        internal void CaptureFrom(BlueprintFrame frame)
        {
            TickCount = frame.TickCount;
            Timestamp = Time.time;

            // States：按需扩容，然后值拷贝
            if (States.Length != frame.States.Length)
                States = new ActionRuntimeState[frame.States.Length];
            Array.Copy(frame.States, States, frame.States.Length);

            // PendingEvents：按需扩容，然后逐项拷贝（struct）
            var events = frame.PendingEvents;
            if (PendingEventsSnapshot.Length != events.Count)
                PendingEventsSnapshot = new PortEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
                PendingEventsSnapshot[i] = events[i];
        }
    }
}
