#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图 System 基类——处理特定类型 Action 节点的无状态逻辑处理器。
    /// <para>
    /// 对齐 FrameSyncEngine.SystemBase 的设计：
    /// - System 不持有可变状态，所有状态通过 BlueprintFrame 读写
    /// - 生命周期：OnInit → (Update 每帧循环) → OnDisabled
    /// - 每个 System 负责处理一类或多类 TypeId 的 Action
    /// </para>
    /// <para>
    /// 后续迁移路径：
    /// BlueprintSystemBase → FrameSyncEngine.SystemBase，
    /// Update(BlueprintFrame) → Schedule(Frame, TaskHandle)。
    /// </para>
    /// </summary>
    public abstract class BlueprintSystemBase
    {
        /// <summary>System 名称（用于日志和调试）</summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// System 执行优先级（越小越先执行）。
        /// <para>
        /// 推荐约定：
        /// - 0~99：框架级 System（TransitionSystem 等）
        /// - 100~199：业务 System（FlowSystem / SpawnSystem 等）
        /// - 200+：后处理 System
        /// </para>
        /// </summary>
        public virtual int Order => 100;

        /// <summary>是否启用（可动态开关）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 初始化回调——蓝图加载完毕后调用一次。
        /// <para>
        /// 对齐 FrameSyncEngine.SystemBase.OnInit(Frame)。
        /// 用于预处理静态数据、构建内部索引等。
        /// </para>
        /// </summary>
        public virtual void OnInit(BlueprintFrame frame) { }

        /// <summary>
        /// 每帧更新回调——由 BlueprintRunner 在每次 Tick 中按 Order 顺序调用。
        /// <para>
        /// 对齐 FrameSyncEngine.SystemBase.Schedule(Frame, TaskHandle)。
        /// System 在此方法中扫描自己关心的 Action，根据 Phase 执行逻辑。
        /// </para>
        /// </summary>
        public abstract void Update(BlueprintFrame frame);

        /// <summary>
        /// 停用回调——蓝图执行结束或 Runner 销毁时调用。
        /// <para>
        /// 对齐 FrameSyncEngine.SystemBase.OnDisabled(Frame)。
        /// 用于清理临时资源。
        /// </para>
        /// </summary>
        public virtual void OnDisabled(BlueprintFrame frame) { }
    }
}
