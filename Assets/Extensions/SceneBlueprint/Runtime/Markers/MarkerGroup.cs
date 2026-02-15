#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 标记组类型
    /// </summary>
    public enum MarkerGroupType
    {
        /// <summary>点组——一组点标记（可用于刷怪点、巡逻路线、阵型等）</summary>
        Point,
    }

    /// <summary>
    /// 标记组——管理一组相关的场景标记，作为一个整体参与蓝图绑定。
    /// <para>
    /// 典型用途：
    /// - 刷怪点组：一组刷怪点供 Spawn 节点随机选择
    /// - 巡逻路线：一组点按顺序定义路径
    /// - 阵型组：一组点定义怪物阵型布局
    /// </para>
    /// <para>
    /// 设计原则：
    /// - 标记组本身也是 SceneMarker，拥有 MarkerId 可被蓝图节点引用
    /// - 成员可以是独立的标记对象，也可以是组内的子 Transform
    /// - 成员顺序有意义（如巡逻路线的顺序、阵型的位置关系）
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Marker Group")]
    public class MarkerGroup : SceneMarker
    {
        public override string MarkerTypeId => MarkerTypeIds.Group;

        [Header("组类型")]
        
        [Tooltip("组类型：定义这是什么类型的标记组")]
        public MarkerGroupType GroupType = MarkerGroupType.Point;

        [Header("成员")]
        
        [Tooltip("组成员（Transform 列表）——可以是独立的标记对象或组内子节点")]
        public List<Transform> Members = new();

        [Header("可视化")]
        
        [Tooltip("是否在 Gizmo 中绘制成员连线")]
        public bool ShowConnectionLines = true;
        
        [Tooltip("连线颜色（如果未自定义，使用图层颜色）")]
        public Color ConnectionColor = new Color(0.5f, 0.7f, 1f, 0.6f);

        /// <summary>
        /// 返回组的中心位置作为代表位置。
        /// <para>计算所有成员的重心；如果没有成员，返回 Transform 位置。</para>
        /// </summary>
        public override Vector3 GetRepresentativePosition()
        {
            if (Members.Count == 0)
                return transform.position;

            var center = Vector3.zero;
            foreach (var member in Members)
            {
                if (member != null)
                    center += member.position;
            }
            return center / Members.Count;
        }

        /// <summary>
        /// 获取成员世界坐标列表（过滤掉空引用）
        /// </summary>
        public List<Vector3> GetMemberWorldPositions()
        {
            var positions = new List<Vector3>();
            foreach (var member in Members)
            {
                if (member != null)
                    positions.Add(member.position);
            }
            return positions;
        }

        /// <summary>
        /// 验证成员列表（移除空引用和重复项）
        /// </summary>
        public void ValidateMembers()
        {
            Members.RemoveAll(m => m == null);
            
            // 移除重复项
            var seen = new HashSet<Transform>();
            for (int i = Members.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(Members[i]))
                {
                    Members.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 添加成员到组
        /// </summary>
        public void AddMember(Transform member)
        {
            if (member != null && !Members.Contains(member))
            {
                Members.Add(member);
            }
        }

        /// <summary>
        /// 移除成员
        /// </summary>
        public void RemoveMember(Transform member)
        {
            Members.Remove(member);
        }

        /// <summary>
        /// 清空所有成员
        /// </summary>
        public void ClearMembers()
        {
            Members.Clear();
        }
    }
}
