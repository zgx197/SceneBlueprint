import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphNode,
  createGraphPoint,
  createGraphPort,
} from "../document/graphDocument";
import { createGraphBehavior } from "./graphBehavior";
import { createGraphTypeCompatibilityRegistry } from "./graphTypeCompatibility";

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

describe("graphBehavior", () => {
  it("shares implicit conversion rules with its connection policy", () => {
    const typeCompatibility = createGraphTypeCompatibilityRegistry();
    typeCompatibility.registerImplicitConversion("marker-ref", "scene-ref");
    const behavior = createGraphBehavior({ typeCompatibility });
    const source = createNode("source", [{ key: "out", direction: "output", kind: "data", dataType: "marker-ref" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "data", dataType: "scene-ref" }]);
    const document = createGraphDocument({
      id: "graph-behavior-conversion",
      nodes: [source, target],
    });

    expect(behavior.typeCompatibility.isCompatible("marker-ref", "scene-ref")).toBe(true);
    expect(
      behavior.connectionPolicy.evaluate(document, {
        sourceNodeId: source.id,
        sourcePortId: "source:out",
        targetNodeId: target.id,
        targetPortId: "target:in",
      }).accepted,
    ).toBe(true);
  });

  it("wires custom validators into the behavior connection policy", () => {
    const behavior = createGraphBehavior({
      validators: [
        {
          id: "reject-target",
          validate(_document, attempt) {
            if (attempt.targetNodeId === "target") {
              return {
                code: "custom-rejected",
                reason: "target rejected",
              };
            }
            return undefined;
          },
        },
      ],
    });
    const source = createNode("source", [{ key: "out", direction: "output", kind: "control" }]);
    const target = createNode("target", [{ key: "in", direction: "input", kind: "control" }]);
    const document = createGraphDocument({
      id: "graph-behavior-validator",
      nodes: [source, target],
    });

    const evaluation = behavior.connectionPolicy.evaluate(document, {
      sourceNodeId: source.id,
      sourcePortId: "source:out",
      targetNodeId: target.id,
      targetPortId: "target:in",
    });

    expect(behavior.validators).toHaveLength(1);
    expect(evaluation.accepted).toBe(false);
    expect(evaluation.code).toBe("custom-rejected");
    expect(evaluation.reason).toBe("target rejected");
  });
});
