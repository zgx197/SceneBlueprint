import { describe, expect, it } from "vitest";
import { createGraphDocument } from "./document/graphDocument";
import { defaultGraphNodeDefinitions } from "./definitions/defaultGraphNodeDefinitions";
import {
  createGraphRuntimeBridgeContract,
  GRAPH_RUNTIME_BRIDGE_SCHEMA,
} from "./runtime/graphWorkspaceBridge";
import { createBootstrapGraphWorkspaceRuntimeState } from "./runtime/graphWorkspaceRuntime";
import { createTestDefinitionRegistry, instantiateTestNode } from "./testing/graphTestUtils";
import {
  createGraphDocumentFileEnvelope,
  GRAPH_DOCUMENT_FILE_SCHEMA,
} from "./serialization/graphDocumentFile";

describe("graph bridge contracts", () => {
  it("keeps the minimal Scene Marker binding contract stable on the spawn-marker node", () => {
    const definition = defaultGraphNodeDefinitions.find((entry) => entry.typeId === "scene.spawn-marker");

    expect(definition).toBeDefined();
    expect(definition?.ports).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          key: "marker",
          direction: "input",
          kind: "data",
          dataType: "marker-ref",
        }),
      ]),
    );

    const fieldKeys = definition?.content.sections.flatMap((section) => section.fields.map((field) => field.key)) ?? [];
    expect(fieldKeys).toEqual(
      expect.arrayContaining(["markerId", "bindingState", "delaySeconds", "snapToGround", "facingMode"]),
    );

    const projection = definition?.content.buildProjection((definition.defaultPayload?.() ?? {}) as Record<string, unknown>);
    expect(projection?.summaryText).toContain("Marker：marker_spawn_a");
  });

  it("keeps the stable payload-to-bridge mapping layer on the spawn-marker definition", () => {
    const definitions = createTestDefinitionRegistry();
    const definition = definitions.getNode("scene.spawn-marker");
    const node = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 48 }, {
      markerId: "marker_boss_gate",
      delaySeconds: 1.5,
      snapToGround: false,
      facingMode: "camera-facing",
    });
    const projection = definition?.bridge?.project(node, {
      graphId: "graph-bridge-contract",
      requestedProjectId: "project-alpha",
      requestedSceneId: "scene-main",
      sceneBindingId: "graph-bridge-contract:scene-default",
    });

    expect(projection).toEqual(
      expect.objectContaining({
        kind: "marker",
        id: "bridge-marker:node-marker",
        nodeId: "node-marker",
        requestedMarkerId: "marker_boss_gate",
        sceneBindingId: "graph-bridge-contract:scene-default",
        projectId: "project-alpha",
        delaySeconds: 1.5,
        snapToGround: false,
        facingMode: "camera-facing",
      }),
    );
  });

  it("keeps the formal Scene / Marker / Project bridge contract stable", () => {
    const definitions = createTestDefinitionRegistry();
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 48 }, {
      markerId: "marker_boss_gate",
      delaySeconds: 1.5,
      snapToGround: false,
      facingMode: "camera-facing",
    });
    const contract = createGraphRuntimeBridgeContract(
      createGraphDocument({
        id: "graph-bridge-contract",
        metadata: {
          projectId: "project-alpha",
          sceneId: "scene-main",
        },
        nodes: [markerNode],
      }),
      definitions,
    );

    expect(contract.schema).toBe(GRAPH_RUNTIME_BRIDGE_SCHEMA);
    expect(contract.project).toEqual({
      graphId: "graph-bridge-contract",
      requestedProjectId: "project-alpha",
      source: "document-metadata",
    });
    expect(contract.scenes).toEqual([
      expect.objectContaining({
        requestedSceneId: "scene-main",
        projectId: "project-alpha",
        sourceNodeIds: ["node-marker"],
        bindingState: "pending-provider",
      }),
    ]);
    expect(contract.markers).toEqual([
      expect.objectContaining({
        id: "bridge-marker:node-marker",
        nodeId: "node-marker",
        requestedMarkerId: "marker_boss_gate",
        projectId: "project-alpha",
        delaySeconds: 1.5,
        snapToGround: false,
        facingMode: "camera-facing",
        bindingState: "pending-provider",
      }),
    ]);
  });

  it("keeps the workspace file bridge contract stable for project persistence", () => {
    const definitions = createTestDefinitionRegistry();
    const runtimeState = createBootstrapGraphWorkspaceRuntimeState(definitions);
    const envelope = createGraphDocumentFileEnvelope(runtimeState, "2026-03-30T00:00:00.000Z");

    expect(envelope.schema).toBe(GRAPH_DOCUMENT_FILE_SCHEMA);
    expect(envelope.savedAt).toBe("2026-03-30T00:00:00.000Z");
    expect(envelope.graph.id).toBe("graph-workspace-bootstrap");
    expect(envelope.workspace.viewport).toEqual(runtimeState.viewState.viewport);
    expect(envelope.workspace.selection.primarySelectedNodeId).toBe("node-spawn-marker");
  });
});