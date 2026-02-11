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
    }
}
