import {
  readGraphNodePayloadRecord,
  projectGraphNodeContent,
} from "../content/graphNodeContent";
import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type {
  GraphComment,
  GraphDocument,
  GraphEdge,
  GraphGroup,
  GraphNode,
  GraphNodeUiState,
  GraphPort,
  GraphSubgraph,
} from "../document/graphDocument";
import type { GraphTopologyPolicy } from "../profile/graphProfile";
import { GRAPH_DOCUMENT_FILE_SCHEMA } from "../serialization/graphDocumentFile";
import { createGraphDocumentIndex } from "./graphDocumentIndex";
import { createGraphRuntimeBridgeContract, type GraphRuntimeBridgeContract } from "./graphWorkspaceBridge";
import type { GraphSubgraphAnalysis } from "./graphSubgraphRuntime";

export const GRAPH_RUNTIME_CONTRACT_SCHEMA = "sceneblueprint.graph-runtime.v1" as const;

export type GraphWorkspaceIssueSeverity = "info" | "warning" | "error";
export type GraphWorkspaceIssueEntityKind =
  | "graph"
  | "node"
  | "edge"
  | "port"
  | "group"
  | "comment"
  | "subgraph"
  | "scene"
  | "marker"
  | "project";

export interface GraphWorkspaceIssueLocation {
  entityKind: GraphWorkspaceIssueEntityKind;
  entityId?: string;
}

export interface GraphWorkspaceIssue {
  code: string;
  severity: GraphWorkspaceIssueSeverity;
  blocking: boolean;
  message: string;
  location: GraphWorkspaceIssueLocation;
}

export interface GraphWorkspaceAnalysisSnapshot {
  topologyPolicy: GraphTopologyPolicy;
  hasCycle: boolean;
  topologicalOrder: string[] | null;
  rootNodeIds: string[];
  leafNodeIds: string[];
  connectedComponents: string[][];
  subgraphAnalysis: GraphSubgraphAnalysis;
}

export interface GraphRuntimeContractPort {
  id: string;
  key: string;
  name: string;
  direction: GraphPort["direction"];
  kind: GraphPort["kind"];
  dataType?: string;
  capacity: GraphPort["capacity"];
}

export interface GraphRuntimeContractNode {
  id: string;
  typeId: string;
  displayName: string;
  category?: string;
  description?: string;
  position: GraphNode["position"];
  payload: Record<string, unknown>;
  ui?: GraphNodeUiState;
  ports: GraphRuntimeContractPort[];
  projection: ReturnType<typeof projectGraphNodeContent>;
}

export interface GraphRuntimeContractEdge {
  id: string;
  sourceNodeId: string;
  sourcePortId: string;
  targetNodeId: string;
  targetPortId: string;
  payload: Record<string, unknown>;
}

export interface GraphRuntimeContractGroup extends GraphGroup {}

export interface GraphRuntimeContractComment extends GraphComment {}

export interface GraphRuntimeContractSubgraph extends GraphSubgraph {}

export interface GraphRuntimeContractAnalysis {
  topologyPolicy: GraphTopologyPolicy;
  hasCycle: boolean;
  topologicalOrder: string[] | null;
  rootNodeIds: string[];
  leafNodeIds: string[];
  connectedComponents: string[][];
}

export interface GraphRuntimeContract {
  schema: typeof GRAPH_RUNTIME_CONTRACT_SCHEMA;
  sourceSchema: typeof GRAPH_DOCUMENT_FILE_SCHEMA;
  generatedAt: string;
  graphId: string;
  metadata?: Record<string, unknown>;
  analysis: GraphRuntimeContractAnalysis;
  bridge: GraphRuntimeBridgeContract;
  nodes: GraphRuntimeContractNode[];
  edges: GraphRuntimeContractEdge[];
  groups: GraphRuntimeContractGroup[];
  comments: GraphRuntimeContractComment[];
  subgraphs: GraphRuntimeContractSubgraph[];
}

export interface GraphWorkspaceValidationResult {
  valid: boolean;
  issues: GraphWorkspaceIssue[];
  blockingIssues: GraphWorkspaceIssue[];
  warningCount: number;
  errorCount: number;
}

export interface GraphWorkspaceExportArtifact {
  format: "json";
  schema: typeof GRAPH_RUNTIME_CONTRACT_SCHEMA;
  suggestedFileName: string;
  content: string;
}

export interface GraphWorkspaceExportResult {
  ok: boolean;
  analysis: GraphWorkspaceAnalysisSnapshot;
  validation: GraphWorkspaceValidationResult;
  issues: GraphWorkspaceIssue[];
  runtimeContract: GraphRuntimeContract | null;
  artifact: GraphWorkspaceExportArtifact | null;
}

interface BuildGraphWorkspaceExportOptions {
  state: GraphWorkspaceRuntimeState;
  definitions: GraphDefinitionRegistry;
  analysis: GraphWorkspaceAnalysisSnapshot;
  generatedAt?: string;
}

function cloneJson<T>(value: T): T {
  if (value === undefined) {
    return value;
  }

  return JSON.parse(JSON.stringify(value)) as T;
}

function sanitizeFileStem(value: string): string {
  const normalized = value.trim().replace(/[^a-zA-Z0-9._-]+/g, "-").replace(/^-+|-+$/g, "");
  return normalized.length > 0 ? normalized : "SceneBlueprint";
}

function createIssue(
  code: string,
  severity: GraphWorkspaceIssueSeverity,
  message: string,
  location: GraphWorkspaceIssueLocation,
  blocking = severity === "error",
): GraphWorkspaceIssue {
  return {
    code,
    severity,
    blocking,
    message,
    location,
  };
}

function collectAnalysisIssues(analysis: GraphWorkspaceAnalysisSnapshot): GraphWorkspaceIssue[] {
  const issues: GraphWorkspaceIssue[] = [];

  if (analysis.topologyPolicy === "dag" && analysis.hasCycle) {
    issues.push(
      createIssue(
        "graph-cycle-detected",
        "error",
        "当前图配置为 DAG，但图中存在环，无法安全导出。",
        { entityKind: "graph" },
      ),
    );
  }

  if (analysis.connectedComponents.length > 1) {
    issues.push(
      createIssue(
        "graph-multiple-components",
        "warning",
        "当前图包含多个连通分量，导出前应确认是否允许孤立流程存在。",
        { entityKind: "graph" },
        false,
      ),
    );
  }

  for (const issue of analysis.subgraphAnalysis.issues) {
    issues.push(
      createIssue(issue.code, issue.severity, issue.message, {
        entityKind: "subgraph",
        entityId: issue.subgraphId,
      }),
    );
  }

  return issues;
}

function collectDocumentIssues(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
): GraphWorkspaceIssue[] {
  const issues: GraphWorkspaceIssue[] = [];
  const index = createGraphDocumentIndex(document);

  for (const node of document.nodes) {
    if (!definitions.getNode(node.typeId)) {
      issues.push(
        createIssue(
          "graph-node-definition-missing",
          "error",
          `节点 ${node.id} 使用了未注册类型 ${node.typeId}，无法导出 runtime contract。`, 
          { entityKind: "node", entityId: node.id },
        ),
      );
    }
  }

  for (const edge of document.edges) {
    const sourceNode = index.findNode(edge.sourceNodeId);
    if (!sourceNode) {
      issues.push(
        createIssue(
          "graph-edge-source-node-missing",
          "error",
          `连线 ${edge.id} 的源节点 ${edge.sourceNodeId} 不存在，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
      continue;
    }

    const targetNode = index.findNode(edge.targetNodeId);
    if (!targetNode) {
      issues.push(
        createIssue(
          "graph-edge-target-node-missing",
          "error",
          `连线 ${edge.id} 的目标节点 ${edge.targetNodeId} 不存在，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
      continue;
    }

    const sourcePort = index.findPortInNode(sourceNode.id, edge.sourcePortId);
    if (!sourcePort) {
      issues.push(
        createIssue(
          "graph-edge-source-port-missing",
          "error",
          `连线 ${edge.id} 的源端口 ${edge.sourcePortId} 不存在于节点 ${sourceNode.id} 上，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
      continue;
    }

    const targetPort = index.findPortInNode(targetNode.id, edge.targetPortId);
    if (!targetPort) {
      issues.push(
        createIssue(
          "graph-edge-target-port-missing",
          "error",
          `连线 ${edge.id} 的目标端口 ${edge.targetPortId} 不存在于节点 ${targetNode.id} 上，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
      continue;
    }

    if (sourcePort.direction !== "output") {
      issues.push(
        createIssue(
          "graph-edge-source-port-direction-invalid",
          "error",
          `连线 ${edge.id} 的源端口 ${sourcePort.id} 不是输出端口，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
    }

    if (targetPort.direction !== "input") {
      issues.push(
        createIssue(
          "graph-edge-target-port-direction-invalid",
          "error",
          `连线 ${edge.id} 的目标端口 ${targetPort.id} 不是输入端口，无法导出。`, 
          { entityKind: "edge", entityId: edge.id },
        ),
      );
    }
  }

  return issues;
}

function collectBridgeIssues(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
): GraphWorkspaceIssue[] {
  const issues: GraphWorkspaceIssue[] = [];
  const bridge = createGraphRuntimeBridgeContract(document, definitions);
  const sceneIds = new Set(bridge.scenes.map((scene) => scene.id));

  if ((bridge.scenes.length > 0 || bridge.markers.length > 0) && bridge.project.requestedProjectId === null) {
    issues.push(
      createIssue(
        "graph-bridge-project-missing",
        "warning",
        "当前 bridge contract 尚未指定 Project Id，Scene / Marker 仍处于待宿主绑定状态。",
        { entityKind: "project", entityId: bridge.project.graphId },
        false,
      ),
    );
  }

  for (const scene of bridge.scenes) {
    if (scene.requestedSceneId === null) {
      issues.push(
        createIssue(
          "graph-bridge-scene-missing",
          "warning",
          `场景桥接 ${scene.id} 尚未指定 Scene Id，当前仍处于待宿主绑定状态。`,
          { entityKind: "scene", entityId: scene.id },
          false,
        ),
      );
    }

    if (scene.projectId !== bridge.project.requestedProjectId) {
      issues.push(
        createIssue(
          "graph-bridge-scene-project-mismatch",
          "error",
          `场景桥接 ${scene.id} 的 Project 绑定与桥接主契约不一致，无法导出。`,
          { entityKind: "scene", entityId: scene.id },
        ),
      );
    }
  }

  for (const marker of bridge.markers) {
    if (!sceneIds.has(marker.sceneBindingId)) {
      issues.push(
        createIssue(
          "graph-bridge-marker-scene-binding-missing",
          "error",
          `Marker 桥接 ${marker.id} 指向了不存在的场景绑定 ${marker.sceneBindingId}，无法导出。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }

    if (marker.projectId !== bridge.project.requestedProjectId) {
      issues.push(
        createIssue(
          "graph-bridge-marker-project-mismatch",
          "error",
          `Marker 桥接 ${marker.id} 的 Project 绑定与桥接主契约不一致，无法导出。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }

    if (marker.requestedMarkerId === null) {
      issues.push(
        createIssue(
          "graph-bridge-marker-target-missing",
          "error",
          `Marker 桥接 ${marker.id} 尚未指定 Marker Id，无法导出正式 bridge contract。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }

    if (marker.markerPortId === null) {
      issues.push(
        createIssue(
          "graph-bridge-marker-port-missing",
          "error",
          `Marker 桥接 ${marker.id} 缺少 marker 数据端口，bridge object 无法成立。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }

    if (marker.inputPortId === null) {
      issues.push(
        createIssue(
          "graph-bridge-marker-input-port-missing",
          "error",
          `Marker 桥接 ${marker.id} 缺少控制输入端口，bridge object 无法成立。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }

    if (marker.completedPortId === null) {
      issues.push(
        createIssue(
          "graph-bridge-marker-completed-port-missing",
          "error",
          `Marker 桥接 ${marker.id} 缺少 completed 输出端口，bridge object 无法成立。`,
          { entityKind: "marker", entityId: marker.id },
        ),
      );
    }
  }

  return issues;
}

export function buildGraphWorkspaceValidation(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
  analysis: GraphWorkspaceAnalysisSnapshot,
): GraphWorkspaceValidationResult {
  const issues = [
    ...collectDocumentIssues(document, definitions),
    ...collectAnalysisIssues(analysis),
    ...collectBridgeIssues(document, definitions),
  ];
  const blockingIssues = issues.filter((issue) => issue.blocking);

  return {
    valid: blockingIssues.length === 0,
    issues,
    blockingIssues,
    warningCount: issues.filter((issue) => issue.severity === "warning").length,
    errorCount: issues.filter((issue) => issue.severity === "error").length,
  };
}
function buildRuntimeContractPort(port: GraphPort): GraphRuntimeContractPort {
  return {
    id: port.id,
    key: port.key,
    name: port.name,
    direction: port.direction,
    kind: port.kind,
    dataType: port.dataType,
    capacity: port.capacity,
  };
}

function buildRuntimeContractNode(
  node: GraphNode,
  definitions: GraphDefinitionRegistry,
): GraphRuntimeContractNode {
  const definition = definitions.getNode(node.typeId);
  if (!definition) {
    throw new Error(`Missing graph definition during export: ${node.typeId}`);
  }

  const payload = readGraphNodePayloadRecord(node.payload);

  return {
    id: node.id,
    typeId: node.typeId,
    displayName: definition.displayName,
    category: definition.category,
    description: definition.description,
    position: cloneJson(node.position),
    payload: cloneJson(payload),
    ui: cloneJson(node.ui),
    ports: node.ports.map((port) => buildRuntimeContractPort(port)),
    projection: projectGraphNodeContent(definition.content, payload),
  };
}

function buildRuntimeContractEdge(edge: GraphEdge): GraphRuntimeContractEdge {
  return {
    id: edge.id,
    sourceNodeId: edge.sourceNodeId,
    sourcePortId: edge.sourcePortId,
    targetNodeId: edge.targetNodeId,
    targetPortId: edge.targetPortId,
    payload: cloneJson(readGraphNodePayloadRecord(edge.payload)),
  };
}

function buildRuntimeContractGroup(group: GraphGroup): GraphRuntimeContractGroup {
  return cloneJson(group);
}

function buildRuntimeContractComment(comment: GraphComment): GraphRuntimeContractComment {
  return cloneJson(comment);
}

function buildRuntimeContractSubgraph(subgraph: GraphSubgraph): GraphRuntimeContractSubgraph {
  return cloneJson(subgraph);
}

export function createGraphRuntimeContract(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
  analysis: GraphWorkspaceAnalysisSnapshot,
  generatedAt = new Date().toISOString(),
): GraphRuntimeContract {
  return {
    schema: GRAPH_RUNTIME_CONTRACT_SCHEMA,
    sourceSchema: GRAPH_DOCUMENT_FILE_SCHEMA,
    generatedAt,
    graphId: document.id,
    metadata: cloneJson(document.metadata),
    analysis: {
      topologyPolicy: analysis.topologyPolicy,
      hasCycle: analysis.hasCycle,
      topologicalOrder: cloneJson(analysis.topologicalOrder),
      rootNodeIds: cloneJson(analysis.rootNodeIds),
      leafNodeIds: cloneJson(analysis.leafNodeIds),
      connectedComponents: cloneJson(analysis.connectedComponents),
    },
    bridge: createGraphRuntimeBridgeContract(document, definitions),
    nodes: document.nodes.map((node) => buildRuntimeContractNode(node, definitions)),
    edges: document.edges.map((edge) => buildRuntimeContractEdge(edge)),
    groups: document.groups.map((group) => buildRuntimeContractGroup(group)),
    comments: document.comments.map((comment) => buildRuntimeContractComment(comment)),
    subgraphs: document.subgraphs.map((subgraph) => buildRuntimeContractSubgraph(subgraph)),
  };
}

export function serializeGraphRuntimeContract(contract: GraphRuntimeContract): string {
  return JSON.stringify(contract, null, 2);
}

export function buildGraphWorkspaceExportResult(
  options: BuildGraphWorkspaceExportOptions,
): GraphWorkspaceExportResult {
  const { definitions, analysis } = options;
  const document = analysis.subgraphAnalysis.normalizedDocument;
  const validation = buildGraphWorkspaceValidation(document, definitions, analysis);

  if (!validation.valid) {
    return {
      ok: false,
      analysis,
      validation,
      issues: validation.issues,
      runtimeContract: null,
      artifact: null,
    };
  }

  const runtimeContract = createGraphRuntimeContract(
    document,
    definitions,
    analysis,
    options.generatedAt,
  );
  const artifact = {
    format: "json",
    schema: runtimeContract.schema,
    suggestedFileName: `${sanitizeFileStem(options.state.document.id)}.runtime.json`,
    content: serializeGraphRuntimeContract(runtimeContract),
  } satisfies GraphWorkspaceExportArtifact;

  return {
    ok: true,
    analysis,
    validation,
    issues: validation.issues,
    runtimeContract,
    artifact,
  };
}

