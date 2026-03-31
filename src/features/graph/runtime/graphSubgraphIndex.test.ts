import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
} from "../document/graphDocument";
import { instantiateTestNode } from "../testing/graphTestUtils";
import { createGraphSubgraphIndex } from "./graphSubgraphIndex";

describe("graphSubgraphIndex", () => {
  it("resolves containing subgraphs, boundaries, and nesting", () => {
    const nodeA = instantiateTestNode("flow.start", "node-a", { x: 0, y: 0 });
    const nodeB = instantiateTestNode("scene.spawn-marker", "node-b", { x: 140, y: 0 });
    const nodeC = instantiateTestNode("flow.wait-signal", "node-c", { x: 280, y: 0 });
    const nodeD = instantiateTestNode("flow.start", "node-d", { x: -140, y: 0 });
    const document = createGraphDocument({
      id: "graph-subgraph-index",
      nodes: [nodeA, nodeB, nodeC, nodeD],
      edges: [
        createGraphEdge({
          id: "edge-d-a",
          sourceNodeId: nodeD.id,
          sourcePortId: `${nodeD.id}:next`,
          targetNodeId: nodeA.id,
          targetPortId: `${nodeA.id}:in`,
        }),
        createGraphEdge({
          id: "edge-a-b",
          sourceNodeId: nodeA.id,
          sourcePortId: `${nodeA.id}:out`,
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
      subgraphs: [
        {
          id: "subgraph-outer",
          title: "Outer",
          nodeIds: [nodeA.id, nodeB.id, nodeC.id],
          entryNodeId: nodeA.id,
        },
        {
          id: "subgraph-inner",
          title: "Inner",
          nodeIds: [nodeA.id, nodeB.id],
          entryNodeId: nodeA.id,
        },
      ],
    });

    const index = createGraphSubgraphIndex(document);

    expect(index.listContainingSubgraphs(nodeA.id).map((subgraph) => subgraph.id)).toEqual([
      "subgraph-inner",
      "subgraph-outer",
    ]);
    expect(index.findContainingSubgraph(nodeA.id)?.id).toBe("subgraph-inner");
    expect(index.getContainedNodeIds("subgraph-inner")).toEqual([nodeA.id, nodeB.id]);
    expect(index.getEntryNode("subgraph-inner")?.id).toBe(nodeA.id);
    expect(index.getBoundaryEdges("subgraph-inner").incomingEdges.map((edge) => edge.id)).toEqual(["edge-d-a"]);
    expect(index.getBoundaryEdges("subgraph-inner").outgoingEdges.map((edge) => edge.id)).toEqual(["edge-b-c"]);
    expect(index.getBoundaryEdges("subgraph-inner").internalEdges.map((edge) => edge.id)).toEqual(["edge-a-b"]);
    expect(index.getNestedRelationship("subgraph-inner")).toEqual({
      parentSubgraphId: "subgraph-outer",
      childSubgraphIds: [],
      depth: 1,
    });
    expect(index.getNestedRelationship("subgraph-outer")).toEqual({
      parentSubgraphId: undefined,
      childSubgraphIds: ["subgraph-inner"],
      depth: 0,
    });
  });
});
