import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
  createGraphNode,
  createGraphPort,
  createGraphPoint,
} from "../document/graphDocument";
import { createGraphConnectionPolicy } from "./graphConnectionPolicy";

function createNode(
  id: string,
  ports: Array<{
    key: string;
    direction: "input" | "output";
    kind: "control" | "event" | "data";
    dataType?: string;
    capacity?: "single" | "multiple";
  }>,
) {
  return createGraphNode({
    id,
    typeId: `test.${id}`,
    position: createGraphPoint(0, 0),
    ports: ports.map((port) =>
      createGraphPort({
        id: `${id}:${port.key}`,
        key: port.key,
        name: port.key,
        direction: port.direction,
        kind: port.kind,
        dataType: port.dataType,
        capacity: port.capacity ?? "single",
      })),
  });
}

describe("graphConnectionPolicy", () => {
  it("accepts reversed drag order by normalizing to output -> input", () => {
    const source = createNode("source", [{ key: "out", direction: "output", kind: "control" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "control" }]);
    const document = createGraphDocument({
      id: "graph",
      nodes: [source, target],
    });

    const evaluation = createGraphConnectionPolicy().evaluate(document, {
      sourceNodeId: "target",
      sourcePortId: "target:in",
      targetNodeId: "source",
      targetPortId: "source:out",
    });

    expect(evaluation).toEqual(
      expect.objectContaining({
        accepted: true,
        sourceNodeId: "source",
        sourcePortId: "source:out",
        targetNodeId: "target",
        targetPortId: "target:in",
      }),
    );
  });

  it("rejects incompatible data ports", () => {
    const source = createNode("source", [{ key: "out", direction: "output", kind: "data", dataType: "marker-ref" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "data", dataType: "signal-tag" }]);
    const document = createGraphDocument({
      id: "graph",
      nodes: [source, target],
    });

    const evaluation = createGraphConnectionPolicy().evaluate(document, {
      sourceNodeId: "source",
      sourcePortId: "source:out",
      targetNodeId: "target",
      targetPortId: "target:in",
    });

    expect(evaluation.accepted).toBe(false);
    expect(evaluation.code).toBe("data-type-mismatch");
  });

  it("rejects duplicate edges", () => {
    const source = createNode("source", [{ key: "out", direction: "output", kind: "control" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "control" }]);
    const document = createGraphDocument({
      id: "graph",
      nodes: [source, target],
      edges: [
        createGraphEdge({
          id: "edge-1",
          sourceNodeId: "source",
          sourcePortId: "source:out",
          targetNodeId: "target",
          targetPortId: "target:in",
        }),
      ],
    });

    const evaluation = createGraphConnectionPolicy().evaluate(document, {
      sourceNodeId: "source",
      sourcePortId: "source:out",
      targetNodeId: "target",
      targetPortId: "target:in",
    });

    expect(evaluation.accepted).toBe(false);
    expect(evaluation.code).toBe("duplicate-edge");
  });

  it("detects DAG cycles before accepting a connection", () => {
    const nodeA = createNode("a", [
      { key: "in", direction: "input", kind: "control", capacity: "multiple" },
      { key: "out", direction: "output", kind: "control", capacity: "multiple" },
    ]);
    const nodeB = createNode("b", [
      { key: "in", direction: "input", kind: "control", capacity: "multiple" },
      { key: "out", direction: "output", kind: "control", capacity: "multiple" },
    ]);
    const nodeC = createNode("c", [
      { key: "in", direction: "input", kind: "control", capacity: "multiple" },
      { key: "out", direction: "output", kind: "control", capacity: "multiple" },
    ]);
    const document = createGraphDocument({
      id: "graph",
      nodes: [nodeA, nodeB, nodeC],
      edges: [
        createGraphEdge({
          id: "edge-a-b",
          sourceNodeId: "a",
          sourcePortId: "a:out",
          targetNodeId: "b",
          targetPortId: "b:in",
        }),
        createGraphEdge({
          id: "edge-b-c",
          sourceNodeId: "b",
          sourcePortId: "b:out",
          targetNodeId: "c",
          targetPortId: "c:in",
        }),
      ],
    });

    const evaluation = createGraphConnectionPolicy().evaluate(document, {
      sourceNodeId: "c",
      sourcePortId: "c:out",
      targetNodeId: "a",
      targetPortId: "a:in",
    });

    expect(evaluation.accepted).toBe(false);
    expect(evaluation.code).toBe("cycle-detected");
  });

  it("reports displaced edges when single-capacity ports are reused", () => {
    const sourceA = createNode("source-a", [{ key: "out", direction: "output", kind: "control" }]);
    const sourceB = createNode("source-b", [{ key: "out", direction: "output", kind: "control" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "control" }]);
    const document = createGraphDocument({
      id: "graph",
      nodes: [sourceA, sourceB, target],
      edges: [
        createGraphEdge({
          id: "edge-existing",
          sourceNodeId: "source-a",
          sourcePortId: "source-a:out",
          targetNodeId: "target",
          targetPortId: "target:in",
        }),
      ],
    });

    const evaluation = createGraphConnectionPolicy().evaluate(document, {
      sourceNodeId: "source-b",
      sourcePortId: "source-b:out",
      targetNodeId: "target",
      targetPortId: "target:in",
    });

    expect(evaluation.accepted).toBe(true);
    expect(evaluation.displacedEdgeIds).toEqual(["edge-existing"]);
  });
});

