import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import {
  createGraphComment,
  createGraphDocument,
  createGraphGroup,
  createGraphPoint,
  createGraphSubgraph,
  type GraphAnnotationTone,
  type GraphComment,
  type GraphDocument,
  type GraphEdge,
  type GraphGroup,
  type GraphNode,
  type GraphSubgraph,
} from "../document/graphDocument";
import {
  createInitialGraphViewState,
  type GraphConnectionPreviewState,
  type GraphInteractionState,
  type GraphSelectionState,
  type GraphViewportState,
} from "../state/graphViewState";
import { createGraphSelectionManager, type GraphSelectionManager } from "./graphSelectionManager";
import { createGraphSubgraphRuntime, type GraphSubgraphRuntime } from "./graphSubgraphRuntime";

export interface NormalizeGraphWorkspaceRuntimeStateOptions {
  selectionManager?: GraphSelectionManager;
  subgraphRuntime?: GraphSubgraphRuntime;
  resetTransientState?: boolean;
}

function cloneJson<TValue>(value: TValue): TValue {
  return JSON.parse(JSON.stringify(value)) as TValue;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function readString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function readNumber(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function readStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter((entry): entry is string => typeof entry === "string");
}

function readAnnotationTone(value: unknown): GraphAnnotationTone | undefined {
  return value === "neutral" || value === "info" || value === "success" || value === "warning" || value === "danger"
    ? value
    : undefined;
}

function pickPrimaryId(candidate: unknown, selectedIds: string[]): string | undefined {
  return typeof candidate === "string" && selectedIds.includes(candidate) ? candidate : selectedIds[0];
}

function normalizeGraphViewportState(value: unknown): GraphViewportState {
  const initialViewport = createInitialGraphViewState().viewport;
  const viewport = isRecord(value) ? value : {};

  return {
    zoom: readNumber(viewport.zoom) ?? initialViewport.zoom,
    panX: readNumber(viewport.panX) ?? initialViewport.panX,
    panY: readNumber(viewport.panY) ?? initialViewport.panY,
  };
}

export function normalizeGraphSelectionState(value: unknown): GraphSelectionState {
  const selection = isRecord(value) ? value : {};
  const selectedNodeIds = readStringArray(selection.selectedNodeIds);
  const selectedEdgeIds = readStringArray(selection.selectedEdgeIds);
  const selectedGroupIds = readStringArray(selection.selectedGroupIds);
  const selectedCommentIds = readStringArray(selection.selectedCommentIds);
  const selectedSubgraphIds = readStringArray(selection.selectedSubgraphIds);

  return {
    selectedNodeIds,
    selectedEdgeIds,
    selectedGroupIds,
    selectedCommentIds,
    selectedSubgraphIds,
    primarySelectedNodeId: pickPrimaryId(selection.primarySelectedNodeId, selectedNodeIds),
    primarySelectedEdgeId: pickPrimaryId(selection.primarySelectedEdgeId, selectedEdgeIds),
    primarySelectedGroupId: pickPrimaryId(selection.primarySelectedGroupId, selectedGroupIds),
    primarySelectedCommentId: pickPrimaryId(selection.primarySelectedCommentId, selectedCommentIds),
    primarySelectedSubgraphId: pickPrimaryId(selection.primarySelectedSubgraphId, selectedSubgraphIds),
  };
}

function normalizeGraphInteractionState(value: unknown): GraphInteractionState {
  const interaction = isRecord(value) ? value : {};
  const marqueeSelectionRecord = isRecord(interaction.marqueeSelection) ? interaction.marqueeSelection : null;
  const marqueeSelection: GraphInteractionState["marqueeSelection"] =
    marqueeSelectionRecord &&
      readNumber(marqueeSelectionRecord.startX) !== undefined &&
      readNumber(marqueeSelectionRecord.startY) !== undefined &&
      readNumber(marqueeSelectionRecord.endX) !== undefined &&
      readNumber(marqueeSelectionRecord.endY) !== undefined
      ? {
          startX: readNumber(marqueeSelectionRecord.startX)!,
          startY: readNumber(marqueeSelectionRecord.startY)!,
          endX: readNumber(marqueeSelectionRecord.endX)!,
          endY: readNumber(marqueeSelectionRecord.endY)!,
        }
      : undefined;

  return {
    draggingNodeIds: readStringArray(interaction.draggingNodeIds),
    hoveredNodeId: readString(interaction.hoveredNodeId),
    hoveredPortId: readString(interaction.hoveredPortId),
    hoveredGroupId: readString(interaction.hoveredGroupId),
    hoveredCommentId: readString(interaction.hoveredCommentId),
    hoveredSubgraphId: readString(interaction.hoveredSubgraphId),
    marqueeSelection,
  };
}

function normalizeGraphConnectionPreviewState(value: unknown): GraphConnectionPreviewState {
  const preview = isRecord(value) ? value : {};
  const pointerRecord = isRecord(preview.pointer) ? preview.pointer : null;
  const pointer: GraphConnectionPreviewState["pointer"] =
    pointerRecord && readNumber(pointerRecord.x) !== undefined && readNumber(pointerRecord.y) !== undefined
      ? {
          x: readNumber(pointerRecord.x)!,
          y: readNumber(pointerRecord.y)!,
        }
      : undefined;

  return {
    active: preview.active === true,
    fromNodeId: readString(preview.fromNodeId),
    fromPortId: readString(preview.fromPortId),
    pointer,
  };
}

function normalizeGraphGroup(value: unknown, validNodeIds: Set<string>): GraphGroup | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = readString(value.id);
  const title = readString(value.title);
  if (!id || !title) {
    return null;
  }

  const nodeIds = [...new Set(readStringArray(value.nodeIds).filter((nodeId) => validNodeIds.has(nodeId)))];
  if (nodeIds.length === 0) {
    return null;
  }

  return createGraphGroup({
    id,
    title,
    nodeIds,
    color: readString(value.color),
    padding: readNumber(value.padding),
  });
}

function normalizeGraphComment(value: unknown): GraphComment | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = readString(value.id);
  const text = typeof value.text === "string" ? value.text : undefined;
  if (!id || text === undefined) {
    return null;
  }

  const position = isRecord(value.position) ? value.position : {};
  const size = isRecord(value.size) ? value.size : {};

  return createGraphComment({
    id,
    text,
    position: createGraphPoint(
      readNumber(position.x) ?? 0,
      readNumber(position.y) ?? 0,
    ),
    size: {
      width: readNumber(size.width) ?? 240,
      height: readNumber(size.height) ?? 132,
    },
    tone: readAnnotationTone(value.tone) ?? "info",
  });
}

function normalizeGraphSubgraph(value: unknown, validNodeIds: Set<string>): GraphSubgraph | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = readString(value.id);
  const title = readString(value.title);
  if (!id || !title) {
    return null;
  }

  const nodeIds = [...new Set(readStringArray(value.nodeIds).filter((nodeId) => validNodeIds.has(nodeId)))];
  if (nodeIds.length === 0) {
    return null;
  }

  const entryNodeId = readString(value.entryNodeId);

  return createGraphSubgraph({
    id,
    title,
    nodeIds,
    color: readString(value.color),
    entryNodeId: entryNodeId && nodeIds.includes(entryNodeId) ? entryNodeId : nodeIds[0],
    description: typeof value.description === "string" ? value.description : undefined,
  });
}

function normalizeGraphDocument(value: unknown, subgraphRuntime: GraphSubgraphRuntime): GraphDocument {
  const document = isRecord(value) ? value : {};
  const id = readString(document.id) ?? "graph-workspace-recovered";
  const nodes = Array.isArray(document.nodes) ? cloneJson(document.nodes as GraphNode[]) : [];
  const edges = Array.isArray(document.edges) ? cloneJson(document.edges as GraphEdge[]) : [];
  const validNodeIds = new Set(
    nodes
      .map((node) => (isRecord(node) ? readString(node.id) : undefined))
      .filter((nodeId): nodeId is string => nodeId !== undefined),
  );

  const normalizedDocument = createGraphDocument({
    id,
    nodes,
    edges,
    groups: Array.isArray(document.groups)
      ? document.groups
        .map((group) => normalizeGraphGroup(group, validNodeIds))
        .filter((group): group is GraphGroup => group !== null)
      : [],
    comments: Array.isArray(document.comments)
      ? document.comments
        .map((comment) => normalizeGraphComment(comment))
        .filter((comment): comment is GraphComment => comment !== null)
      : [],
    subgraphs: Array.isArray(document.subgraphs)
      ? document.subgraphs
        .map((subgraph) => normalizeGraphSubgraph(subgraph, validNodeIds))
        .filter((subgraph): subgraph is GraphSubgraph => subgraph !== null)
      : [],
    metadata: isRecord(document.metadata) ? cloneJson(document.metadata) : undefined,
  });

  return subgraphRuntime.normalizeDocument(normalizedDocument);
}

export function normalizeGraphWorkspaceRuntimeState(
  state: GraphWorkspaceRuntimeState,
  options: NormalizeGraphWorkspaceRuntimeStateOptions = {},
): GraphWorkspaceRuntimeState {
  const selectionManager = options.selectionManager ?? createGraphSelectionManager();
  const subgraphRuntime = options.subgraphRuntime ?? createGraphSubgraphRuntime();
  const resetTransientState = options.resetTransientState ?? true;
  const initialViewState = createInitialGraphViewState();
  const rawViewState = (isRecord(state.viewState) ? state.viewState : {}) as Record<string, unknown>;
  const document = normalizeGraphDocument(state.document, subgraphRuntime);
  const selection = selectionManager.prune(document, normalizeGraphSelectionState(rawViewState.selection));

  return {
    document,
    viewState: {
      viewport: normalizeGraphViewportState(rawViewState.viewport),
      selection,
      connectionPreview: resetTransientState
        ? initialViewState.connectionPreview
        : normalizeGraphConnectionPreviewState(rawViewState.connectionPreview),
      interaction: resetTransientState
        ? initialViewState.interaction
        : normalizeGraphInteractionState(rawViewState.interaction),
    },
  };
}
