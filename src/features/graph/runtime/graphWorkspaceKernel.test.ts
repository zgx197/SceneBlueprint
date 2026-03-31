import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
} from "../document/graphDocument";
import { createInitialGraphViewState } from "../state/graphViewState";
import { createBootstrapGraphWorkspaceRuntimeState } from "./graphWorkspaceRuntime";
import { createGraphWorkspaceKernel } from "./graphWorkspaceKernel";
import {
  createTestDefinitionRegistry,
  createTestTextMeasurer,
  createTestRuntimeState,
  instantiateTestNode,
} from "../testing/graphTestUtils";

describe("graphWorkspaceKernel", () => {
  it("builds snapshot data from a single orchestration entry", () => {
    const definitions = createTestDefinitionRegistry();
    const kernel = createGraphWorkspaceKernel({
      initialState: createBootstrapGraphWorkspaceRuntimeState(definitions),
      definitions,
      textMeasurer: createTestTextMeasurer(),
    });

    const snapshot = kernel.getSnapshot();

    expect(snapshot.state.document.nodes).toHaveLength(3);
    expect(snapshot.frame.summary.nodeCount).toBe(3);
    expect(snapshot.selectionTarget.kind).toBe("graph-node");
    expect(snapshot.commandSnapshot.historyLength).toBe(0);
    expect(snapshot.analysis.rootNodeIds).toContain("node-start");
  });

  it("surfaces export preflight issues from graph topology and subgraph analysis", () => {
    const definitions = createTestDefinitionRegistry();
    const nodeA = instantiateTestNode("flow.wait-signal", "node-a", { x: 0, y: 0 });
    const nodeB = instantiateTestNode("scene.spawn-marker", "node-b", { x: 120, y: 0 });
    const nodeC = instantiateTestNode("flow.wait-signal", "node-c", { x: 240, y: 0 });
    const kernel = createGraphWorkspaceKernel({
      initialState: createTestRuntimeState(
        createGraphDocument({
          id: "graph-kernel-export",
          nodes: [nodeA, nodeB, nodeC],
          edges: [
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
            createGraphEdge({
              id: "edge-c-a",
              sourceNodeId: nodeC.id,
              sourcePortId: `${nodeC.id}:out`,
              targetNodeId: nodeA.id,
              targetPortId: `${nodeA.id}:in`,
            }),
          ],
          subgraphs: [
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
        }),
        createInitialGraphViewState(),
      ),
      definitions,
      textMeasurer: createTestTextMeasurer(),
    });

    const preflight = kernel.validateForExport();
    const exportResult = kernel.compileForExport();

    expect(preflight.valid).toBe(false);
    expect(preflight.issues.map((issue) => issue.code)).toEqual(expect.arrayContaining([
      "graph-cycle-detected",
      "subgraph-overlap",
    ]));
    expect(exportResult.ok).toBe(false);
    expect(exportResult.runtimeContract).toBeNull();
    expect(exportResult.artifact).toBeNull();
  });

  it("surfaces bridge validation issues through the kernel preflight", () => {
    const definitions = createTestDefinitionRegistry();
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-bridge-invalid", { x: 120, y: 0 }, {
      markerId: "",
      delaySeconds: 0,
      snapToGround: true,
      facingMode: "marker-forward",
    });
    const malformedNode = {
      ...markerNode,
      ports: markerNode.ports.filter((port) => port.key !== "marker"),
    };
    const kernel = createGraphWorkspaceKernel({
      initialState: createTestRuntimeState(
        createGraphDocument({
          id: "graph-kernel-bridge-invalid",
          metadata: {
            projectId: "project-alpha",
            sceneId: "scene-main",
          },
          nodes: [malformedNode],
        }),
        createInitialGraphViewState(),
      ),
      definitions,
      textMeasurer: createTestTextMeasurer(),
    });

    const preflight = kernel.validateForExport();
    const exportResult = kernel.compileForExport();

    expect(preflight.valid).toBe(false);
    expect(preflight.blockingIssues.map((issue) => issue.code)).toEqual(expect.arrayContaining([
      "graph-bridge-marker-target-missing",
      "graph-bridge-marker-port-missing",
    ]));
    expect(exportResult.ok).toBe(false);
    expect(exportResult.runtimeContract).toBeNull();
    expect(exportResult.artifact).toBeNull();
  });

  it("produces a runtime contract artifact from the kernel export orchestration", () => {
    const definitions = createTestDefinitionRegistry();
    const kernel = createGraphWorkspaceKernel({
      initialState: createBootstrapGraphWorkspaceRuntimeState(definitions),
      definitions,
      textMeasurer: createTestTextMeasurer(),
    });

    const exportResult = kernel.compileForExport({ generatedAt: "2026-03-31T00:00:00.000Z" });

    expect(exportResult.ok).toBe(true);
    expect(exportResult.validation.valid).toBe(true);
    expect(exportResult.runtimeContract?.schema).toBe("sceneblueprint.graph-runtime.v1");
    expect(exportResult.runtimeContract?.generatedAt).toBe("2026-03-31T00:00:00.000Z");
    expect(exportResult.runtimeContract?.bridge.markers).toEqual([
      expect.objectContaining({
        nodeId: "node-spawn-marker",
        requestedMarkerId: "marker_spawn_a",
      }),
    ]);
    expect(exportResult.runtimeContract?.nodes).toHaveLength(3);
    expect(exportResult.runtimeContract?.nodes.find((node) => node.id === "node-spawn-marker")?.projection.summaryText).toContain("Marker");
    expect(exportResult.artifact?.content).toContain("sceneblueprint.graph-runtime.v1");
  });

  it("delegates execute and undo through the kernel runtime facade", () => {
    const definitions = createTestDefinitionRegistry();
    const kernel = createGraphWorkspaceKernel({
      initialState: createBootstrapGraphWorkspaceRuntimeState(definitions),
      definitions,
      textMeasurer: createTestTextMeasurer(),
    });

    kernel.execute({
      type: "graph.add-node",
      nodeTypeId: "flow.wait-signal",
      position: { x: 800, y: 200 },
    });

    expect(kernel.getState().document.nodes).toHaveLength(4);
    expect(kernel.getCommandSnapshot().historyLength).toBe(1);

    kernel.undo();

    expect(kernel.getState().document.nodes).toHaveLength(3);
    expect(kernel.getCommandSnapshot().historyLength).toBe(0);
  });
});
