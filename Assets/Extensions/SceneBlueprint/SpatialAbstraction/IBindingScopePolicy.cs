#nullable enable

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 绑定作用域策略接口。
    /// 统一生成导出和恢复链路使用的 scoped key。
    /// </summary>
    public interface IBindingScopePolicy
    {
        string BuildScopedKey(string subGraphId, string bindingKey, string nodeId);
    }
}
