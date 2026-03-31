import { describe, expect, it } from "vitest";
import { createGraphDocument } from "../graph/document/graphDocument";
import { createTestDefinitionRegistry, instantiateTestNode } from "../graph/testing/graphTestUtils";
import { createGraphRuntimeBridgeContract } from "../graph/runtime/graphWorkspaceBridge";
import { createSceneViewportBridgeModel } from "./sceneViewportBridge";

describe("sceneViewportBridge", () => {
  it("projects formal bridge markers into viewport marker models", () => {
    const definitions = createTestDefinitionRegistry();
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 48 }, {
      markerId: "marker_boss_gate",
      delaySeconds: 1.5,
      snapToGround: false,
      facingMode: "camera-facing",
    });
    const bridge = createGraphRuntimeBridgeContract(
      createGraphDocument({
        id: "graph-scene-viewport",
        metadata: {
          projectId: "project-alpha",
          sceneId: "scene-main",
        },
        nodes: [markerNode],
      }),
      definitions,
    );

    const model = createSceneViewportBridgeModel(bridge, [], bridge.markers[0].id);

    expect(model.projectId).toBe("project-alpha");
    expect(model.sceneId).toBe("scene-main");
    expect(model.markerCount).toBe(1);
    expect(model.markers).toEqual([
      expect.objectContaining({
        id: bridge.markers[0].id,
        label: "marker_boss_gate",
        selected: true,
        issueCount: 0,
        bindingState: "pending-provider",
      }),
    ]);
  });

  it("marks viewport bridge markers with issue-aware colors and counts", () => {
    const definitions = createTestDefinitionRegistry();
    const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 120, y: 48 }, {
      markerId: "",
      delaySeconds: 0,
      snapToGround: true,
      facingMode: "marker-forward",
    });
    const bridge = createGraphRuntimeBridgeContract(
      createGraphDocument({
        id: "graph-scene-viewport-issues",
        nodes: [markerNode],
      }),
      definitions,
    );

    const model = createSceneViewportBridgeModel(bridge, [
      {
        code: "graph-bridge-project-missing",
        severity: "warning",
        blocking: false,
        message: "当前 bridge contract 尚未指定 Project Id。",
        location: {
          entityKind: "project",
          entityId: bridge.project.graphId,
        },
      },
      {
        code: "graph-bridge-marker-target-missing",
        severity: "error",
        blocking: true,
        message: "Marker 桥接尚未指定 Marker Id。",
        location: {
          entityKind: "marker",
          entityId: bridge.markers[0].id,
        },
      },
    ], null);

    expect(model.warningCount).toBe(1);
    expect(model.errorCount).toBe(1);
    expect(model.markers[0]).toEqual(expect.objectContaining({
      issueCount: 2,
      color: "#c96565",
    }));
  });
});
