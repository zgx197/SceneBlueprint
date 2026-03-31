import type {
  GraphEdge,
  GraphNode,
  GraphNodeUiState,
  GraphPoint,
  GraphPort,
  NodeId,
} from "../document/graphDocument";
import type { GraphDocument } from "../document/graphDocument";
import type { GraphSelectionState } from "../state/graphViewState";

export interface GraphClipboardPortSnapshot {
  key: string;
  name: string;
  direction: GraphPort["direction"];
  kind: GraphPort["kind"];
  dataType?: string;
  capacity: GraphPort["capacity"];
}

export interface GraphClipboardNodeSnapshot {
  sourceNodeId: NodeId;
  typeId: string;
  position: GraphPoint;
  payload?: unknown;
  ui?: GraphNodeUiState;
  ports: GraphClipboardPortSnapshot[];
}

export interface GraphClipboardEdgeSnapshot {
  sourceNodeId: NodeId;
  sourcePortKey: string;
  targetNodeId: NodeId;
  targetPortKey: string;
  payload?: unknown;
}

export interface GraphClipboardSnapshot {
  copiedAt: string;
  nodes: GraphClipboardNodeSnapshot[];
  edges: GraphClipboardEdgeSnapshot[];
}

function cloneJson<T>(value: T): T {
  if (value === undefined) {
    return value;
  }

  return JSON.parse(JSON.stringify(value)) as T;
}

function serializePort(port: GraphPort): GraphClipboardPortSnapshot {
  return {
    key: port.key,
    name: port.name,
    direction: port.direction,
    kind: port.kind,
    dataType: port.dataType,
    capacity: port.capacity,
  };
}

function serializeNode(node: GraphNode): GraphClipboardNodeSnapshot {
  return {
    sourceNodeId: node.id,
    typeId: node.typeId,
    position: cloneJson(node.position),
    payload: cloneJson(node.payload),
    ui: cloneJson(node.ui),
    ports: node.ports.map((port) => serializePort(port)),
  };
}

function serializeEdge(edge: GraphEdge, document: GraphDocument): GraphClipboardEdgeSnapshot | null {
  const sourceNode = document.nodes.find((node) => node.id === edge.sourceNodeId);
  const targetNode = document.nodes.find((node) => node.id === edge.targetNodeId);
  const sourcePort = sourceNode?.ports.find((port) => port.id === edge.sourcePortId);
  const targetPort = targetNode?.ports.find((port) => port.id === edge.targetPortId);

  if (!sourcePort || !targetPort) {
    return null;
  }

  return {
    sourceNodeId: edge.sourceNodeId,
    sourcePortKey: sourcePort.key,
    targetNodeId: edge.targetNodeId,
    targetPortKey: targetPort.key,
    payload: cloneJson(edge.payload),
  };
}

export function createGraphClipboardSnapshot(
  document: GraphDocument,
  selection: GraphSelectionState,
): GraphClipboardSnapshot | null {
  const selectedNodeIds = new Set(selection.selectedNodeIds);
  if (selectedNodeIds.size === 0) {
    return null;
  }

  const nodes = document.nodes
    .filter((node) => selectedNodeIds.has(node.id))
    .map((node) => serializeNode(node));
  const edges = document.edges
    .filter((edge) => selectedNodeIds.has(edge.sourceNodeId) && selectedNodeIds.has(edge.targetNodeId))
    .map((edge) => serializeEdge(edge, document))
    .filter((edge): edge is GraphClipboardEdgeSnapshot => edge !== null);

  return {
    copiedAt: new Date().toISOString(),
    nodes,
    edges,
  };
}

export function getGraphClipboardSummary(snapshot: GraphClipboardSnapshot | null) {
  if (!snapshot) {
    return null;
  }

  return {
    nodeCount: snapshot.nodes.length,
    edgeCount: snapshot.edges.length,
    copiedAt: snapshot.copiedAt,
  };
}

