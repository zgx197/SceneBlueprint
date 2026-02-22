#nullable enable
using System.Collections.Generic;

namespace NodeGraph.Core
{
    /// <summary>
    /// 节点类型目录接口——在 <see cref="INodeTypeProvider"/>（按 TypeId 查询）基础上，
    /// 额外支持枚举和关键字搜索，供搜索菜单等 UI 使用。
    /// <para>
    /// 实现者：<see cref="NodeTypeRegistry"/>（NodeGraph 内建）或
    /// SceneBlueprint 侧的 ActionRegistryNodeTypeCatalog（Action 体系适配）。
    /// </para>
    /// </summary>
    public interface INodeTypeCatalog : INodeTypeProvider
    {
        /// <summary>返回全部已注册的节点类型定义。</summary>
        IEnumerable<NodeTypeDefinition> GetAll();

        /// <summary>按关键字搜索节点类型（匹配 TypeId、DisplayName 或 Category）。</summary>
        IEnumerable<NodeTypeDefinition> Search(string keyword);

        /// <summary>获取所有分类路径（去重）。</summary>
        IEnumerable<string> GetCategories();
    }
}
