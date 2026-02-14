#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Action 场景需求声明——描述一个 Action 需要什么类型的场景标记。
    /// <para>
    /// 放在 <see cref="ActionDefinition.SceneRequirements"/> 中，驱动：
    /// <list type="bullet">
    ///   <item>Scene View 右键菜单：根据 Action 的需求自动创建对应标记</item>
    ///   <item>Inspector 绑定 UI：自动生成标记绑定字段</item>
    ///   <item>验证逻辑：检查必需标记是否已绑定</item>
    ///   <item>多步创建流程：按声明顺序引导设计师逐步放置标记</item>
    /// </list>
    /// </para>
    /// <para>
    /// 标记的创建参数（显示名称、颜色、尺寸等）由 <c>MarkerPresetSO</c> 统一管理。
    /// 通过 <see cref="PresetId"/> 引用预设，实现"声明与创建配置分离"。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Spawn Action 声明需要一个区域和多个点位
    /// SceneRequirements = new[]
    /// {
    ///     new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
    ///         presetId: "Combat.SpawnArea", required: true),
    ///     new MarkerRequirement("spawnPoints", MarkerTypeIds.Point,
    ///         presetId: "Combat.SpawnPoint", allowMultiple: true, minCount: 1),
    /// };
    /// </code>
    /// </example>
    public class MarkerRequirement
    {
        /// <summary>
        /// 绑定键名——与 <see cref="PropertyDefinition.Key"/> 类似，作为绑定映射的 key。
        /// <para>如 "spawnArea", "spawnPoints", "cameraPosition"</para>
        /// </summary>
        public string BindingKey { get; set; } = "";

        /// <summary>
        /// 需要的标记类型 ID——对应 <see cref="MarkerTypeIds"/> 中定义的常量，
        /// 也可以是自定义的字符串 ID。
        /// <para>如 "Point", "Area", "Entity", "Path"</para>
        /// </summary>
        public string MarkerTypeId { get; set; } = "";

        /// <summary>
        /// 引用的标记预设 ID——对应 <c>MarkerPresetSO.PresetId</c>。
        /// <para>
        /// 创建标记时从预设读取显示名称、颜色、尺寸等配置。
        /// 为空时回退到基础标记类型的默认行为。
        /// </para>
        /// </summary>
        public string PresetId { get; set; } = "";

        /// <summary>
        /// 是否必需——未绑定时显示警告，阻止导出
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 是否允许绑定多个标记——如多个刷怪点
        /// </summary>
        public bool AllowMultiple { get; set; }

        /// <summary>
        /// 最少数量——当 <see cref="AllowMultiple"/> 为 true 时有效
        /// </summary>
        public int MinCount { get; set; }

        // ── 向后兼容字段（PresetId 为空时回退使用） ──

        /// <summary>
        /// [向后兼容] 显示名称——优先使用 PresetSO.DisplayName，无预设时回退到此字段。
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// [向后兼容] 默认 Tag——优先使用 PresetSO.DefaultTag，无预设时回退到此字段。
        /// </summary>
        public string DefaultTag { get; set; } = "";

        /// <summary>无参构造函数（序列化需要）</summary>
        public MarkerRequirement() { }

        /// <summary>推荐构造函数——使用 PresetId 引用预设</summary>
        public MarkerRequirement(
            string bindingKey,
            string markerTypeId,
            string presetId = "",
            bool required = false,
            bool allowMultiple = false,
            int minCount = 0)
        {
            BindingKey = bindingKey;
            MarkerTypeId = markerTypeId;
            PresetId = presetId;
            Required = required;
            AllowMultiple = allowMultiple;
            MinCount = minCount;
        }

        /// <summary>[向后兼容] 旧构造函数——displayName 和 defaultTag 直接嵌入</summary>
        public MarkerRequirement(
            string bindingKey,
            string markerTypeId,
            string displayName,
            bool required = false,
            bool allowMultiple = false,
            int minCount = 0,
            string defaultTag = "")
        {
            BindingKey = bindingKey;
            MarkerTypeId = markerTypeId;
            DisplayName = displayName;
            Required = required;
            AllowMultiple = allowMultiple;
            MinCount = minCount;
            DefaultTag = defaultTag;
        }
    }
}
