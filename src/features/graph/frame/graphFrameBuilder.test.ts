import { describe, expect, it } from "vitest";
import { createGraphConnectionPolicy } from "../runtime/graphConnectionPolicy";
import {
  createBootstrapGraphWorkspaceRuntimeState,
} from "../runtime/graphWorkspaceRuntime";
import {
  createTestDefinitionRegistry,
  createTestTextMeasurer,
} from "../testing/graphTestUtils";
import { createInitialGraphViewState } from "../state/graphViewState";
import { createDefaultGraphProfile } from "../profile/graphProfile";
import { createGraphFrameBuilder } from "./graphFrameBuilder";

describe("graphFrameBuilder", () => {
  it("projects stable frame layers for nodes, annotations, labels, diagnostics, and minimap", () => {
    const definitions = createTestDefinitionRegistry();
    const runtimeState = createBootstrapGraphWorkspaceRuntimeState(definitions);
    const frame = createGraphFrameBuilder({
      profile: createDefaultGraphProfile(),
      connectionPolicy: createGraphConnectionPolicy(),
      textMeasurer: createTestTextMeasurer(),
    }).build(runtimeState.document, runtimeState.viewState, definitions);

    expect(frame.nodes).toHaveLength(3);
    expect(frame.edges).toHaveLength(2);
    expect(frame.groups).toHaveLength(1);
    expect(frame.comments).toHaveLength(1);
    expect(frame.subgraphs).toHaveLength(1);
    expect(frame.edges.map((edge) => edge.label?.text)).toEqual(["进入出生点", "等待战斗信号"]);
    expect(frame.decorations.map((decoration) => decoration.tone)).toEqual(["info", "warning"]);
    expect(frame.minimap?.enabled).toBe(true);
    expect(frame.minimap?.viewportRect).not.toBeNull();
    expect(frame.summary).toEqual(
      expect.objectContaining({
        nodeCount: 3,
        edgeCount: 2,
        hasActiveConnectionPreview: false,
      }),
    );
  });

  it("marks connectable input ports and emits preview overlays during connection drag", () => {
    const definitions = createTestDefinitionRegistry();
    const runtimeState = createBootstrapGraphWorkspaceRuntimeState(definitions);
    const viewState = createInitialGraphViewState({
      viewport: runtimeState.viewState.viewport,
      selection: runtimeState.viewState.selection,
      connectionPreview: {
        active: true,
        fromNodeId: "node-start",
        fromPortId: "node-start:next",
        pointer: { x: 540, y: 180 },
      },
      interaction: {
        hoveredPortId: "node-wait-signal:in",
      },
    });

    const frame = createGraphFrameBuilder({
      profile: createDefaultGraphProfile(),
      connectionPolicy: createGraphConnectionPolicy(),
      textMeasurer: createTestTextMeasurer(),
    }).build(runtimeState.document, viewState, definitions);

    const waitSignalNode = frame.nodes.find((node) => node.id === "node-wait-signal");
    const startNode = frame.nodes.find((node) => node.id === "node-start");

    expect(waitSignalNode?.inputs.find((port) => port.id === "node-wait-signal:in")?.connectable).toBe(true);
    expect(startNode?.outputs.find((port) => port.id === "node-start:next")?.source).toBe(true);
    expect(frame.overlays).toEqual([
      expect.objectContaining({
        kind: "connection-preview",
        sourceNodeId: "node-start",
        sourcePortId: "node-start:next",
      }),
    ]);
  });
});

