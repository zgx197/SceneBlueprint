import { useMemo, useState, type RefObject } from "react";
import type { GraphPoint, NodeId, PortId } from "../../document/graphDocument";
import type { GraphWorkspaceController } from "../../GraphWorkspaceController";
import type { GraphNodeDefinition } from "../../definitions/graphDefinitions";
import {
  buildGraphContextMenuModel,
  buildGraphNodeDefinitionGroups,
  createGraphHitTarget,
  getSelectionForGraphHitTarget,
  type GraphContextMenuActionId,
  type GraphContextMenuState,
  type GraphHitTarget,
  type GraphHitTargetDescriptor,
} from "../graphCanvasContextMenuModel";
import { screenToGraphLocal, screenToGraphWorld } from "../../interaction/graphPanZoomHandler";

interface UseGraphCanvasContextMenuOptions {
  controller: GraphWorkspaceController;
  viewportRef: RefObject<HTMLDivElement | null>;
}

export function useGraphCanvasContextMenu(options: UseGraphCanvasContextMenuOptions) {
  const { controller, viewportRef } = options;
  const [contextMenu, setContextMenu] = useState<GraphContextMenuState | null>(null);

  const groupedDefinitions = useMemo(() => {
    return buildGraphNodeDefinitionGroups(controller.definitions.listNodes());
  }, [controller.definitions]);

  const contextMenuModel = useMemo(() => {
    if (!contextMenu) {
      return null;
    }

    return buildGraphContextMenuModel({
      target: contextMenu.target,
      definitionGroups: groupedDefinitions,
      activeCategory: contextMenu.activeCategory,
    });
  }, [contextMenu, groupedDefinitions]);

  const syncSelectionForContextTarget = (target: GraphHitTarget) => {
    const selection = getSelectionForGraphHitTarget(target);
    if (selection) {
      controller.setSelection(selection);
    }
  };

  const openContextMenu = (
    clientPoint: GraphPoint,
    descriptor: GraphHitTargetDescriptor,
    options: { syncSelection?: boolean } = {},
  ) => {
    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return null;
    }

    const local = screenToGraphLocal(clientPoint, viewportElement);
    const world = screenToGraphWorld(clientPoint, viewportElement, controller.viewState.viewport);
    const target = createGraphHitTarget(world, descriptor);
    const viewportRect = viewportElement.getBoundingClientRect();
    const menuWidth = 420;
    const menuHeight = 360;
    const clampedX = Math.min(Math.max(local.x, 12), Math.max(12, viewportRect.width - menuWidth - 12));
    const clampedY = Math.min(Math.max(local.y, 12), Math.max(12, viewportRect.height - menuHeight - 12));

    if (options.syncSelection ?? descriptor.kind !== "canvas") {
      syncSelectionForContextTarget(target);
    }

    setContextMenu({
      screenX: clampedX,
      screenY: clampedY,
      target,
      activeCategory: groupedDefinitions[0]?.category,
    });

    return target;
  };

  const executeContextMenuAction = (actionId: GraphContextMenuActionId) => {
    if (!contextMenu) {
      return;
    }

    switch (actionId) {
      case "context.disconnect-node-edges": {
        if (contextMenu.target.kind === "node") {
          controller.disconnectNodeEdges([contextMenu.target.nodeId]);
        }
        setContextMenu(null);
        return;
      }
      case "context.disconnect-port-edges": {
        if (contextMenu.target.kind === "port") {
          controller.disconnectPortEdges([contextMenu.target.portId]);
        }
        setContextMenu(null);
        return;
      }
      case "context.delete-node": {
        if (contextMenu.target.kind === "node") {
          controller.execute({ type: "graph.remove-nodes", nodeIds: [contextMenu.target.nodeId] });
        }
        setContextMenu(null);
        return;
      }
      case "context.delete-edge": {
        if (contextMenu.target.kind === "edge") {
          controller.execute({ type: "graph.remove-edges", edgeIds: [contextMenu.target.edgeId] });
        }
        setContextMenu(null);
        return;
      }
      case "context.reset-viewport": {
        controller.patchViewport({ zoom: 1, panX: 0, panY: 0 });
        setContextMenu(null);
        return;
      }
      default:
        return actionId satisfies never;
    }
  };

  const handleEdgeContextMenu = (clientPoint: GraphPoint, edgeId: string) => {
    openContextMenu(clientPoint, { kind: "edge", edgeId });
  };

  const handleNodeContextMenu = (clientPoint: GraphPoint, nodeId: NodeId) => {
    openContextMenu(clientPoint, { kind: "node", nodeId });
  };

  const handlePortContextMenu = (
    clientPoint: GraphPoint,
    nodeId: NodeId,
    portId: PortId,
    direction: "input" | "output",
  ) => {
    openContextMenu(clientPoint, { kind: "port", nodeId, portId, direction });
  };

  const createNodeFromContextMenu = (definition: GraphNodeDefinition) => {
    if (!contextMenu) {
      return;
    }

    controller.execute({
      type: "graph.add-node",
      nodeTypeId: definition.typeId,
      position: {
        x: Math.round(contextMenu.target.world.x),
        y: Math.round(contextMenu.target.world.y),
      },
    });
    setContextMenu(null);
  };

  return {
    contextMenu,
    setContextMenu,
    contextMenuModel,
    openContextMenu,
    executeContextMenuAction,
    handleEdgeContextMenu,
    handleNodeContextMenu,
    handlePortContextMenu,
    createNodeFromContextMenu,
  };
}
