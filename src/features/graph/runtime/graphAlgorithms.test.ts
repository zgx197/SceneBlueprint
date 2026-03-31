import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
} from "../document/graphDocument";
import { instantiateTestNode } from "../testing/graphTestUtils";
import {
  getConnectedComponents,
  getLeafNodeIds,
  getReachableNodes,
  getRootNodeIds,
  hasCycle,
  topologicalSort,
  wouldCreateCycle,
} from "./graphAlgorithms";

describe("graphAlgorithms", () => {
  it("derives DAG traversal, roots, leaves, and components", () => {
    const nodeA = instantiateTestNode("flow.start", "node-a", { x: 0, y: 0 });
    const nodeB = instantiateTestNode("scene.spawn-marker", "node-b", { x: 120, y: 0 });
    const nodeC = instantiateTestNode("flow.wait-signal", "node-c", { x: 240, y: 0 });
    const nodeD = instantiateTestNode("flow.wait-signal", "node-d", { x: 0, y: 160 });
    const document = createGraphDocument({
      id: "graph-algorithms",
      nodes: [nodeA, nodeB, nodeC, nodeD],
      edges: [
        createGraphEdge({
          id: "edge-a-b",
          sourceNodeId: nodeA.id,
          sourcePortId: `${nodeA.id}:next`,
          targetNodeId: nodeB.id,
          targetPortId: `${nodeB.id}:in`,
        }),
        createGraphEdge({
          id: "edge-b-c",
          sourceNodeId: nodeB.id,
          sourcePortId: `${nodeB.id}:completed`,
          targetNodeId: nodeC.id,
          targetPortId: `${nodeC.id}:in`,
        }),
      ],
    });

    expect(getReachableNodes(document, nodeA.id)).toEqual([nodeB.id, nodeC.id]);
    expect(topologicalSort(document)).toEqual([nodeA.id, nodeD.id, nodeB.id, nodeC.id]);
    expect(hasCycle(document)).toBe(false);
    expect(getRootNodeIds(document)).toEqual([nodeA.id, nodeD.id]);
    expect(getLeafNodeIds(document)).toEqual([nodeC.id, nodeD.id]);
    expect(getConnectedComponents(document)).toEqual([
      [nodeA.id, nodeB.id, nodeC.id],
      [nodeD.id],
    ]);
  });

  it("detects cycles and respects excluded edges", () => {
    const nodeA = instantiateTestNode("flow.start", "cycle-a", { x: 0, y: 0 });
    const nodeB = instantiateTestNode("scene.spawn-marker", "cycle-b", { x: 120, y: 0 });
    const nodeC = instantiateTestNode("flow.wait-signal", "cycle-c", { x: 240, y: 0 });
    const document = createGraphDocument({
      id: "graph-cycle",
      nodes: [nodeA, nodeB, nodeC],
      edges: [
        createGraphEdge({
          id: "edge-a-b",
          sourceNodeId: nodeA.id,
          sourcePortId: `${nodeA.id}:next`,
          targetNodeId: nodeB.id,
          targetPortId: `${nodeB.id}:in`,
        }),
        createGraphEdge({
          id: "edge-b-c",
          sourceNodeId: nodeB.id,
          sourcePortId: `${nodeB.id}:completed`,
          targetNodeId: nodeC.id,
          targetPortId: `${nodeC.id}:in`,
        }),
        createGraphEdge({
          id: "edge-c-b",
          sourceNodeId: nodeC.id,
          sourcePortId: `${nodeC.id}:out`,
          targetNodeId: nodeB.id,
          targetPortId: `${nodeB.id}:in`,
        }),
      ],
    });

    expect(hasCycle(document)).toBe(true);
    expect(topologicalSort(document)).toBeNull();
    expect(wouldCreateCycle(document, nodeC.id, nodeA.id)).toBe(true);
    expect(wouldCreateCycle(document, nodeC.id, nodeA.id, { excludeEdgeIds: new Set(["edge-b-c"]) })).toBe(false);
  });
});
