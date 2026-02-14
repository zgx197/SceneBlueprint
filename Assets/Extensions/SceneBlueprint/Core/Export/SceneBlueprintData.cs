#nullable enable
using System;

namespace SceneBlueprint.Core.Export
{
    /// <summary>
    /// 蓝图导出数据（顶层）。
    /// 纯数据类，零框架依赖。运行时通过 JSON 反序列化消费此数据。
    /// </summary>
    [Serializable]
    public class SceneBlueprintData
    {
        // ── 元信息 ──
        public string BlueprintId = "";
        public string BlueprintName = "";
        public int Version = 1;
        public string ExportTime = "";

        // ── 核心数据 ──
        public ActionEntry[] Actions = Array.Empty<ActionEntry>();
        public TransitionEntry[] Transitions = Array.Empty<TransitionEntry>();
        public VariableEntry[] BlackboardInit = Array.Empty<VariableEntry>();
    }

    /// <summary>行动条目（对应图中的一个节点）</summary>
    [Serializable]
    public class ActionEntry
    {
        public string Id = "";
        public string TypeId = "";
        public PropertyValue[] Properties = Array.Empty<PropertyValue>();
        public SceneBindingEntry[] SceneBindings = Array.Empty<SceneBindingEntry>();
    }

    /// <summary>属性值（扁平化键值对，字符串序列化）</summary>
    [Serializable]
    public class PropertyValue
    {
        public string Key = "";
        public string ValueType = "";
        public string Value = "";
    }

    /// <summary>过渡条目（对应图中的一条连线）</summary>
    [Serializable]
    public class TransitionEntry
    {
        public string FromActionId = "";
        public string FromPortId = "";
        public string ToActionId = "";
        public string ToPortId = "";
        public ConditionData Condition = new ConditionData();
    }

    /// <summary>
    /// 条件数据（可嵌套组合）。
    /// Type: "Immediate" | "Delay" | "Expression" | "Tag" | "Event" | "AllOf" | "AnyOf"
    /// </summary>
    [Serializable]
    public class ConditionData
    {
        public string Type = "Immediate";
        public string Expression = "";
        public ConditionData[] Children = Array.Empty<ConditionData>();
    }

    /// <summary>场景绑定条目</summary>
    [Serializable]
    public class SceneBindingEntry
    {
        public string BindingKey = "";
        public string BindingType = "";
        /// <summary>
        /// 绑定对象标识（V2 统一语义）：与 <see cref="StableObjectId"/> 保持一致。
        /// </summary>
        public string SceneObjectId = "";

        /// <summary>V2 字段：稳定对象 ID（抗重命名）。</summary>
        public string StableObjectId = "";

        /// <summary>V2 字段：导出时使用的空间适配器类型（如 Unity3D / Unity2D）。</summary>
        public string AdapterType = "";

        /// <summary>V2 字段：空间扩展载荷（JSON 字符串，C2 先占位）。</summary>
        public string SpatialPayloadJson = "";

        public string SourceSubGraph = "";
        public string SourceActionTypeId = "";
    }

    /// <summary>变量条目（黑板初始值，Phase 2+ 预留）</summary>
    [Serializable]
    public class VariableEntry
    {
        public string Key = "";
        public string ValueType = "";
        public string InitialValue = "";
    }
}
