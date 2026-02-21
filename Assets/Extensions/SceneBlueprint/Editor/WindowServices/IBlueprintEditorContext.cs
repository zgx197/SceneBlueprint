#nullable enable
using NodeGraph.View;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 蓝图编辑器窗口向各服务类暴露的最小上下文接口。
    /// <para>
    /// 接口规则（防止 grab-bag 退化）：
    /// 只允许三类成员：
    /// 1. 被动数据访问（ViewModel、CurrentAsset）
    /// 2. 无状态工具注册表（GetActionRegistry）
    /// 3. 窗口副作用入口（RequestRepaint）
    /// </para>
    /// <para>
    /// 禁止放入：具体业务状态（BindingContext、ISceneBindingStore 等）。
    /// 这些状态应由对应的服务类自己持有，而不是通过此接口透传。
    /// </para>
    /// </summary>
    public interface IBlueprintEditorContext
    {
        /// <summary>当前图 ViewModel（服务类需自行判空）</summary>
        GraphViewModel? ViewModel { get; }

        /// <summary>当前蓝图资产（服务类需自行判空）</summary>
        BlueprintAsset? CurrentAsset { get; }

        /// <summary>获取 ActionRegistry（懒加载单例，始终非 null）</summary>
        ActionRegistry GetActionRegistry();

        /// <summary>请求编辑器窗口重绘（对应 EditorWindow.Repaint()）</summary>
        void RequestRepaint();
    }
}
