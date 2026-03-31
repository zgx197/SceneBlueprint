import type {
  EdgeId,
  GraphCommentId,
  GraphGroupId,
  GraphSubgraphId,
  NodeId,
  PortId,
} from "../document/graphDocument";

export interface GraphViewportState {
  zoom: number;
  panX: number;
  panY: number;
}

export interface GraphSelectionState {
  selectedNodeIds: NodeId[];
  selectedEdgeIds: EdgeId[];
  selectedGroupIds: GraphGroupId[];
  selectedCommentIds: GraphCommentId[];
  selectedSubgraphIds: GraphSubgraphId[];
  primarySelectedNodeId?: NodeId;
  primarySelectedEdgeId?: EdgeId;
  primarySelectedGroupId?: GraphGroupId;
  primarySelectedCommentId?: GraphCommentId;
  primarySelectedSubgraphId?: GraphSubgraphId;
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
  hoveredGroupId?: GraphGroupId;
  hoveredCommentId?: GraphCommentId;
  hoveredSubgraphId?: GraphSubgraphId;
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
      selectedGroupIds: [],
      selectedCommentIds: [],
      selectedSubgraphIds: [],
      primarySelectedNodeId: undefined,
      primarySelectedEdgeId: undefined,
      primarySelectedGroupId: undefined,
      primarySelectedCommentId: undefined,
      primarySelectedSubgraphId: undefined,
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
