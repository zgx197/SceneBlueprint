import type { EdgeId, NodeId, PortId } from "../document/graphDocument";

export interface GraphViewportState {
  zoom: number;
  panX: number;
  panY: number;
}

export interface GraphSelectionState {
  selectedNodeIds: NodeId[];
  selectedEdgeIds: EdgeId[];
}

export interface GraphConnectionPreviewState {
  active: boolean;
  fromNodeId?: NodeId;
  fromPortId?: PortId;
  pointer?: { x: number; y: number };
}

export interface GraphInteractionState {
  draggingNodeIds: NodeId[];
  hoveredNodeId?: NodeId;
  hoveredPortId?: PortId;
  marqueeSelection?: {
    startX: number;
    startY: number;
    endX: number;
    endY: number;
  };
}

export interface GraphViewState {
  viewport: GraphViewportState;
  selection: GraphSelectionState;
  connectionPreview: GraphConnectionPreviewState;
  interaction: GraphInteractionState;
}

export interface CreateGraphViewStateOptions {
  viewport?: Partial<GraphViewportState>;
  selection?: Partial<GraphSelectionState>;
  connectionPreview?: Partial<GraphConnectionPreviewState>;
  interaction?: Partial<GraphInteractionState>;
}

export function createInitialGraphViewState(options: CreateGraphViewStateOptions = {}): GraphViewState {
  const { viewport, selection, connectionPreview, interaction } = options;

  return {
    viewport: {
      zoom: 1,
      panX: 0,
      panY: 0,
      ...viewport,
    },
    selection: {
      selectedNodeIds: [],
      selectedEdgeIds: [],
      ...selection,
    },
    connectionPreview: {
      active: false,
      ...connectionPreview,
    },
    interaction: {
      draggingNodeIds: [],
      ...interaction,
    },
  };
}
