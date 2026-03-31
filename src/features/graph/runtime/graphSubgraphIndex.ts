import type { GraphDocument, GraphEdge, GraphNode, GraphSubgraph, GraphSubgraphId, NodeId } from "../document/graphDocument";

export interface GraphSubgraphBoundaryEdges {
  incomingEdges: GraphEdge[];
  outgoingEdges: GraphEdge[];
  internalEdges: GraphEdge[];
}

export interface GraphSubgraphNestedRelationship {
  parentSubgraphId?: GraphSubgraphId;
  childSubgraphIds: GraphSubgraphId[];
  depth: number;
}

export interface GraphSubgraphIndex {
  readonly document: GraphDocument;
  readonly subgraphsById: ReadonlyMap<GraphSubgraphId, GraphSubgraph>;

  findSubgraph(subgraphId: GraphSubgraphId): GraphSubgraph | undefined;
  listContainingSubgraphs(nodeId: NodeId): GraphSubgraph[];
  findContainingSubgraph(nodeId: NodeId): GraphSubgraph | undefined;
  getContainedNodeIds(subgraphId: GraphSubgraphId): NodeId[];
  getEntryNode(subgraphId: GraphSubgraphId): GraphNode | undefined;
  getBoundaryEdges(subgraphId: GraphSubgraphId): GraphSubgraphBoundaryEdges;
  getNestedRelationship(subgraphId: GraphSubgraphId): GraphSubgraphNestedRelationship | undefined;
}

function buildNodeSet(subgraph: GraphSubgraph): Set<NodeId> {
  return new Set(subgraph.nodeIds);
}

function isStrictSubset(candidate: Set<NodeId>, container: Set<NodeId>) {
  if (candidate.size >= container.size) {
    return false;
  }

  for (const nodeId of candidate) {
    if (!container.has(nodeId)) {
      return false;
    }
  }

  return true;
}

export function createGraphSubgraphIndex(document: GraphDocument): GraphSubgraphIndex {
  const subgraphsById = new Map(document.subgraphs.map((subgraph) => [subgraph.id, subgraph]));
  const nodeToSubgraphIds = new Map<NodeId, GraphSubgraphId[]>();
  const nodeSets = new Map<GraphSubgraphId, Set<NodeId>>();

  for (const subgraph of document.subgraphs) {
    nodeSets.set(subgraph.id, buildNodeSet(subgraph));
    for (const nodeId of subgraph.nodeIds) {
      const bucket = nodeToSubgraphIds.get(nodeId) ?? [];
      bucket.push(subgraph.id);
      nodeToSubgraphIds.set(nodeId, bucket);
    }
  }

  const listContainingSubgraphs = (nodeId: NodeId) => {
    const subgraphs = (nodeToSubgraphIds.get(nodeId) ?? [])
      .map((subgraphId) => subgraphsById.get(subgraphId))
      .filter((subgraph): subgraph is GraphSubgraph => subgraph !== undefined)
      .sort((left, right) => left.nodeIds.length - right.nodeIds.length || left.id.localeCompare(right.id));

    return subgraphs;
  };

  const getNestedRelationship = (subgraphId: GraphSubgraphId): GraphSubgraphNestedRelationship | undefined => {
    const subgraph = subgraphsById.get(subgraphId);
    const nodeSet = nodeSets.get(subgraphId);
    if (!subgraph || !nodeSet) {
      return undefined;
    }

    const parentCandidates = document.subgraphs
      .filter((candidate) => candidate.id !== subgraphId)
      .filter((candidate) => {
        const candidateSet = nodeSets.get(candidate.id);
        return candidateSet ? isStrictSubset(nodeSet, candidateSet) : false;
      })
      .sort((left, right) => left.nodeIds.length - right.nodeIds.length || left.id.localeCompare(right.id));
    const parentSubgraphId = parentCandidates[0]?.id;

    const childSubgraphIds = document.subgraphs
      .filter((candidate) => candidate.id !== subgraphId)
      .filter((candidate) => {
        const candidateSet = nodeSets.get(candidate.id);
        return candidateSet ? isStrictSubset(candidateSet, nodeSet) : false;
      })
      .filter((candidate) => {
        const candidateSet = nodeSets.get(candidate.id)!;
        return !document.subgraphs.some((other) => {
          if (other.id === candidate.id || other.id === subgraphId) {
            return false;
          }

          const otherSet = nodeSets.get(other.id);
          return otherSet ? isStrictSubset(candidateSet, otherSet) && isStrictSubset(otherSet, nodeSet) : false;
        });
      })
      .map((candidate) => candidate.id)
      .sort((left, right) => left.localeCompare(right));

    let depth = 0;
    let currentParentId: GraphSubgraphId | undefined = parentSubgraphId;
    while (currentParentId) {
      depth += 1;
      currentParentId = getNestedRelationship(currentParentId)?.parentSubgraphId;
    }

    return {
      parentSubgraphId,
      childSubgraphIds,
      depth,
    };
  };

  return {
    document,
    subgraphsById,
    findSubgraph(subgraphId) {
      return subgraphsById.get(subgraphId);
    },
    listContainingSubgraphs,
    findContainingSubgraph(nodeId) {
      return listContainingSubgraphs(nodeId)[0];
    },
    getContainedNodeIds(subgraphId) {
      return [...(subgraphsById.get(subgraphId)?.nodeIds ?? [])];
    },
    getEntryNode(subgraphId) {
      const entryNodeId = subgraphsById.get(subgraphId)?.entryNodeId;
      return entryNodeId ? document.nodes.find((node) => node.id === entryNodeId) : undefined;
    },
    getBoundaryEdges(subgraphId) {
      const nodeSet = nodeSets.get(subgraphId) ?? new Set<NodeId>();
      const incomingEdges: GraphEdge[] = [];
      const outgoingEdges: GraphEdge[] = [];
      const internalEdges: GraphEdge[] = [];

      for (const edge of document.edges) {
        const sourceInside = nodeSet.has(edge.sourceNodeId);
        const targetInside = nodeSet.has(edge.targetNodeId);
        if (sourceInside && targetInside) {
          internalEdges.push(edge);
        } else if (!sourceInside && targetInside) {
          incomingEdges.push(edge);
        } else if (sourceInside && !targetInside) {
          outgoingEdges.push(edge);
        }
      }

      return {
        incomingEdges,
        outgoingEdges,
        internalEdges,
      };
    },
    getNestedRelationship,
  };
}

