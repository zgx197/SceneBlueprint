import type { EdgeId, GraphDocument, NodeId } from "../document/graphDocument";

interface CreateAdjacencyOptions {
  excludeEdgeIds?: ReadonlySet<EdgeId>;
}

function buildAdjacency(document: GraphDocument, options: CreateAdjacencyOptions = {}) {
  const adjacency = new Map<NodeId, NodeId[]>();
  const inDegree = new Map<NodeId, number>();
  const validNodeIds = new Set(document.nodes.map((node) => node.id));
  const excludedEdgeIds = options.excludeEdgeIds ?? new Set<EdgeId>();

  for (const node of document.nodes) {
    adjacency.set(node.id, []);
    inDegree.set(node.id, 0);
  }

  for (const edge of document.edges) {
    if (excludedEdgeIds.has(edge.id)) {
      continue;
    }

    if (!validNodeIds.has(edge.sourceNodeId) || !validNodeIds.has(edge.targetNodeId)) {
      continue;
    }

    adjacency.get(edge.sourceNodeId)?.push(edge.targetNodeId);
    inDegree.set(edge.targetNodeId, (inDegree.get(edge.targetNodeId) ?? 0) + 1);
  }

  return {
    adjacency,
    inDegree,
    validNodeIds,
  };
}

export function getReachableNodes(
  document: GraphDocument,
  startNodeId: NodeId,
  options: CreateAdjacencyOptions = {},
): NodeId[] {
  const { adjacency, validNodeIds } = buildAdjacency(document, options);
  if (!validNodeIds.has(startNodeId)) {
    return [];
  }

  const visited = new Set<NodeId>();
  const pending: NodeId[] = [startNodeId];

  while (pending.length > 0) {
    const currentNodeId = pending.pop()!;
    if (visited.has(currentNodeId)) {
      continue;
    }

    visited.add(currentNodeId);
    for (const nextNodeId of adjacency.get(currentNodeId) ?? []) {
      if (!visited.has(nextNodeId)) {
        pending.push(nextNodeId);
      }
    }
  }

  visited.delete(startNodeId);
  return [...visited];
}

export function wouldCreateCycle(
  document: GraphDocument,
  sourceNodeId: NodeId,
  targetNodeId: NodeId,
  options: CreateAdjacencyOptions = {},
): boolean {
  return getReachableNodes(document, targetNodeId, options).includes(sourceNodeId);
}

export function topologicalSort(
  document: GraphDocument,
  options: CreateAdjacencyOptions = {},
): NodeId[] | null {
  const { adjacency, inDegree } = buildAdjacency(document, options);
  const queue = [...document.nodes.map((node) => node.id).filter((nodeId) => (inDegree.get(nodeId) ?? 0) === 0)];
  const result: NodeId[] = [];
  const nextInDegree = new Map(inDegree);

  while (queue.length > 0) {
    const currentNodeId = queue.shift()!;
    result.push(currentNodeId);

    for (const nextNodeId of adjacency.get(currentNodeId) ?? []) {
      const degree = (nextInDegree.get(nextNodeId) ?? 0) - 1;
      nextInDegree.set(nextNodeId, degree);
      if (degree === 0) {
        queue.push(nextNodeId);
      }
    }
  }

  return result.length === document.nodes.length ? result : null;
}

export function hasCycle(document: GraphDocument, options: CreateAdjacencyOptions = {}): boolean {
  return topologicalSort(document, options) === null;
}

export function getRootNodeIds(document: GraphDocument, options: CreateAdjacencyOptions = {}): NodeId[] {
  const { inDegree } = buildAdjacency(document, options);
  return document.nodes.map((node) => node.id).filter((nodeId) => (inDegree.get(nodeId) ?? 0) === 0);
}

export function getLeafNodeIds(document: GraphDocument, options: CreateAdjacencyOptions = {}): NodeId[] {
  const { adjacency } = buildAdjacency(document, options);
  return document.nodes.map((node) => node.id).filter((nodeId) => (adjacency.get(nodeId) ?? []).length === 0);
}

export function getConnectedComponents(document: GraphDocument, options: CreateAdjacencyOptions = {}): NodeId[][] {
  const { adjacency } = buildAdjacency(document, options);
  const undirected = new Map<NodeId, Set<NodeId>>();

  for (const node of document.nodes) {
    undirected.set(node.id, new Set<NodeId>());
  }

  for (const [sourceNodeId, nextNodeIds] of adjacency.entries()) {
    for (const targetNodeId of nextNodeIds) {
      undirected.get(sourceNodeId)?.add(targetNodeId);
      undirected.get(targetNodeId)?.add(sourceNodeId);
    }
  }

  const visited = new Set<NodeId>();
  const components: NodeId[][] = [];

  for (const node of document.nodes) {
    if (visited.has(node.id)) {
      continue;
    }

    const component: NodeId[] = [];
    const pending = [node.id];

    while (pending.length > 0) {
      const currentNodeId = pending.pop()!;
      if (visited.has(currentNodeId)) {
        continue;
      }

      visited.add(currentNodeId);
      component.push(currentNodeId);
      for (const neighborNodeId of undirected.get(currentNodeId) ?? []) {
        if (!visited.has(neighborNodeId)) {
          pending.push(neighborNodeId);
        }
      }
    }

    components.push(component);
  }

  return components;
}
