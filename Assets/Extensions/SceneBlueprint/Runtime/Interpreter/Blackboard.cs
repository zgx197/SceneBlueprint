#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 黑板——蓝图全局变量存储。
    /// <para>
    /// 对齐 FrameSyncEngine 的 Global 概念：
    /// 跨 System 共享的全局数据。后续迁移时映射到 Frame._globals 上。
    /// </para>
    /// <para>
    /// Phase 1 使用 Dictionary 实现；Phase 4（迁移）时改为 Frame 上的确定性存储。
    /// </para>
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> _values = new();

        /// <summary>设置变量值</summary>
        public void Set(string key, object value) => _values[key] = value;

        /// <summary>获取变量值（不存在则返回 default）</summary>
        public T? Get<T>(string key)
        {
            if (_values.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return default;
        }

        /// <summary>尝试获取变量值</summary>
        public bool TryGet<T>(string key, out T? value)
        {
            if (_values.TryGetValue(key, out var val) && val is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>是否包含指定变量</summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>移除变量</summary>
        public bool Remove(string key) => _values.Remove(key);

        /// <summary>清空所有变量</summary>
        public void Clear() => _values.Clear();

        /// <summary>变量数量</summary>
        public int Count => _values.Count;
    }
}
