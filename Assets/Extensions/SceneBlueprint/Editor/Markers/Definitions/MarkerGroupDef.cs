#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 标记组定义——一组相关标记的集合。
    /// </summary>
    [MarkerDef(MarkerTypeIds.Group)]
    public class MarkerGroupDef : IMarkerDefinitionProvider
    {
        public MarkerDefinition Define() => new MarkerDefinition
        {
            TypeId = MarkerTypeIds.Group,
            DisplayName = "标记组",
            Description = "一组相关标记的集合（如刷怪点组、巡逻路线、阵型组）",
            ComponentType = typeof(MarkerGroup),
            DefaultSpacing = 3f,
            Initializer = marker =>
            {
                if (marker is MarkerGroup group)
                {
                    group.GroupType = MarkerGroupType.Point;
                    group.ShowConnectionLines = true;
                }
            }
        };
    }
}
