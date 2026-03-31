import type { GraphNodeDefinition } from "../definitions/graphDefinitions";
import type { GraphNodeSearchResult } from "../services/graphNodeSearchService";

export type GraphCommandPaletteActionId =
  | "palette.focus-search"
  | "palette.select-all"
  | "palette.copy-selection"
  | "palette.paste-clipboard"
  | "palette.auto-layout"
  | "palette.reset-viewport"
  | "palette.save-draft"
  | "palette.load-draft"
  | "palette.save-workspace-file"
  | "palette.export-runtime-contract"
  | "palette.load-workspace-file"
  | "palette.reset-bootstrap";

export type GraphCommandPaletteItem =
  | {
      id: string;
      kind: "action";
      label: string;
      description: string;
      badge?: string;
      keywords: string[];
      actionId: GraphCommandPaletteActionId;
    }
  | {
      id: string;
      kind: "focus-node";
      label: string;
      description: string;
      badge?: string;
      keywords: string[];
      nodeId: string;
    }
  | {
      id: string;
      kind: "create-node";
      label: string;
      description: string;
      badge?: string;
      keywords: string[];
      nodeTypeId: string;
    };

export interface GraphCommandPaletteGroup {
  id: string;
  title: string;
  items: GraphCommandPaletteItem[];
}

export interface GraphCommandPaletteModel {
  groups: GraphCommandPaletteGroup[];
  emptyLabel: string;
}

interface BuildGraphCommandPaletteModelOptions {
  definitions: GraphNodeDefinition[];
  nodeResults: GraphNodeSearchResult[];
  selectedNodeCount: number;
  selectedEdgeCount: number;
  clipboardLabel: string;
  searchQuery: string;
}

function createActionItems(options: BuildGraphCommandPaletteModelOptions): GraphCommandPaletteItem[] {
  const { selectedNodeCount, selectedEdgeCount, clipboardLabel } = options;

  return [
    {
      id: "palette-action-focus-search",
      kind: "action",
      actionId: "palette.focus-search",
      label: "聚焦节点搜索",
      description: "把焦点切到右上角节点搜索框。",
      badge: "Mod+F",
      keywords: ["find", "search", "node", "focus"],
    },
    {
      id: "palette-action-select-all",
      kind: "action",
      actionId: "palette.select-all",
      label: "全选节点",
      description: `当前会选择整张图中的全部节点，已选节点 ${selectedNodeCount} 个。`,
      badge: "Mod+A",
      keywords: ["select", "all", "nodes"],
    },
    {
      id: "palette-action-copy",
      kind: "action",
      actionId: "palette.copy-selection",
      label: "复制当前选择",
      description: `复制当前节点选择，当前已选节点 ${selectedNodeCount} 个，边 ${selectedEdgeCount} 条。`,
      badge: "Mod+C",
      keywords: ["copy", "clipboard", "selection"],
    },
    {
      id: "palette-action-paste",
      kind: "action",
      actionId: "palette.paste-clipboard",
      label: "粘贴剪贴板",
      description: `按当前偏移粘贴 Graph 剪贴板内容，当前摘要 ${clipboardLabel}。`,
      badge: "Mod+V",
      keywords: ["paste", "clipboard"],
    },
    {
      id: "palette-action-layout",
      kind: "action",
      actionId: "palette.auto-layout",
      label: "自动布局",
      description: "优先使用 ELK.js，对当前选择或整张图执行自动布局。",
      badge: "Mod+Shift+L",
      keywords: ["layout", "arrange", "elk", "dagre"],
    },
    {
      id: "palette-action-reset-viewport",
      kind: "action",
      actionId: "palette.reset-viewport",
      label: "重置视口",
      description: "恢复到 1x 缩放和原点平移。",
      keywords: ["viewport", "reset", "camera"],
    },
    {
      id: "palette-action-save-draft",
      kind: "action",
      actionId: "palette.save-draft",
      label: "保存本地草稿",
      description: "把当前 Graph Workspace 状态保存到本地草稿存储。",
      keywords: ["draft", "save", "local"],
    },
    {
      id: "palette-action-load-draft",
      kind: "action",
      actionId: "palette.load-draft",
      label: "恢复本地草稿",
      description: "从本地草稿恢复上一次保存的 Graph Workspace 状态。",
      keywords: ["draft", "load", "restore", "local"],
    },
    {
      id: "palette-action-save-file",
      kind: "action",
      actionId: "palette.save-workspace-file",
      label: "保存正式 Graph 文件",
      description: "把当前工作区写回正式 Graph 文件。",
      keywords: ["save", "file", "workspace"],
    },
    {
      id: "palette-action-export-runtime-contract",
      kind: "action",
      actionId: "palette.export-runtime-contract",
      label: "导出 Runtime Contract",
      description: "执行 validate -> contract -> export 主链路，并输出 runtime contract 文件。",
      keywords: ["export", "runtime", "contract", "validate", "compile"],
    },
    {
      id: "palette-action-load-file",
      kind: "action",
      actionId: "palette.load-workspace-file",
      label: "重新加载正式 Graph 文件",
      description: "重新读取当前工作区绑定的正式 Graph 文件。",
      keywords: ["load", "reload", "file", "workspace"],
    },
    {
      id: "palette-action-reset-bootstrap",
      kind: "action",
      actionId: "palette.reset-bootstrap",
      label: "重置为 Bootstrap 图",
      description: "清空当前草稿并恢复到初始 Graph Workspace 骨架。",
      keywords: ["reset", "bootstrap", "default"],
    },
  ];
}

export function buildGraphCommandPaletteModel(
  options: BuildGraphCommandPaletteModelOptions,
): GraphCommandPaletteModel {
  const groups: GraphCommandPaletteGroup[] = [];
  const actionItems = createActionItems(options);

  groups.push({
    id: "palette-actions",
    title: "Workspace Actions",
    items: actionItems,
  });

  if (options.searchQuery.trim()) {
    groups.push({
      id: "palette-matching-nodes",
      title: "Matching Nodes",
      items: options.nodeResults.map((node) => ({
        id: `palette-focus-node-${node.nodeId}`,
        kind: "focus-node",
        nodeId: node.nodeId,
        label: node.title,
        description: node.summaryText ?? node.description ?? node.typeId,
        badge: node.category,
        keywords: [node.nodeId, node.typeId, node.title, node.category ?? "", node.summaryText ?? ""].filter(Boolean),
      })),
    });
  }

  const definitionGroups = new Map<string, GraphNodeDefinition[]>();
  options.definitions.forEach((definition) => {
    const category = definition.category ?? "Uncategorized";
    const bucket = definitionGroups.get(category) ?? [];
    bucket.push(definition);
    definitionGroups.set(category, bucket);
  });

  [...definitionGroups.entries()]
    .sort((left, right) => left[0].localeCompare(right[0]))
    .forEach(([category, definitions]) => {
      groups.push({
        id: `palette-create-${category}`,
        title: `Create · ${category}`,
        items: [...definitions]
          .sort((left, right) => left.displayName.localeCompare(right.displayName))
          .map((definition) => ({
            id: `palette-create-node-${definition.typeId}`,
            kind: "create-node",
            nodeTypeId: definition.typeId,
            label: definition.displayName,
            description: definition.description ?? definition.typeId,
            badge: definition.typeId,
            keywords: [
              definition.typeId,
              definition.displayName,
              definition.category ?? "",
              definition.description ?? "",
            ].filter(Boolean),
          })),
      });
    });

  return {
    groups,
    emptyLabel: options.searchQuery.trim() ? "没有匹配的命令或节点。" : "输入关键字以搜索命令、节点或可创建节点。",
  };
}
