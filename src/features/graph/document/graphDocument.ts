export type GraphId = string;
export type NodeId = string;
export type PortId = string;
export type EdgeId = string;
export type GraphGroupId = string;
export type GraphCommentId = string;
export type GraphSubgraphId = string;

export type PortDirection = "input" | "output";
export type PortCapacity = "single" | "multiple";
export type PortKind = "control" | "event" | "data";
export type GraphAnnotationTone = "neutral" | "info" | "success" | "warning" | "danger";

export interface GraphPoint {
  x: number;
  y: number;
}

export interface GraphSize {
  width: number;
  height: number;
}

export interface GraphBounds extends GraphPoint, GraphSize {}

export interface GraphDocument {
  id: GraphId;
  nodes: GraphNode[];
  edges: GraphEdge[];
  groups: GraphGroup[];
  comments: GraphComment[];
  subgraphs: GraphSubgraph[];
  metadata?: Record<string, unknown>;
}

export interface GraphNode {
  id: NodeId;
  typeId: string;
  position: GraphPoint;
  ports: GraphPort[];
  payload?: unknown;
  ui?: GraphNodeUiState;
}

export interface GraphNodeUiState {
  collapsed?: boolean;
  width?: number;
  height?: number;
}

export interface GraphPort {
  id: PortId;
  key: string;
  name: string;
  direction: PortDirection;
  kind: PortKind;
  dataType?: string;
  capacity: PortCapacity;
}

export interface GraphEdge {
  id: EdgeId;
  sourceNodeId: NodeId;
  sourcePortId: PortId;
  targetNodeId: NodeId;
  targetPortId: PortId;
  payload?: unknown;
}

export interface GraphGroup {
  id: GraphGroupId;
  title: string;
  nodeIds: NodeId[];
  color?: string;
  padding?: number;
}

export interface GraphComment {
  id: GraphCommentId;
  text: string;
  position: GraphPoint;
  size: GraphSize;
  tone?: GraphAnnotationTone;
}

export interface GraphSubgraph {
  id: GraphSubgraphId;
  title: string;
  nodeIds: NodeId[];
  color?: string;
  entryNodeId?: NodeId;
  description?: string;
}

export interface CreateGraphDocumentOptions {
  id: GraphId;
  nodes?: GraphNode[];
  edges?: GraphEdge[];
  groups?: GraphGroup[];
  comments?: GraphComment[];
  subgraphs?: GraphSubgraph[];
  metadata?: Record<string, unknown>;
}

export interface CreateGraphNodeOptions {
  id: NodeId;
  typeId: string;
  position: GraphPoint;
  ports?: GraphPort[];
  payload?: unknown;
  ui?: GraphNodeUiState;
}

export interface CreateGraphPortOptions {
  id: PortId;
  key: string;
  name: string;
  direction: PortDirection;
  kind: PortKind;
  dataType?: string;
  capacity?: PortCapacity;
}

export interface CreateGraphEdgeOptions {
  id: EdgeId;
  sourceNodeId: NodeId;
  sourcePortId: PortId;
  targetNodeId: NodeId;
  targetPortId: PortId;
  payload?: unknown;
}

export interface CreateGraphGroupOptions {
  id: GraphGroupId;
  title: string;
  nodeIds: NodeId[];
  color?: string;
  padding?: number;
}

export interface CreateGraphCommentOptions {
  id: GraphCommentId;
  text: string;
  position: GraphPoint;
  size?: GraphSize;
  tone?: GraphAnnotationTone;
}

export interface CreateGraphSubgraphOptions {
  id: GraphSubgraphId;
  title: string;
  nodeIds: NodeId[];
  color?: string;
  entryNodeId?: NodeId;
  description?: string;
}

export function createGraphPoint(x: number, y: number): GraphPoint {
  return { x, y };
}

export function createGraphPort(options: CreateGraphPortOptions): GraphPort {
  const { capacity = "single", ...rest } = options;
  return {
    ...rest,
    capacity,
  };
}

export function createGraphNode(options: CreateGraphNodeOptions): GraphNode {
  const { ports = [], ...rest } = options;
  return {
    ...rest,
    ports,
  };
}

export function createGraphEdge(options: CreateGraphEdgeOptions): GraphEdge {
  return {
    ...options,
  };
}

export function createGraphGroup(options: CreateGraphGroupOptions): GraphGroup {
  return {
    padding: 28,
    ...options,
  };
}

export function createGraphComment(options: CreateGraphCommentOptions): GraphComment {
  return {
    size: options.size ?? { width: 240, height: 132 },
    tone: options.tone ?? "info",
    ...options,
  };
}

export function createGraphSubgraph(options: CreateGraphSubgraphOptions): GraphSubgraph {
  return {
    color: options.color ?? "rgba(119, 143, 199, 0.18)",
    ...options,
  };
}

export function createGraphDocument(options: CreateGraphDocumentOptions): GraphDocument {
  const {
    nodes = [],
    edges = [],
    groups = [],
    comments = [],
    subgraphs = [],
    metadata,
    id,
  } = options;

  return {
    id,
    nodes,
    edges,
    groups,
    comments,
    subgraphs,
    metadata,
  };
}
