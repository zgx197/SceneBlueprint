#nullable enable
using UnityEngine;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 实体标记——表示一个 Prefab 实例的放置。
    /// <para>
    /// 典型用途：预设怪物、可交互物体、NPC。
    /// PrefabRef 指向要放置的 Prefab 资产，运行时 Handler 负责实例化。
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Entity Marker")]
    public class EntityMarker : SceneMarker
    {
        public override string MarkerTypeId => MarkerTypeIds.Entity;

        [Header("实体设置")]

        [Tooltip("引用的 Prefab 资产")]
        public GameObject? PrefabRef;

        [Tooltip("放置数量（用于批量刷怪等场景）")]
        [Min(1)]
        public int Count = 1;
    }
}
