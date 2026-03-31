import { useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent, type RefObject, type WheelEvent } from "react";
import type { GraphPoint, NodeId } from "../../document/graphDocument";
import type { GraphWorkspaceController } from "../../GraphWorkspaceController";
import type { GraphFrameNode } from "../../frame/graphFrame";
import {
  buildGraphPanViewportPatch,
  buildGraphZoomViewportPatch,
  createGraphPanSession,
  screenToGraphLocal,
  screenToGraphWorld,
  type GraphPanSession,
} from "../../interaction/graphPanZoomHandler";
import {
  buildGraphNodeMoveCommand,
  createGraphNodeDragSession,
  updateGraphNodeDragSession,
  type GraphNodeDragSession,
} from "../../interaction/graphNodeDragHandler";
import { clearGraphConnectionPreview } from "../../interaction/graphConnectionPreviewHandler";
import { isGraphAdditiveSelectionPointer, isGraphPrimaryPointer, shouldStartGraphPanGesture } from "../../../../host/input/graphInput";
import { createLocalRect, intersectsNodeRect, pointToClient, unique } from "./graphCanvasUtils";

export interface MarqueeSession {
  startLocal: GraphPoint;
  currentLocal: GraphPoint;
  additive: boolean;
}

interface UseGraphCanvasPointerInteractionsOptions {
  controller: GraphWorkspaceController;
  viewportRef: RefObject<HTMLDivElement | null>;
  displayNodes: GraphFrameNode[];
  viewState: GraphWorkspaceController["viewState"];
  onDismissContextMenu: () => void;
}

export function useGraphCanvasPointerInteractions(options: UseGraphCanvasPointerInteractionsOptions) {
  const { controller, viewportRef, displayNodes, viewState, onDismissContextMenu } = options;
  const [dragSession, setDragSession] = useState<GraphNodeDragSession | null>(null);
  const [panSession, setPanSession] = useState<GraphPanSession | null>(null);
  const [marqueeSession, setMarqueeSession] = useState<MarqueeSession | null>(null);
  const didFinishDragRef = useRef(false);

  useEffect(() => {
    if (!dragSession && !panSession && !marqueeSession) {
      return;
    }

    const handlePointerMove = (event: PointerEvent) => {
      const viewportElement = viewportRef.current;
      if (!viewportElement) {
        return;
      }

      if (dragSession) {
        const world = screenToGraphWorld(
          { x: event.clientX, y: event.clientY },
          viewportElement,
          controller.viewState.viewport,
        );
        setDragSession((current) => {
          if (!current) {
            return current;
          }

          return updateGraphNodeDragSession(current, world);
        });
        return;
      }

      if (panSession) {
        controller.patchViewport(
          buildGraphPanViewportPatch(panSession, { x: event.clientX, y: event.clientY }),
        );
        return;
      }

      if (marqueeSession) {
        const local = screenToGraphLocal({ x: event.clientX, y: event.clientY }, viewportElement);
        setMarqueeSession((current) => {
          if (!current) {
            return current;
          }

          return {
            ...current,
            currentLocal: local,
          };
        });
        controller.patchInteraction({
          marqueeSelection: {
            startX: marqueeSession.startLocal.x,
            startY: marqueeSession.startLocal.y,
            endX: local.x,
            endY: local.y,
          },
        });
      }
    };

    const handlePointerUp = () => {
      const viewportElement = viewportRef.current;

      if (dragSession) {
        const moveCommand = buildGraphNodeMoveCommand(dragSession);
        if (moveCommand) {
          controller.execute(moveCommand);
          didFinishDragRef.current = true;
        }

        controller.patchInteraction({ draggingNodeIds: [] });
        setDragSession(null);
      }

      if (panSession) {
        setPanSession(null);
      }

      if (marqueeSession && viewportElement) {
        const localRect = createLocalRect(marqueeSession.startLocal, marqueeSession.currentLocal);
        const additive = marqueeSession.additive;

        if (localRect.width < 4 && localRect.height < 4) {
          if (!additive) {
            controller.setSelection({ selectedNodeIds: [], selectedEdgeIds: [] });
          }
        } else {
          const startWorld = screenToGraphWorld(
            pointToClient({ x: localRect.x, y: localRect.y }, viewportElement),
            viewportElement,
            viewState.viewport,
          );
          const endWorld = screenToGraphWorld(
            pointToClient({ x: localRect.x + localRect.width, y: localRect.y + localRect.height }, viewportElement),
            viewportElement,
            viewState.viewport,
          );
          const worldRect = {
            x: Math.min(startWorld.x, endWorld.x),
            y: Math.min(startWorld.y, endWorld.y),
            width: Math.abs(endWorld.x - startWorld.x),
            height: Math.abs(endWorld.y - startWorld.y),
          };
          const hitNodeIds = displayNodes
            .filter((node) => intersectsNodeRect(node, worldRect))
            .map((node) => node.id);

          controller.setSelection({
            selectedNodeIds: additive
              ? unique([...viewState.selection.selectedNodeIds, ...hitNodeIds])
              : hitNodeIds,
            selectedEdgeIds: [],
          });
        }

        controller.patchInteraction({ marqueeSelection: undefined });
        setMarqueeSession(null);
      }
    };

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", handlePointerUp);
    window.addEventListener("pointercancel", handlePointerUp);

    return () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", handlePointerUp);
      window.removeEventListener("pointercancel", handlePointerUp);
    };
  }, [controller, displayNodes, dragSession, marqueeSession, panSession, viewState.selection.selectedNodeIds, viewState.viewport, viewportRef]);

  const handleViewportWheel = (event: WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    onDismissContextMenu();

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    controller.patchViewport(
      buildGraphZoomViewportPatch(
        { x: event.clientX, y: event.clientY },
        viewportElement,
        controller.viewState.viewport,
        event.deltaY,
      ),
    );
  };

  const handleViewportPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    onDismissContextMenu();

    if (shouldStartGraphPanGesture(event)) {
      setPanSession(createGraphPanSession({ x: event.clientX, y: event.clientY }, viewState.viewport));
      return;
    }

    if (!isGraphPrimaryPointer(event) || event.target !== event.currentTarget) {
      return;
    }

    /**
     * 连线预览态下，点击空白区域的首要语义是“取消这次连线”，
     * 而不是顺便开始 marquee。否则用户只是想退出临时态，
     * 却会误触成框选，手感会非常别扭。
     */
    if (viewState.connectionPreview.active) {
      controller.setConnectionPreview(clearGraphConnectionPreview());
      controller.patchInteraction({ hoveredPortId: undefined, marqueeSelection: undefined });
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const local = screenToGraphLocal({ x: event.clientX, y: event.clientY }, viewportElement);
    const additive = isGraphAdditiveSelectionPointer(event);

    controller.patchInteraction({
      hoveredPortId: undefined,
      marqueeSelection: {
        startX: local.x,
        startY: local.y,
        endX: local.x,
        endY: local.y,
      },
    });
    setMarqueeSession({ startLocal: local, currentLocal: local, additive });
  };

  const handleViewportPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!viewState.connectionPreview.active) {
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const pointer = screenToGraphWorld(
      { x: event.clientX, y: event.clientY },
      viewportElement,
      viewState.viewport,
    );

    controller.setConnectionPreview({
      ...viewState.connectionPreview,
      active: true,
      pointer,
    });
  };

  const beginNodeDrag = (event: ReactPointerEvent<HTMLElement>, nodeId: NodeId) => {
    if (!isGraphPrimaryPointer(event)) {
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const world = screenToGraphWorld(
      { x: event.clientX, y: event.clientY },
      viewportElement,
      viewState.viewport,
    );

    const selection = viewState.selection.selectedNodeIds.includes(nodeId)
      ? viewState.selection.selectedNodeIds
      : [nodeId];

    controller.setSelection({ selectedNodeIds: selection, selectedEdgeIds: [] });
    controller.patchInteraction({ draggingNodeIds: selection, hoveredNodeId: nodeId });
    setDragSession(createGraphNodeDragSession(selection, world));
  };

  const clearTransientInteraction = () => {
    controller.patchInteraction({ marqueeSelection: undefined });
  };

  const marqueeRect = useMemo(() => {
    return marqueeSession ? createLocalRect(marqueeSession.startLocal, marqueeSession.currentLocal) : null;
  }, [marqueeSession]);

  return {
    dragSession,
    didFinishDragRef,
    marqueeRect,
    handleViewportWheel,
    handleViewportPointerDown,
    handleViewportPointerMove,
    beginNodeDrag,
    clearTransientInteraction,
  };
}
