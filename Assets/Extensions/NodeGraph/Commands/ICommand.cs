#nullable enable
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 命令接口。所有可撤销的操作都实现此接口。
    /// Execute 和 Undo 必须互为逆操作，且对同一个 Graph 实例操作。
    /// </summary>
    public interface ICommand
    {
        /// <summary>命令描述（用于 UI 显示，如"添加节点 SpawnTask"）</summary>
        string Description { get; }

        /// <summary>执行命令</summary>
        void Execute(Graph graph);

        /// <summary>撤销命令</summary>
        void Undo(Graph graph);

        /// <summary>
        /// 尝试与撤销栈顶的 <paramref name="previous"/> 命令合并。
        /// <para>
        /// 合并成功时 <paramref name="previous"/> 的状态已被修改（纳入了本命令的增量），
        /// 调用方丢弃 <c>this</c> 不入栈。默认返回 <c>false</c>（不合并）。
        /// </para>
        /// </summary>
        bool TryMergeWith(ICommand previous) => false;
    }
}
