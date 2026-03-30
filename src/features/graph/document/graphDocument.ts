export type GraphId = string;
export type NodeId = string;
export type PortId = string;
export type EdgeId = string;

export type PortDirection = "input" | "output";
export type PortCapacity = "single" | "multiple";
export type PortKind = "control" | "event" | "data";

export interface GraphPoint {
  x: number;
  y: number;
}

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
  id: string;
  title: string;
  nodeIds: NodeId[];
}

export interface GraphComment {
  id: string;
  text: string;
}

export interface GraphSubgraph {
  id: string;
  title: string;
  nodeIds: NodeId[];
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
