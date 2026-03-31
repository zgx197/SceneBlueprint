import { describe, expect, it } from "vitest";
import { createGraphDocument } from "../document/graphDocument";
import { instantiateTestNode } from "../testing/graphTestUtils";
import { createGraphSubgraphRuntime } from "./graphSubgraphRuntime";

describe("graphSubgraphRuntime", () => {
  it("prunes invalid node ids and reassigns invalid entry nodes", () => {
    const nodeA = instantiateTestNode("flow.start", "node-a", { x: 0, y: 0 });
    const runtime = createGraphSubgraphRuntime();
    const analysis = runtime.analyze(createGraphDocument({
      id: "graph-subgraph-runtime-prune",
      nodes: [nodeA],
      subgraphs: [
        {
          id: "subgraph-main",
          title: "Main",
          nodeIds: ["ghost-node", nodeA.id, nodeA.id],
          entryNodeId: "ghost-node",
        },
      ],
    }));

    expect(analysis.normalizedDocument.subgraphs).toEqual([
      expect.objectContaining({
        id: "subgraph-main",
        nodeIds: [nodeA.id],
        entryNodeId: nodeA.id,
      }),
    ]);
    expect(analysis.issues.map((issue) => issue.code)).toEqual([
      "subgraph-nodes-pruned",
      "subgraph-entry-reassigned",
    ]);
  });

  it("removes empty subgraphs and reports overlap errors for non-nested memberships", () => {
    const nodeA = instantiateTestNode("flow.start", "node-a", { x: 0, y: 0 });
    const nodeB = instantiateTestNode("scene.spawn-marker", "node-b", { x: 120, y: 0 });
    const nodeC = instantiateTestNode("flow.wait-signal", "node-c", { x: 240, y: 0 });
    const runtime = createGraphSubgraphRuntime();
    const analysis = runtime.analyze(createGraphDocument({
      id: "graph-subgraph-runtime-overlap",
      nodes: [nodeA, nodeB, nodeC],
      subgraphs: [
        {
          id: "subgraph-empty",
          title: "Empty",
          nodeIds: ["ghost-node"],
          entryNodeId: "ghost-node",
        },
        {
          id: "subgraph-left",
          title: "Left",
          nodeIds: [nodeA.id, nodeB.id],
          entryNodeId: nodeA.id,
        },
        {
          id: "subgraph-right",
          title: "Right",
          nodeIds: [nodeB.id, nodeC.id],
          entryNodeId: nodeB.id,
        },
      ],
    }));

    expect(analysis.normalizedDocument.subgraphs.map((subgraph) => subgraph.id)).toEqual([
      "subgraph-left",
      "subgraph-right",
    ]);
    expect(analysis.issues).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: "empty-subgraph", severity: "warning", subgraphId: "subgraph-empty" }),
      expect.objectContaining({ code: "subgraph-overlap", severity: "error", subgraphId: "subgraph-left" }),
    ]));
  });
});
