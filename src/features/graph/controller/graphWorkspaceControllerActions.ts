import {
  createGraphComment,
  createGraphGroup,
  createGraphPoint,
  createGraphSubgraph,
  type GraphComment,
  type GraphGroup,
  type GraphPoint,
  type GraphSubgraph,
} from "../document/graphDocument";
import type { GraphClipboardSnapshot } from "../runtime/graphClipboard";
import { createGraphClipboardSnapshot, getGraphClipboardSummary } from "../runtime/graphClipboard";
import type { GraphWorkspaceRuntimeState, GraphWorkspaceCommand } from "../commands/graphCommands";

export const GRAPH_WORKSPACE_PASTE_OFFSET_STEP = 56;

function createScopedId(prefix: string, existingIds: string[]) {
  const numbers = existingIds
    .map((id) => {
      const match = id.match(new RegExp(`^${prefix}-(\\d+)$`));
      return match ? Number.parseInt(match[1], 10) : 0;
    })
    .filter((value) => Number.isFinite(value));

  const nextValue = (numbers.length > 0 ? Math.max(...numbers) : 0) + 1;
  return `${prefix}-${nextValue}`;
}

export function getViewportCenterWorldPoint(
  viewport: GraphWorkspaceRuntimeState["viewState"]["viewport"],
  viewportSize: { width: number; height: number },
): GraphPoint {
  const zoom = viewport.zoom <= 0 ? 1 : viewport.zoom;
  return createGraphPoint(
    (viewportSize.width * 0.5 - viewport.panX) / zoom,
    (viewportSize.height * 0.5 - viewport.panY) / zoom,
  );
}

export function buildCreateGroupFromSelectionCommand(state: GraphWorkspaceRuntimeState): {
  ok: false;
} | {
  ok: true;
  group: GraphGroup;
  command: GraphWorkspaceCommand;
} {
  const nodeIds = state.viewState.selection.selectedNodeIds;
  if (nodeIds.length === 0) {
    return { ok: false };
  }

  const group = createGraphGroup({
    id: createScopedId("group", state.document.groups.map((entry) => entry.id)),
    title: `分组 ${state.document.groups.length + 1}`,
    nodeIds: [...nodeIds],
    color: "rgba(175, 144, 96, 0.16)",
    padding: 36,
  });

  return {
    ok: true,
    group,
    command: {
      type: "graph.add-group",
      group,
    },
  };
}

export function buildCreateSubgraphFromSelectionCommand(state: GraphWorkspaceRuntimeState): {
  ok: false;
} | {
  ok: true;
  subgraph: GraphSubgraph;
  command: GraphWorkspaceCommand;
} {
  const nodeIds = state.viewState.selection.selectedNodeIds;
  if (nodeIds.length === 0) {
    return { ok: false };
  }

  const entryNodeId = state.viewState.selection.primarySelectedNodeId ?? nodeIds[0];
  const subgraph = createGraphSubgraph({
    id: createScopedId("subgraph", state.document.subgraphs.map((entry) => entry.id)),
    title: `子图 ${state.document.subgraphs.length + 1}`,
    nodeIds: [...nodeIds],
    color: "rgba(119, 143, 199, 0.18)",
    entryNodeId,
    description: "待补充子图说明。",
  });

  return {
    ok: true,
    subgraph,
    command: {
      type: "graph.add-subgraph",
      subgraph,
    },
  };
}

export function buildCreateCommentAtViewportCenterCommand(
  state: GraphWorkspaceRuntimeState,
  viewportSize: { width: number; height: number },
): {
  ok: false;
} | {
  ok: true;
  comment: GraphComment;
  command: GraphWorkspaceCommand;
} {
  if (viewportSize.width <= 0 || viewportSize.height <= 0) {
    return { ok: false };
  }

  const center = getViewportCenterWorldPoint(state.viewState.viewport, viewportSize);
  const comment = createGraphComment({
    id: createScopedId("comment", state.document.comments.map((entry) => entry.id)),
    text: "在这里记录当前 Graph 结构的设计说明。",
    position: createGraphPoint(center.x - 120, center.y - 66),
    size: { width: 240, height: 132 },
    tone: "info",
  });

  return {
    ok: true,
    comment,
    command: {
      type: "graph.add-comment",
      comment,
    },
  };
}

export function buildGraphCopySelectionResult(state: GraphWorkspaceRuntimeState): {
  ok: false;
} | {
  ok: true;
  snapshot: GraphClipboardSnapshot;
  summary: ReturnType<typeof getGraphClipboardSummary>;
} {
  const snapshot = createGraphClipboardSnapshot(state.document, state.viewState.selection);
  if (!snapshot) {
    return { ok: false };
  }

  return {
    ok: true,
    snapshot,
    summary: getGraphClipboardSummary(snapshot),
  };
}

export function buildGraphPasteClipboardCommand(
  snapshot: GraphClipboardSnapshot,
  pasteSequence: number,
): GraphWorkspaceCommand {
  return {
    type: "graph.paste-clipboard",
    snapshot,
    offset: {
      x: GRAPH_WORKSPACE_PASTE_OFFSET_STEP * pasteSequence,
      y: GRAPH_WORKSPACE_PASTE_OFFSET_STEP * pasteSequence,
    },
  };
}

export function formatAutoLayoutAppliedMessage(
  providerId: string,
  selectedNodeCount: number,
  totalNodeCount: number,
): string {
  return selectedNodeCount > 0
    ? `已通过 ${providerId} 对 ${selectedNodeCount} 个选中节点执行自动布局。`
    : `已通过 ${providerId} 对全图 ${totalNodeCount} 个节点执行自动布局。`;
}
