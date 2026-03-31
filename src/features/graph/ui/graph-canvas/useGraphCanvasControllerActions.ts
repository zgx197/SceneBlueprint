import type { MouseEvent, MutableRefObject, PointerEvent as ReactPointerEvent, RefObject } from "react";
import type {
  GraphCommentId,
  GraphGroupId,
  GraphPoint,
  GraphSubgraphId,
  NodeId,
  PortId,
} from "../../document/graphDocument";
import type { GraphWorkspaceController } from "../../GraphWorkspaceController";
import { GRAPH_FRAME_LAYOUT } from "../../frame/graphFrameBuilder";
import { clearGraphConnectionPreview, createGraphConnectionPreview } from "../../interaction/graphConnectionPreviewHandler";
import { screenToGraphWorld } from "../../interaction/graphPanZoomHandler";
import type { GraphHitTargetDescriptor } from "../graphCanvasContextMenuModel";
import { clamp } from "./graphCanvasUtils";
import { isGraphAdditiveSelectionPointer } from "../../../../host/input/graphInput";
import type { GraphCanvasDerivedState } from "./useGraphCanvasDerivedState";
import type { GraphCanvasViewportSize } from "./useGraphCanvasViewportSize";

interface UseGraphCanvasControllerActionsOptions {
  controller: GraphWorkspaceController;
  viewportRef: RefObject<HTMLDivElement | null>;
  viewportSize: GraphCanvasViewportSize;
  viewState: GraphWorkspaceController["viewState"];
  measuredPortAnchors: Map<PortId, GraphPoint>;
  displayState: Pick<GraphCanvasDerivedState, "displayNodeMap" | "displayPortMap">;
  didFinishDragRef: MutableRefObject<boolean>;
  openContextMenu: (
    clientPoint: GraphPoint,
    descriptor: GraphHitTargetDescriptor,
    options?: { syncSelection?: boolean },
  ) => unknown;
}

function createEmptySelection() {
  return {
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
  };
}

export function useGraphCanvasControllerActions(options: UseGraphCanvasControllerActionsOptions) {
  const {
    controller,
    viewportRef,
    viewportSize,
    viewState,
    measuredPortAnchors,
    displayState,
    didFinishDragRef,
    openContextMenu,
  } = options;

  const { displayNodeMap, displayPortMap } = displayState;

  /**
   * 连线预览态是 Graph Canvas 的高优先级临时态。
   *
   * 一旦已经从输出端点拉出预览线，用户接下来的常见意图只有两类：
   * 1. 左键点击合法输入端点完成连线；
   * 2. 右键 / Esc / 点击空白取消这次连线。
   *
   * 因此在 preview 仍然 active 时，右键不应该再继续穿透到 context menu，
   * 否则会出现“本来想取消拖线，却顺手弹出菜单”的混乱体验。
   */
  const cancelConnectionPreview = () => {
    if (!viewState.connectionPreview.active) {
      return false;
    }

    controller.setConnectionPreview(clearGraphConnectionPreview());
    controller.patchInteraction({ hoveredPortId: undefined });
    return true;
  };

  const startConnectionPreview = (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => {
    event.stopPropagation();

    const sourcePort = displayPortMap.get(portId);
    if (!sourcePort) {
      return;
    }

    controller.setSelection({
      ...createEmptySelection(),
      selectedNodeIds: [nodeId],
      primarySelectedNodeId: nodeId,
    });
    controller.setConnectionPreview(
      createGraphConnectionPreview(
        nodeId,
        portId,
        measuredPortAnchors.get(portId) ?? sourcePort.anchor,
      ),
    );
  };

  const completeConnectionPreview = (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => {
    event.stopPropagation();

    const preview = viewState.connectionPreview;
    if (!preview.active || !preview.fromNodeId || !preview.fromPortId) {
      return;
    }

    const targetPort = displayPortMap.get(portId);
    if (!targetPort || !targetPort.connectable) {
      return;
    }

    controller.execute({
      type: "graph.connect-ports",
      sourceNodeId: preview.fromNodeId,
      sourcePortId: preview.fromPortId,
      targetNodeId: nodeId,
      targetPortId: portId,
    });
    controller.setConnectionPreview(clearGraphConnectionPreview());
    controller.patchInteraction({ hoveredPortId: undefined });
  };

  const focusNode = (nodeId: NodeId) => {
    const node = displayNodeMap.get(nodeId);
    const viewportElement = viewportRef.current;
    if (!node || !viewportElement) {
      return;
    }

    controller.setSelection({
      ...createEmptySelection(),
      selectedNodeIds: [nodeId],
      primarySelectedNodeId: nodeId,
    });
    controller.centerViewportOnPoint(
      {
        x: node.bounds.x + node.bounds.width * 0.5,
        y: node.bounds.y + node.bounds.height * 0.5,
      },
      { width: viewportElement.clientWidth, height: viewportElement.clientHeight },
    );
  };

  const createNodeAtViewportCenter = (nodeTypeId: string) => {
    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const rect = viewportElement.getBoundingClientRect();
    const world = screenToGraphWorld(
      {
        x: rect.left + rect.width * 0.5,
        y: rect.top + rect.height * 0.5,
      },
      viewportElement,
      viewState.viewport,
    );

    controller.execute({
      type: "graph.add-node",
      nodeTypeId,
      position: {
        x: Math.round(world.x),
        y: Math.round(world.y),
      },
    });
  };

  const resetViewport = () => {
    controller.patchViewport({ zoom: 1, panX: 0, panY: 0 });
  };

  const handleNodeClick = (event: MouseEvent<HTMLElement>, nodeId: NodeId) => {
    if (didFinishDragRef.current) {
      didFinishDragRef.current = false;
      return;
    }

    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedNodeIds.includes(nodeId);
      controller.setSelection({
        ...createEmptySelection(),
        selectedNodeIds: exists
          ? viewState.selection.selectedNodeIds.filter((entry) => entry !== nodeId)
          : [...viewState.selection.selectedNodeIds, nodeId],
        primarySelectedNodeId: nodeId,
      });
      return;
    }

    controller.setSelection({
      ...createEmptySelection(),
      selectedNodeIds: [nodeId],
      primarySelectedNodeId: nodeId,
    });
  };

  const handleEdgeClick = (event: MouseEvent<SVGPathElement | SVGGElement>, edgeId: string) => {
    event.stopPropagation();
    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedEdgeIds.includes(edgeId);
      controller.setSelection({
        ...createEmptySelection(),
        selectedEdgeIds: exists
          ? viewState.selection.selectedEdgeIds.filter((entry) => entry !== edgeId)
          : [...viewState.selection.selectedEdgeIds, edgeId],
        primarySelectedEdgeId: edgeId,
      });
      return;
    }

    controller.setSelection({
      ...createEmptySelection(),
      selectedEdgeIds: [edgeId],
      primarySelectedEdgeId: edgeId,
    });
  };

  const handleGroupClick = (event: MouseEvent<HTMLElement>, groupId: GraphGroupId) => {
    event.stopPropagation();
    controller.setSelection({
      ...createEmptySelection(),
      selectedGroupIds: [groupId],
      primarySelectedGroupId: groupId,
    });
  };

  const handleCommentClick = (event: MouseEvent<HTMLElement>, commentId: GraphCommentId) => {
    event.stopPropagation();
    controller.setSelection({
      ...createEmptySelection(),
      selectedCommentIds: [commentId],
      primarySelectedCommentId: commentId,
    });
  };

  const handleSubgraphClick = (event: MouseEvent<HTMLElement>, subgraphId: GraphSubgraphId) => {
    event.stopPropagation();
    controller.setSelection({
      ...createEmptySelection(),
      selectedSubgraphIds: [subgraphId],
      primarySelectedSubgraphId: subgraphId,
    });
  };

  const handleNodePointerEnter = (nodeId: NodeId) => {
    controller.patchInteraction({ hoveredNodeId: nodeId });
  };

  const handleNodePointerLeave = () => {
    controller.patchInteraction({ hoveredNodeId: undefined });
  };

  const handlePortPointerEnter = (portId: PortId) => {
    controller.patchInteraction({ hoveredPortId: portId });
  };

  const handlePortPointerLeave = () => {
    controller.patchInteraction({ hoveredPortId: undefined });
  };

  const handleGroupPointerEnter = (groupId: GraphGroupId) => {
    controller.patchInteraction({ hoveredGroupId: groupId });
  };

  const handleGroupPointerLeave = () => {
    controller.patchInteraction({ hoveredGroupId: undefined });
  };

  const handleCommentPointerEnter = (commentId: GraphCommentId) => {
    controller.patchInteraction({ hoveredCommentId: commentId });
  };

  const handleCommentPointerLeave = () => {
    controller.patchInteraction({ hoveredCommentId: undefined });
  };

  const handleSubgraphPointerEnter = (subgraphId: GraphSubgraphId) => {
    controller.patchInteraction({ hoveredSubgraphId: subgraphId });
  };

  const handleSubgraphPointerLeave = () => {
    controller.patchInteraction({ hoveredSubgraphId: undefined });
  };

  const handleViewportContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();

    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "canvas" }, { syncSelection: false });
  };

  const handleEdgeContextMenu = (clientPoint: GraphPoint, edgeId: string) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "edge", edgeId });
  };

  const handleNodeContextMenu = (clientPoint: GraphPoint, nodeId: NodeId) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "node", nodeId });
  };

  const handleGroupContextMenu = (clientPoint: GraphPoint, groupId: GraphGroupId) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "group", groupId });
  };

  const handleCommentContextMenu = (clientPoint: GraphPoint, commentId: GraphCommentId) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "comment", commentId });
  };

  const handleSubgraphContextMenu = (clientPoint: GraphPoint, subgraphId: GraphSubgraphId) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "subgraph", subgraphId });
  };

  const handlePortContextMenu = (
    clientPoint: GraphPoint,
    nodeId: NodeId,
    portId: PortId,
    direction: "input" | "output",
  ) => {
    if (cancelConnectionPreview()) {
      return;
    }

    openContextMenu(clientPoint, { kind: "port", nodeId, portId, direction });
  };

  const handleMinimapPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();

    if (viewportSize.width <= 0 || viewportSize.height <= 0) {
      return;
    }

    const rect = event.currentTarget.getBoundingClientRect();
    const localX = clamp(event.clientX - rect.left, 0, rect.width);
    const localY = clamp(event.clientY - rect.top, 0, rect.height);
    const worldX = (localX / rect.width) * GRAPH_FRAME_LAYOUT.contentWidth;
    const worldY = (localY / rect.height) * GRAPH_FRAME_LAYOUT.contentHeight;

    controller.centerViewportOnPoint(
      { x: worldX, y: worldY },
      { width: viewportSize.width, height: viewportSize.height },
    );
  };

  return {
    cancelConnectionPreview,
    createNodeAtViewportCenter,
    focusNode,
    resetViewport,
    handleNodeClick,
    handleEdgeClick,
    handleGroupClick,
    handleCommentClick,
    handleSubgraphClick,
    handleEdgeContextMenu,
    handleNodeContextMenu,
    handleGroupContextMenu,
    handleCommentContextMenu,
    handleSubgraphContextMenu,
    handleNodePointerEnter,
    handleNodePointerLeave,
    handleGroupPointerEnter,
    handleGroupPointerLeave,
    handleCommentPointerEnter,
    handleCommentPointerLeave,
    handleSubgraphPointerEnter,
    handleSubgraphPointerLeave,
    handlePortContextMenu,
    handlePortPointerEnter,
    handlePortPointerLeave,
    handleViewportContextMenu,
    handleMinimapPointerDown,
    startConnectionPreview,
    completeConnectionPreview,
  };
}





