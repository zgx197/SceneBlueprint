import dagre from "dagre";
import ELK from "elkjs/lib/elk.bundled";
import type { ApplyNodeLayoutCommand } from "../commands/graphCommands";
import type { GraphDocument, GraphNode, NodeId } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphProfile } from "../profile/graphProfile";
import { measureGraphNodePresentation } from "./graphNodePresentation";
import type { TextMeasurer } from "../../../host/measurement/textMeasurer";

export type GraphLayoutProviderId = "elk" | "dagre";

export interface GraphLayoutServiceResult {
  command: ApplyNodeLayoutCommand;
  providerId: GraphLayoutProviderId;
  attemptedProviderIds: GraphLayoutProviderId[];
}

export interface GraphLayoutService {
  readonly preferredProviderId: GraphLayoutProviderId;
  buildApplyLayoutCommand(document: GraphDocument, targetNodeIds: NodeId[]): Promise<GraphLayoutServiceResult | null>;
}

interface CreateGraphLayoutServiceOptions {
  definitions: GraphDefinitionRegistry;
  profile: GraphProfile;
  textMeasurer: TextMeasurer;
  preferredProviderId?: GraphLayoutProviderId;
}

interface GraphLayoutNodeEntry {
  id: NodeId;
  node: GraphNode;
  width: number;
  height: number;
}

interface GraphLayoutSubset {
  nodes: GraphLayoutNodeEntry[];
  edges: GraphDocument["edges"];
  origin: { x: number; y: number };
}

interface GraphLayoutProviderResult {
  entries: ApplyNodeLayoutCommand["entries"];
}

interface GraphLayoutProvider {
  id: GraphLayoutProviderId;
  layout(subset: GraphLayoutSubset): Promise<GraphLayoutProviderResult | null>;
}

function unique<TValue>(values: TValue[]) {
  return [...new Set(values)];
}

function buildLayoutSubset(
  document: GraphDocument,
  targetNodeIds: NodeId[],
  definitions: GraphDefinitionRegistry,
  profile: GraphProfile,
  textMeasurer: TextMeasurer,
): GraphLayoutSubset | null {
  const resolvedNodeIds = unique(targetNodeIds.length > 0 ? targetNodeIds : document.nodes.map((node) => node.id));
  if (resolvedNodeIds.length === 0) {
    return null;
  }

  const nodeIdSet = new Set(resolvedNodeIds);
  const nodes = document.nodes
    .filter((node) => nodeIdSet.has(node.id))
    .map((node) => {
      const metrics = measureGraphNodePresentation(node, definitions, textMeasurer, profile.render);
      return {
        id: node.id,
        node,
        width: metrics.width,
        height: metrics.height,
      };
    });
  if (nodes.length === 0) {
    return null;
  }

  const edges = document.edges.filter((edge) => {
    return nodeIdSet.has(edge.sourceNodeId) && nodeIdSet.has(edge.targetNodeId);
  });
  const origin = {
    x: Math.min(...nodes.map((entry) => entry.node.position.x)),
    y: Math.min(...nodes.map((entry) => entry.node.position.y)),
  };

  return {
    nodes,
    edges,
    origin,
  };
}

function normalizeLayoutEntries(
  entries: Array<{ nodeId: NodeId; x: number; y: number }>,
  origin: { x: number; y: number },
): ApplyNodeLayoutCommand["entries"] {
  const minX = Math.min(...entries.map((entry) => entry.x));
  const minY = Math.min(...entries.map((entry) => entry.y));

  return entries.map((entry) => ({
    nodeId: entry.nodeId,
    position: {
      x: Math.round(origin.x + (entry.x - minX)),
      y: Math.round(origin.y + (entry.y - minY)),
    },
  }));
}

function createElkLayoutProvider(): GraphLayoutProvider {
  const elk = new ELK();

  return {
    id: "elk",
    async layout(subset) {
      const graph = await elk.layout({
        id: "graph-layout-root",
        layoutOptions: {
          "elk.algorithm": "layered",
          "elk.direction": "RIGHT",
          "elk.spacing.nodeNode": "68",
          "elk.layered.spacing.nodeNodeBetweenLayers": "196",
          "elk.padding": "[left=0,top=0,right=0,bottom=0]",
        },
        children: subset.nodes.map((entry) => ({
          id: entry.id,
          width: entry.width,
          height: entry.height,
        })),
        edges: subset.edges.map((edge) => ({
          id: edge.id,
          sources: [edge.sourceNodeId],
          targets: [edge.targetNodeId],
        })),
      });

      const laidOutChildren = graph.children ?? [];
      if (laidOutChildren.length !== subset.nodes.length) {
        return null;
      }

      const positionedEntries = laidOutChildren.flatMap((node) => {
        if (typeof node.id !== "string" || typeof node.x !== "number" || typeof node.y !== "number") {
          return [];
        }

        return [
          {
            nodeId: node.id,
            x: node.x,
            y: node.y,
          },
        ];
      });
      if (positionedEntries.length !== subset.nodes.length) {
        return null;
      }

      return {
        entries: normalizeLayoutEntries(positionedEntries, subset.origin),
      };
    },
  };
}

function createDagreLayoutProvider(): GraphLayoutProvider {
  return {
    id: "dagre",
    async layout(subset) {
      const graph = new dagre.graphlib.Graph();
      graph.setGraph({
        rankdir: "LR",
        align: "UL",
        ranksep: 196,
        nodesep: 72,
        edgesep: 30,
        marginx: 0,
        marginy: 0,
      });
      graph.setDefaultEdgeLabel(() => ({}));

      subset.nodes.forEach((entry) => {
        graph.setNode(entry.id, {
          width: entry.width,
          height: entry.height,
        });
      });
      subset.edges.forEach((edge) => {
        graph.setEdge(edge.sourceNodeId, edge.targetNodeId);
      });
      dagre.layout(graph);

      const positionedEntries = subset.nodes.flatMap((entry) => {
        const laidOutNode = graph.node(entry.id);
        if (!laidOutNode || typeof laidOutNode.x !== "number" || typeof laidOutNode.y !== "number") {
          return [];
        }

        return [
          {
            nodeId: entry.id,
            x: laidOutNode.x - entry.width * 0.5,
            y: laidOutNode.y - entry.height * 0.5,
          },
        ];
      });
      if (positionedEntries.length !== subset.nodes.length) {
        return null;
      }

      return {
        entries: normalizeLayoutEntries(positionedEntries, subset.origin),
      };
    },
  };
}

export function createGraphLayoutService(options: CreateGraphLayoutServiceOptions): GraphLayoutService {
  const { definitions, profile, textMeasurer } = options;
  const preferredProviderId = options.preferredProviderId ?? "elk";
  const providers = new Map<GraphLayoutProviderId, GraphLayoutProvider>([
    ["elk", createElkLayoutProvider()],
    ["dagre", createDagreLayoutProvider()],
  ]);

  return {
    preferredProviderId,
    async buildApplyLayoutCommand(document, targetNodeIds) {
      const subset = buildLayoutSubset(document, targetNodeIds, definitions, profile, textMeasurer);
      if (!subset) {
        return null;
      }

      const fallbackProviderId: GraphLayoutProviderId = preferredProviderId === "elk" ? "dagre" : "elk";
      const attemptedProviderIds: GraphLayoutProviderId[] = [];

      for (const providerId of [preferredProviderId, fallbackProviderId]) {
        const provider = providers.get(providerId);
        if (!provider) {
          continue;
        }

        attemptedProviderIds.push(providerId);

        try {
          const result = await provider.layout(subset);
          if (!result || result.entries.length === 0) {
            continue;
          }

          return {
            providerId,
            attemptedProviderIds,
            command: {
              type: "graph.apply-node-layout",
              entries: result.entries,
            },
          };
        } catch {
          continue;
        }
      }

      return null;
    },
  };
}
