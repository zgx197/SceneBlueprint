#nullable enable
using NodeGraph.Core;
using SceneBlueprint.SpatialAbstraction;
using SceneBlueprint.SpatialAbstraction.Defaults;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// BindingScope 工具：统一 scopedBindingKey 的生成与兼容处理。
    /// 当前键格式：subGraphId/bindingKey。
    /// </summary>
    internal static class BindingScopeUtility
    {
        public const string TopLevelScopeId = "__toplevel__";
        private static readonly IBindingScopePolicy ScopePolicy = new DefaultBindingScopePolicy();

        public static string BuildScopedKey(string subGraphId, string bindingKey, string nodeId = "")
        {
            string scope = string.IsNullOrEmpty(subGraphId) ? TopLevelScopeId : subGraphId;
            return ScopePolicy.BuildScopedKey(scope, bindingKey, nodeId);
        }

        public static string BuildScopedKeyForNode(Graph? graph, string nodeId, string bindingKey)
        {
            if (graph == null)
                return BuildScopedKey(TopLevelScopeId, bindingKey, nodeId);

            var sgf = graph.FindContainerSubGraphFrame(nodeId);
            return BuildScopedKey(sgf?.Id ?? TopLevelScopeId, bindingKey, nodeId);
        }

        public static bool IsScopedKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.Contains("/");
        }

        public static string NormalizeManagerBindingKey(string storedBindingKey, string subGraphFrameId)
        {
            if (string.IsNullOrEmpty(storedBindingKey))
                return "";

            return IsScopedKey(storedBindingKey)
                ? storedBindingKey
                : BuildScopedKey(subGraphFrameId, storedBindingKey);
        }

        public static string ExtractRawBindingKey(string scopedOrRawKey)
        {
            if (string.IsNullOrEmpty(scopedOrRawKey))
                return "";

            int idx = scopedOrRawKey.IndexOf('/');
            if (idx < 0 || idx + 1 >= scopedOrRawKey.Length)
                return scopedOrRawKey;

            return scopedOrRawKey[(idx + 1)..];
        }
    }
}
