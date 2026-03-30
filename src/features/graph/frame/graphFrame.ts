import type { GraphPoint, PortDirection, PortId, PortKind } from "../document/graphDocument";
import type { GraphViewportState } from "../state/graphViewState";

export interface GraphFrameBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface GraphFramePort {
  id: PortId;
  key: string;
  name: string;
  direction: PortDirection;
  kind: PortKind;
  dataType?: string;
  /**
   * 端口锚点是当前节点 UI 中“可见端点圆心”的世界坐标。
   * 所有边、预览线、命中测试都必须以这个点为准，不能改成节点外框或标签位置。
   */
  anchor: GraphPoint;
  connectedEdgeCount: number;
  connected: boolean;
  hovered: boolean;
  connectable: boolean;
  source: boolean;
}

export interface GraphFramePortRow {
  input?: GraphFramePort;
  output?: GraphFramePort;
}

export interface GraphFrameNode {
  id: string;
  title: string;
  typeId: string;
  category?: string;
  summary?: string;
  bounds: GraphFrameBounds;
  selected: boolean;
  hovered: boolean;
  rows: GraphFramePortRow[];
  inputs: GraphFramePort[];
  outputs: GraphFramePort[];
}

export interface GraphFrameEdge {
  id: string;
  sourceNodeId: string;
  sourcePortId: string;
  targetNodeId: string;
  targetPortId: string;
  /**
   * start / end 不是缓存用的随意坐标，它们必须严格对应端口锚点中心。
   * GraphCanvas 中所有可见边都应当能追溯到某个 sourcePortId / targetPortId。
   */
  start: GraphPoint;
  end: GraphPoint;
  path: string;
  midpoint: GraphPoint;
  selected: boolean;
}

export interface GraphFrameOverlay {
  kind: "connection-preview";
  /**
   * 预览线也必须从真实 sourcePortId 的锚点中心出发。
   * 之所以显式保留 sourcePortId / start / end，是为了在拖拽预览阶段重算路径时
   * 仍然能够绑定到正确端口，避免回退成“看起来差不多”的错误起点。
   */
  sourceNodeId: string;
  sourcePortId: string;
  start: GraphPoint;
  end: GraphPoint;
  path: string;
}

export interface GraphFrameSummary {
  nodeCount: number;
  edgeCount: number;
  hasActiveConnectionPreview: boolean;
  activeOutputPortId?: PortId;
}

export interface GraphFrame {
  viewport: GraphViewportState;
  nodes: GraphFrameNode[];
  edges: GraphFrameEdge[];
  overlays: GraphFrameOverlay[];
  summary: GraphFrameSummary;
}
