import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type {
  GraphAnnotationTone,
  GraphComment,
  GraphDocument,
  GraphEdge,
  GraphGroup,
  GraphNode,
  GraphNodeUiState,
  GraphPort,
  GraphSubgraph,
} from "../document/graphDocument";
import { createInitialGraphViewState, type GraphSelectionState, type GraphViewportState } from "../state/graphViewState";
import { normalizeGraphWorkspaceRuntimeState } from "../runtime/graphRuntimeStateMigration";

export const GRAPH_DOCUMENT_FILE_SCHEMA = "sceneblueprint.graph-document.v1" as const;

export interface GraphDocumentWorkspaceSnapshot {
  viewport: GraphViewportState;
  selection: GraphSelectionState;
}

export interface GraphDocumentFileEnvelope {
  schema: typeof GRAPH_DOCUMENT_FILE_SCHEMA;
  savedAt: string;
  graph: GraphDocument;
  workspace: GraphDocumentWorkspaceSnapshot;
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function readString(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

function readNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function readStringArray(value: unknown): string[] | null {
  if (!Array.isArray(value)) {
    return null;
  }

  const items = value.filter((entry): entry is string => typeof entry === "string");
  return items.length === value.length ? items : null;
}

function readAnnotationTone(value: unknown): GraphAnnotationTone | undefined {
  return value === "neutral" || value === "info" || value === "success" || value === "warning" || value === "danger"
    ? value
    : undefined;
}

function sanitizeNodeUi(value: unknown): GraphNodeUiState | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const width = readNumber(value.width);
  const height = readNumber(value.height);
  const collapsed = typeof value.collapsed === "boolean" ? value.collapsed : undefined;

  if (width === null && height === null && collapsed === undefined) {
    return undefined;
  }

  return {
    width: width ?? undefined,
    height: height ?? undefined,
    collapsed,
  };
}

function sanitizeGraphPort(value: unknown): GraphPort {
  if (!isRecord(value)) {
    throw new Error("Graph 端口格式无效。");
  }

  const id = readString(value.id);
  const key = readString(value.key);
  const name = readString(value.name);
  const direction = readString(value.direction);
  const kind = readString(value.kind);
  const dataType = readString(value.dataType);
  const capacity = readString(value.capacity);

  if (!id || !key || !name) {
    throw new Error("Graph 端口缺少必要字段。");
  }

  if (direction !== "input" && direction !== "output") {
    throw new Error(`Graph 端口方向无效：${String(direction)}`);
  }

  if (kind !== "control" && kind !== "event" && kind !== "data") {
    throw new Error(`Graph 端口类型无效：${String(kind)}`);
  }

  if (capacity !== "single" && capacity !== "multiple") {
    throw new Error(`Graph 端口容量无效：${String(capacity)}`);
  }

  return {
    id,
    key,
    name,
    direction,
    kind,
    dataType: dataType ?? undefined,
    capacity,
  };
}

function sanitizeGraphNode(value: unknown): GraphNode {
  if (!isRecord(value)) {
    throw new Error("Graph 节点格式无效。");
  }

  const id = readString(value.id);
  const typeId = readString(value.typeId);
  const position = isRecord(value.position)
    ? {
        x: readNumber(value.position.x),
        y: readNumber(value.position.y),
      }
    : null;

  if (!id || !typeId || !position || position.x === null || position.y === null) {
    throw new Error("Graph 节点缺少必要字段。");
  }

  const rawPorts = Array.isArray(value.ports) ? value.ports : [];

  return {
    id,
    typeId,
    position: {
      x: position.x,
      y: position.y,
    },
    ports: rawPorts.map((port) => sanitizeGraphPort(port)),
    payload: cloneJson(value.payload),
    ui: sanitizeNodeUi(value.ui),
  };
}

function sanitizeGraphEdge(value: unknown): GraphEdge {
  if (!isRecord(value)) {
    throw new Error("Graph 连线格式无效。");
  }

  const id = readString(value.id);
  const sourceNodeId = readString(value.sourceNodeId);
  const sourcePortId = readString(value.sourcePortId);
  const targetNodeId = readString(value.targetNodeId);
  const targetPortId = readString(value.targetPortId);

  if (!id || !sourceNodeId || !sourcePortId || !targetNodeId || !targetPortId) {
    throw new Error("Graph 连线缺少必要字段。");
  }

  return {
    id,
    sourceNodeId,
    sourcePortId,
    targetNodeId,
    targetPortId,
    payload: cloneJson(value.payload),
  };
}

function sanitizeGraphGroup(value: unknown): GraphGroup {
  if (!isRecord(value)) {
    throw new Error("Graph 分组格式无效。");
  }

  const id = readString(value.id);
  const title = readString(value.title);
  const nodeIds = readStringArray(value.nodeIds);
  const color = readString(value.color) ?? undefined;
  const padding = readNumber(value.padding) ?? undefined;

  if (!id || !title || !nodeIds) {
    throw new Error("Graph 分组缺少必要字段。");
  }

  return {
    id,
    title,
    nodeIds,
    color,
    padding,
  };
}

function sanitizeGraphComment(value: unknown): GraphComment {
  if (!isRecord(value)) {
    throw new Error("Graph 注释格式无效。");
  }

  const id = readString(value.id);
  const text = readString(value.text);
  const positionRecord = isRecord(value.position) ? value.position : {};
  const sizeRecord = isRecord(value.size) ? value.size : {};

  if (!id || text === null) {
    throw new Error("Graph 注释缺少必要字段。");
  }

  return {
    id,
    text,
    position: {
      x: readNumber(positionRecord.x) ?? 0,
      y: readNumber(positionRecord.y) ?? 0,
    },
    size: {
      width: readNumber(sizeRecord.width) ?? 240,
      height: readNumber(sizeRecord.height) ?? 132,
    },
    tone: readAnnotationTone(value.tone) ?? "info",
  };
}

function sanitizeGraphSubgraph(value: unknown): GraphSubgraph {
  if (!isRecord(value)) {
    throw new Error("Graph 子图格式无效。");
  }

  const id = readString(value.id);
  const title = readString(value.title);
  const nodeIds = readStringArray(value.nodeIds);
  const color = readString(value.color) ?? undefined;
  const entryNodeId = readString(value.entryNodeId) ?? undefined;
  const description = readString(value.description) ?? undefined;

  if (!id || !title || !nodeIds) {
    throw new Error("Graph 子图缺少必要字段。");
  }

  return {
    id,
    title,
    nodeIds,
    color,
    entryNodeId,
    description,
  };
}

function sanitizeGraphDocument(value: unknown): GraphDocument {
  if (!isRecord(value)) {
    throw new Error("Graph 文档格式无效。");
  }

  const id = readString(value.id);
  if (!id) {
    throw new Error("Graph 文档缺少 id 字段。");
  }

  const nodes = Array.isArray(value.nodes) ? value.nodes.map((node) => sanitizeGraphNode(node)) : [];
  const edges = Array.isArray(value.edges) ? value.edges.map((edge) => sanitizeGraphEdge(edge)) : [];
  const groups = Array.isArray(value.groups) ? value.groups.map((group) => sanitizeGraphGroup(group)) : [];
  const comments = Array.isArray(value.comments)
    ? value.comments.map((comment) => sanitizeGraphComment(comment))
    : [];
  const subgraphs = Array.isArray(value.subgraphs)
    ? value.subgraphs.map((subgraph) => sanitizeGraphSubgraph(subgraph))
    : [];

  return {
    id,
    nodes,
    edges,
    groups,
    comments,
    subgraphs,
    metadata: isRecord(value.metadata) ? cloneJson(value.metadata) : undefined,
  };
}

function sanitizeWorkspaceSnapshot(value: unknown): GraphDocumentWorkspaceSnapshot {
  const initialState = createInitialGraphViewState();
  if (!isRecord(value)) {
    return {
      viewport: initialState.viewport,
      selection: initialState.selection,
    };
  }

  const viewportRecord = isRecord(value.viewport) ? value.viewport : {};
  const selectionRecord = isRecord(value.selection) ? value.selection : {};
  const viewport = {
    zoom: readNumber(viewportRecord.zoom) ?? initialState.viewport.zoom,
    panX: readNumber(viewportRecord.panX) ?? initialState.viewport.panX,
    panY: readNumber(viewportRecord.panY) ?? initialState.viewport.panY,
  };
  const selectedNodeIds = readStringArray(selectionRecord.selectedNodeIds) ?? [];
  const selectedEdgeIds = readStringArray(selectionRecord.selectedEdgeIds) ?? [];
  const selectedGroupIds = readStringArray(selectionRecord.selectedGroupIds) ?? [];
  const selectedCommentIds = readStringArray(selectionRecord.selectedCommentIds) ?? [];
  const selectedSubgraphIds = readStringArray(selectionRecord.selectedSubgraphIds) ?? [];
  const primarySelectedNodeId = readString(selectionRecord.primarySelectedNodeId);
  const primarySelectedEdgeId = readString(selectionRecord.primarySelectedEdgeId);
  const primarySelectedGroupId = readString(selectionRecord.primarySelectedGroupId);
  const primarySelectedCommentId = readString(selectionRecord.primarySelectedCommentId);
  const primarySelectedSubgraphId = readString(selectionRecord.primarySelectedSubgraphId);

  return {
    viewport,
    selection: {
      selectedNodeIds,
      selectedEdgeIds,
      selectedGroupIds,
      selectedCommentIds,
      selectedSubgraphIds,
      primarySelectedNodeId:
        primarySelectedNodeId && selectedNodeIds.includes(primarySelectedNodeId)
          ? primarySelectedNodeId
          : selectedNodeIds[0],
      primarySelectedEdgeId:
        primarySelectedEdgeId && selectedEdgeIds.includes(primarySelectedEdgeId)
          ? primarySelectedEdgeId
          : selectedEdgeIds[0],
      primarySelectedGroupId:
        primarySelectedGroupId && selectedGroupIds.includes(primarySelectedGroupId)
          ? primarySelectedGroupId
          : selectedGroupIds[0],
      primarySelectedCommentId:
        primarySelectedCommentId && selectedCommentIds.includes(primarySelectedCommentId)
          ? primarySelectedCommentId
          : selectedCommentIds[0],
      primarySelectedSubgraphId:
        primarySelectedSubgraphId && selectedSubgraphIds.includes(primarySelectedSubgraphId)
          ? primarySelectedSubgraphId
          : selectedSubgraphIds[0],
    },
  };
}

export function createGraphDocumentFileEnvelope(
  runtimeState: GraphWorkspaceRuntimeState,
  savedAt = new Date().toISOString(),
): GraphDocumentFileEnvelope {
  return {
    schema: GRAPH_DOCUMENT_FILE_SCHEMA,
    savedAt,
    graph: cloneJson(runtimeState.document),
    workspace: {
      viewport: cloneJson(runtimeState.viewState.viewport),
      selection: cloneJson(runtimeState.viewState.selection),
    },
  };
}

export function serializeGraphDocumentFile(runtimeState: GraphWorkspaceRuntimeState): string {
  return JSON.stringify(createGraphDocumentFileEnvelope(runtimeState), null, 2);
}

export function parseGraphDocumentFileEnvelope(raw: string): GraphDocumentFileEnvelope {
  let parsed: unknown;

  try {
    parsed = JSON.parse(raw) as unknown;
  } catch {
    throw new Error("Graph 文件不是有效的 JSON 文本。");
  }

  if (!isRecord(parsed)) {
    throw new Error("Graph 文件根对象格式无效。");
  }

  if (parsed.schema !== GRAPH_DOCUMENT_FILE_SCHEMA) {
    throw new Error(`当前不支持的 Graph 文件 schema：${String(parsed.schema)}`);
  }

  const savedAt = readString(parsed.savedAt) ?? new Date().toISOString();

  return {
    schema: GRAPH_DOCUMENT_FILE_SCHEMA,
    savedAt,
    graph: sanitizeGraphDocument(parsed.graph),
    workspace: sanitizeWorkspaceSnapshot(parsed.workspace),
  };
}

export function deserializeGraphDocumentFile(raw: string): GraphWorkspaceRuntimeState {
  const envelope = parseGraphDocumentFileEnvelope(raw);

  return normalizeGraphWorkspaceRuntimeState({
    document: envelope.graph,
    viewState: createInitialGraphViewState({
      viewport: envelope.workspace.viewport,
      selection: envelope.workspace.selection,
    }),
  });
}

