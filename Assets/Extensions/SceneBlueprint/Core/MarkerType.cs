#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 场景标记类型——决定标记的空间表达形式。
    /// <para>
    /// 用于 <see cref="MarkerRequirement"/> 声明 Action 需要什么类型的标记，
    /// 以及 SceneMarker 组件标识自身类型。
    /// </para>
    /// </summary>
    public enum MarkerType
    {
        /// <summary>单点标记——位置 + 朝向（如刷怪点、摄像机位、VFX 播放点）</summary>
        Point,

        /// <summary>区域标记——多边形或 Box 区域（如触发区、刷怪区、灯光区）</summary>
        Area,

        /// <summary>实体标记——Prefab 实例引用（如预设怪物、可交互物体）</summary>
        Entity
    }
}
