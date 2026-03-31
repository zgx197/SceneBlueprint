import type { GraphRuntimeBridgeContract, GraphRuntimeMarkerBridgeContract } from "../graph/runtime/graphWorkspaceBridge";
import type { GraphWorkspaceIssue } from "../graph/runtime/graphWorkspaceExport";

export interface SceneViewportMarkerModel {
  id: string;
  nodeId: string;
  label: string;
  position: readonly [number, number, number];
  color: string;
  selected: boolean;
  issueCount: number;
  requestedMarkerId: string | null;
  projectId: string | null;
  sceneId: string | null;
  bindingState: GraphRuntimeMarkerBridgeContract["bindingState"];
}

export interface SceneViewportBridgeModel {
  projectId: string | null;
  sceneId: string | null;
  markerCount: number;
  warningCount: number;
  errorCount: number;
  markers: SceneViewportMarkerModel[];
}

function buildMarkerPosition(index: number): readonly [number, number, number] {
  const column = index % 3;
  const row = Math.floor(index / 3);
  return [
    (column - 1) * 2.9,
    0.45 + (index % 2) * 0.18,
    (row - 0.5) * 2.7,
  ] as const;
}

function countIssues(
  issues: GraphWorkspaceIssue[],
  markerId: string,
  sceneId: string | null,
  graphId: string,
): number {
  return issues.filter((issue) => {
    if (issue.location.entityKind === "marker" && issue.location.entityId === markerId) {
      return true;
    }

    if (sceneId && issue.location.entityKind === "scene" && issue.location.entityId === sceneId) {
      return true;
    }

    return issue.location.entityKind === "project" && issue.location.entityId === graphId;
  }).length;
}

function readMarkerColor(issueCount: number, selected: boolean): string {
  if (selected) {
    return "#df9b49";
  }

  if (issueCount > 0) {
    return "#c96565";
  }

  return "#51a7d9";
}

export function createSceneViewportBridgeModel(
  bridge: GraphRuntimeBridgeContract,
  issues: GraphWorkspaceIssue[],
  selectedMarkerId: string | null,
): SceneViewportBridgeModel {
  const warningCount = issues.filter((issue) => issue.severity === "warning").length;
  const errorCount = issues.filter((issue) => issue.severity === "error").length;

  const markers = bridge.markers.map((marker, index) => {
    const scene = bridge.scenes.find((entry) => entry.id === marker.sceneBindingId) ?? null;
    const issueCount = countIssues(issues, marker.id, scene?.id ?? null, bridge.project.graphId);
    const selected = marker.id === selectedMarkerId;

    return {
      id: marker.id,
      nodeId: marker.nodeId,
      label: marker.requestedMarkerId ?? `Unbound Marker ${index + 1}`,
      position: buildMarkerPosition(index),
      color: readMarkerColor(issueCount, selected),
      selected,
      issueCount,
      requestedMarkerId: marker.requestedMarkerId,
      projectId: marker.projectId,
      sceneId: scene?.requestedSceneId ?? null,
      bindingState: marker.bindingState,
    } satisfies SceneViewportMarkerModel;
  });

  return {
    projectId: bridge.project.requestedProjectId,
    sceneId: bridge.scenes[0]?.requestedSceneId ?? null,
    markerCount: markers.length,
    warningCount,
    errorCount,
    markers,
  };
}
