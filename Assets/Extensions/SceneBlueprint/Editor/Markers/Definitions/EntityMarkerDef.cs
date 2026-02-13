#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 实体标记定义——Prefab 实例引用。
    /// </summary>
    [MarkerDef(MarkerTypeIds.Entity)]
    public class EntityMarkerDef : IMarkerDefinitionProvider
    {
        public MarkerDefinition Define() => new MarkerDefinition
        {
            TypeId = MarkerTypeIds.Entity,
            DisplayName = "实体标记",
            Description = "Prefab 实例引用（如预设怪物、可交互物体、NPC）",
            ComponentType = typeof(EntityMarker),
            DefaultSpacing = 2f,
        };
    }
}
