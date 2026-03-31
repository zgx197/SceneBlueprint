import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
} from "../document/graphDocument";
import { createInitialGraphViewState } from "../state/graphViewState";
import {
  buildGraphWorkspaceExportResult,
  buildGraphWorkspaceValidation,
  serializeGraphRuntimeContract,
} from "./graphWorkspaceExport";
import {
  createTestDefinitionRegistry,
  createTestRuntimeState,
  instantiateTestNode,
} from "../testing/graphTestUtils";
import { createGraphSubgraphRuntime } from "./graphSubgraphRuntime";

describe("graphWorkspaceExport", () => {
  it("creates a runtime contract and artifact for a valid graph", () => {
    const definitions = createTestDefinitionRegistry();
    const startNode = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 0 });
    const document = createGraphDocument({
      id: "graph-export-valid",
      nodes: [startNode, markerNode],
      edges: [
        createGraphEdge({
          id: "edge-start-marker",
          sourceNodeId: startNode.id,
          sourcePortId: `${startNode.id}:next`,
          targetNodeId: markerNode.id,
          targetPortId: `${markerNode.id}:in`,
        }),
      ],
      subgraphs: [
        {
          id: "subgraph-main",
          title: "Main",
          nodeIds: [startNode.id, markerNode.id],
          entryNodeId: startNode.id,
        },
      ],
    });
    const subgraphAnalysis = createGraphSubgraphRuntime().analyze(document);
    const analysis = {
      topologyPolicy: "dag" as const,
      hasCycle: false,
      topologicalOrder: [startNode.id, markerNode.id],
      rootNodeIds: [startNode.id],
      leafNodeIds: [markerNode.id],
      connectedComponents: [[startNode.id, markerNode.id]],
      subgraphAnalysis,
    };

    const exportResult = buildGraphWorkspaceExportResult({
      state: createTestRuntimeState(document, createInitialGraphViewState()),
      definitions,
      analysis,
      generatedAt: "2026-03-31T00:00:00.000Z",
    });

    expect(exportResult.ok).toBe(true);
    expect(exportResult.validation.valid).toBe(true);
    expect(exportResult.validation.warningCount).toBe(2);
    expect(exportResult.validation.issues.map((issue) => issue.code)).toEqual(expect.arrayContaining([
      "graph-bridge-project-missing",
      "graph-bridge-scene-missing",
    ]));
    expect(exportResult.runtimeContract?.graphId).toBe("graph-export-valid");
    expect(exportResult.runtimeContract?.bridge.markers).toEqual([
      expect.objectContaining({
        nodeId: markerNode.id,
        requestedMarkerId: "marker_spawn_a",
        bindingState: "pending-provider",
      }),
    ]);
    expect(exportResult.runtimeContract?.nodes.find((node) => node.id === markerNode.id)?.projection.summaryText).toContain("Marker");
    expect(exportResult.artifact?.suggestedFileName).toBe("graph-export-valid.runtime.json");
    expect(serializeGraphRuntimeContract(exportResult.runtimeContract!)).toContain("graph-export-valid");
  });

  it("blocks export when structural document issues are detected", () => {
    const definitions = createTestDefinitionRegistry();
    const startNode = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 0 });
    const document = createGraphDocument({
      id: "graph-export-invalid",
      nodes: [startNode, markerNode],
      edges: [
        createGraphEdge({
          id: "edge-invalid",
          sourceNodeId: startNode.id,
          sourcePortId: `${startNode.id}:next`,
          targetNodeId: markerNode.id,
          targetPortId: `${markerNode.id}:missing-port`,
        }),
      ],
    });
    const subgraphAnalysis = createGraphSubgraphRuntime().analyze(document);
    const analysis = {
      topologyPolicy: "dag" as const,
      hasCycle: false,
      topologicalOrder: [startNode.id, markerNode.id],
      rootNodeIds: [startNode.id],
      leafNodeIds: [markerNode.id],
      connectedComponents: [[startNode.id, markerNode.id]],
      subgraphAnalysis,
    };

    const validation = buildGraphWorkspaceValidation(document, definitions, analysis);
    const exportResult = buildGraphWorkspaceExportResult({
      state: createTestRuntimeState(document, createInitialGraphViewState()),
      definitions,
      analysis,
    });

    expect(validation.valid).toBe(false);
    expect(validation.blockingIssues.map((issue) => issue.code)).toContain("graph-edge-target-port-missing");
    expect(exportResult.ok).toBe(false);
    expect(exportResult.runtimeContract).toBeNull();
    expect(exportResult.artifact).toBeNull();
  });

  it("blocks export when bridge target ids or bridge ports are missing", () => {
    const definitions = createTestDefinitionRegistry();
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 0 }, {
      markerId: "   ",
      delaySeconds: 0,
      snapToGround: true,
      facingMode: "marker-forward",
    });
    const malformedNode = {
      ...markerNode,
      ports: markerNode.ports.filter((port) => port.key !== "marker" && port.key !== "completed"),
    };
    const document = createGraphDocument({
      id: "graph-export-bridge-invalid",
      metadata: {
        projectId: "project-alpha",
        sceneId: "scene-main",
      },
      nodes: [malformedNode],
    });
    const subgraphAnalysis = createGraphSubgraphRuntime().analyze(document);
    const analysis = {
      topologyPolicy: "dag" as const,
      hasCycle: false,
      topologicalOrder: [malformedNode.id],
      rootNodeIds: [malformedNode.id],
      leafNodeIds: [malformedNode.id],
      connectedComponents: [[malformedNode.id]],
      subgraphAnalysis,
    };

    const validation = buildGraphWorkspaceValidation(document, definitions, analysis);
    const exportResult = buildGraphWorkspaceExportResult({
      state: createTestRuntimeState(document, createInitialGraphViewState()),
      definitions,
      analysis,
    });

    expect(validation.valid).toBe(false);
    expect(validation.blockingIssues.map((issue) => issue.code)).toEqual(expect.arrayContaining([
      "graph-bridge-marker-target-missing",
      "graph-bridge-marker-port-missing",
      "graph-bridge-marker-completed-port-missing",
    ]));
    expect(exportResult.ok).toBe(false);
    expect(exportResult.runtimeContract).toBeNull();
    expect(exportResult.artifact).toBeNull();
  });
});
