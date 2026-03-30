import type { MouseEvent, MutableRefObject, PointerEvent as ReactPointerEvent, RefObject } from "react";
import type { GraphPoint, NodeId, PortId } from "../../document/graphDocument";
import type { GraphWorkspaceController } from "../../GraphWorkspaceController";
import { GRAPH_FRAME_LAYOUT } from "../../frame/graphFrameBuilder";
import { clearGraphConnectionPreview, createGraphConnectionPreview } from "../../interaction/graphConnectionPreviewHandler";
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

  const startConnectionPreview = (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => {
    event.stopPropagation();

    const sourcePort = displayPortMap.get(portId);
    if (!sourcePort) {
      return;
    }

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
    controller.setConnectionPreview(
      createGraphConnectionPreview(
        nodeId,
        portId,
        // 新建预览线时也必须优先采用实测 socket 圆心，避免起点瞬间从节点中部跳出来。
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

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
    controller.centerViewportOnPoint(
      {
        x: node.bounds.x + node.bounds.width * 0.5,
        y: node.bounds.y + node.bounds.height * 0.5,
      },
      { width: viewportElement.clientWidth, height: viewportElement.clientHeight },
    );
  };

  const handleNodeClick = (event: MouseEvent<HTMLElement>, nodeId: NodeId) => {
    if (didFinishDragRef.current) {
      didFinishDragRef.current = false;
      return;
    }

    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedNodeIds.includes(nodeId);
      controller.setSelection({
        selectedNodeIds: exists
          ? viewState.selection.selectedNodeIds.filter((entry) => entry !== nodeId)
          : [...viewState.selection.selectedNodeIds, nodeId],
        selectedEdgeIds: [],
      });
      return;
    }

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
  };

  const handleEdgeClick = (event: MouseEvent<SVGPathElement | SVGGElement>, edgeId: string) => {
    event.stopPropagation();
    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedEdgeIds.includes(edgeId);
      controller.setSelection({
        selectedNodeIds: [],
        selectedEdgeIds: exists
          ? viewState.selection.selectedEdgeIds.filter((entry) => entry !== edgeId)
          : [...viewState.selection.selectedEdgeIds, edgeId],
      });
      return;
    }

    controller.setSelection({ selectedNodeIds: [], selectedEdgeIds: [edgeId] });
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

  const handleViewportContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();
    openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "canvas" }, { syncSelection: false });
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
    focusNode,
    handleNodeClick,
    handleEdgeClick,
    handleNodePointerEnter,
    handleNodePointerLeave,
    handlePortPointerEnter,
    handlePortPointerLeave,
    handleViewportContextMenu,
    handleMinimapPointerDown,
    startConnectionPreview,
    completeConnectionPreview,
  };
}
