import type { ApplyNodeLayoutCommand } from "../commands/graphCommands";
import type { GraphDocument, NodeId } from "../document/graphDocument";

const GRAPH_AUTO_LAYOUT_HORIZONTAL_GAP = 340;
const GRAPH_AUTO_LAYOUT_VERTICAL_GAP = 180;

function unique<TValue>(values: TValue[]) {
  return [...new Set(values)];
}

export function buildGraphAutoLayoutCommand(
  document: GraphDocument,
  targetNodeIds: NodeId[],
): ApplyNodeLayoutCommand | null {
  const resolvedNodeIds = unique(targetNodeIds.length > 0 ? targetNodeIds : document.nodes.map((node) => node.id));
  if (resolvedNodeIds.length === 0) {
    return null;
  }

  const targetSet = new Set(resolvedNodeIds);
  const targetNodes = document.nodes.filter((node) => targetSet.has(node.id));
  const internalEdges = document.edges.filter(
    (edge) => targetSet.has(edge.sourceNodeId) && targetSet.has(edge.targetNodeId),
  );

  const incoming = new Map<NodeId, number>();
  const outgoing = new Map<NodeId, NodeId[]>();
  const layers = new Map<NodeId, number>();

  for (const node of targetNodes) {
    incoming.set(node.id, 0);
    outgoing.set(node.id, []);
  }

  for (const edge of internalEdges) {
    incoming.set(edge.targetNodeId, (incoming.get(edge.targetNodeId) ?? 0) + 1);
    outgoing.set(edge.sourceNodeId, [...(outgoing.get(edge.sourceNodeId) ?? []), edge.targetNodeId]);
  }

  const queue = targetNodes
    .filter((node) => (incoming.get(node.id) ?? 0) === 0)
    .sort((left, right) => left.position.x - right.position.x || left.position.y - right.position.y)
    .map((node) => node.id);

  const visited = new Set<NodeId>();

  while (queue.length > 0) {
    const nodeId = queue.shift()!;
    visited.add(nodeId);
    const currentLayer = layers.get(nodeId) ?? 0;
    for (const nextNodeId of outgoing.get(nodeId) ?? []) {
      const nextLayer = Math.max(layers.get(nextNodeId) ?? 0, currentLayer + 1);
      layers.set(nextNodeId, nextLayer);
      incoming.set(nextNodeId, (incoming.get(nextNodeId) ?? 1) - 1);
      if ((incoming.get(nextNodeId) ?? 0) <= 0) {
        queue.push(nextNodeId);
      }
    }
  }

  const unresolvedNodes = targetNodes
    .filter((node) => !visited.has(node.id))
    .sort((left, right) => left.position.x - right.position.x || left.position.y - right.position.y);
  const maxResolvedLayer = Math.max(0, ...layers.values());

  unresolvedNodes.forEach((node, index) => {
    layers.set(node.id, maxResolvedLayer + index + 1);
  });

  const layerBuckets = new Map<number, typeof targetNodes>();
  for (const node of targetNodes) {
    const layer = layers.get(node.id) ?? 0;
    const bucket = layerBuckets.get(layer) ?? [];
    bucket.push(node);
    layerBuckets.set(layer, bucket);
  }

  const startX = Math.min(...targetNodes.map((node) => node.position.x));
  const startY = Math.min(...targetNodes.map((node) => node.position.y));
  const layoutEntries: ApplyNodeLayoutCommand["entries"] = [];
  const sortedLayers = [...layerBuckets.entries()].sort((left, right) => left[0] - right[0]);

  sortedLayers.forEach(([layer, nodesInLayer]) => {
    const sortedNodes = [...nodesInLayer].sort((left, right) => left.position.y - right.position.y);
    sortedNodes.forEach((node, rowIndex) => {
      layoutEntries.push({
        nodeId: node.id,
        position: {
          x: startX + layer * GRAPH_AUTO_LAYOUT_HORIZONTAL_GAP,
          y: startY + rowIndex * GRAPH_AUTO_LAYOUT_VERTICAL_GAP,
        },
      });
    });
  });

  return {
    type: "graph.apply-node-layout",
    entries: layoutEntries,
  };
}
