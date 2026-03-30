import type { EdgeId, GraphPoint, NodeId, PortDirection, PortId } from "../document/graphDocument";
import type { GraphNodeDefinition } from "../definitions/graphDefinitions";
import type { GraphSelectionState } from "../state/graphViewState";

export type GraphHitTargetDescriptor =
  | { kind: "canvas" }
  | { kind: "node"; nodeId: NodeId }
  | { kind: "edge"; edgeId: EdgeId }
  | { kind: "port"; nodeId: NodeId; portId: PortId; direction: PortDirection };

export type GraphHitTarget =
  | { kind: "canvas"; world: GraphPoint }
  | { kind: "node"; world: GraphPoint; nodeId: NodeId }
  | { kind: "edge"; world: GraphPoint; edgeId: EdgeId }
  | { kind: "port"; world: GraphPoint; nodeId: NodeId; portId: PortId; direction: PortDirection };

export interface GraphContextMenuState {
  screenX: number;
  screenY: number;
  target: GraphHitTarget;
  activeCategory?: string;
}

export type GraphContextMenuActionId =
  | "context.disconnect-node-edges"
  | "context.disconnect-port-edges"
  | "context.delete-node"
  | "context.delete-edge"
  | "context.reset-viewport";

export interface GraphContextMenuAction {
  id: GraphContextMenuActionId;
  label: string;
  tone?: "default" | "danger";
}

export interface GraphNodeDefinitionGroup {
  category: string;
  definitions: GraphNodeDefinition[];
}

export interface GraphContextMenuModel {
  target: GraphHitTarget;
  actions: GraphContextMenuAction[];
  definitionGroups: GraphNodeDefinitionGroup[];
  activeCategory?: string;
  visibleDefinitions: GraphNodeDefinition[];
  showDefinitionBrowser: boolean;
}

export interface BuildGraphContextMenuModelOptions {
  target: GraphHitTarget;
  definitionGroups: GraphNodeDefinitionGroup[];
  activeCategory?: string;
}

export function createGraphHitTarget(
  world: GraphPoint,
  descriptor: GraphHitTargetDescriptor,
): GraphHitTarget {
  switch (descriptor.kind) {
    case "canvas":
      return {
        kind: "canvas",
        world,
      };
    case "node":
      return {
        kind: "node",
        world,
        nodeId: descriptor.nodeId,
      };
    case "edge":
      return {
        kind: "edge",
        world,
        edgeId: descriptor.edgeId,
      };
    case "port":
      return {
        kind: "port",
        world,
        nodeId: descriptor.nodeId,
        portId: descriptor.portId,
        direction: descriptor.direction,
      };
    default:
      return descriptor satisfies never;
  }
}

export function getSelectionForGraphHitTarget(target: GraphHitTarget): GraphSelectionState | null {
  switch (target.kind) {
    case "node":
      return {
        selectedNodeIds: [target.nodeId],
        selectedEdgeIds: [],
      };
    case "edge":
      return {
        selectedNodeIds: [],
        selectedEdgeIds: [target.edgeId],
      };
    case "port":
      return {
        selectedNodeIds: [target.nodeId],
        selectedEdgeIds: [],
      };
    case "canvas":
      return null;
    default:
      return target satisfies never;
  }
}

export function buildGraphNodeDefinitionGroups(
  definitions: GraphNodeDefinition[],
): GraphNodeDefinitionGroup[] {
  const map = new Map<string, GraphNodeDefinition[]>();

  for (const definition of definitions) {
    const category = definition.category ?? "Uncategorized";
    const bucket = map.get(category) ?? [];
    bucket.push(definition);
    map.set(category, bucket);
  }

  return [...map.entries()]
    .map(([category, categoryDefinitions]) => ({
      category,
      definitions: [...categoryDefinitions].sort((left, right) => left.displayName.localeCompare(right.displayName)),
    }))
    .sort((left, right) => left.category.localeCompare(right.category));
}

export function buildGraphContextMenuModel(
  options: BuildGraphContextMenuModelOptions,
): GraphContextMenuModel {
  const { target, definitionGroups, activeCategory } = options;
  const showDefinitionBrowser = target.kind === "canvas";
  const resolvedCategory = showDefinitionBrowser ? activeCategory ?? definitionGroups[0]?.category : undefined;
  const selectedGroup = showDefinitionBrowser
    ? definitionGroups.find((group) => group.category === resolvedCategory) ?? definitionGroups[0]
    : undefined;

  return {
    target,
    actions: getGraphContextMenuActions(target),
    definitionGroups,
    activeCategory: resolvedCategory,
    visibleDefinitions: selectedGroup?.definitions ?? [],
    showDefinitionBrowser,
  };
}

function getGraphContextMenuActions(target: GraphHitTarget): GraphContextMenuAction[] {
  switch (target.kind) {
    case "node":
      return [
        {
          id: "context.disconnect-node-edges",
          label: "断开所有连线",
        },
        {
          id: "context.delete-node",
          label: "删除当前节点",
          tone: "danger",
        },
      ];
    case "port":
      return [
        {
          id: "context.disconnect-port-edges",
          label: "断开此端点所有连线",
        },
      ];
    case "edge":
      return [
        {
          id: "context.delete-edge",
          label: "删除当前连线",
          tone: "danger",
        },
      ];
    case "canvas":
      return [
        {
          id: "context.reset-viewport",
          label: "重置视口",
        },
      ];
    default:
      return target satisfies never;
  }
}