import {
  createGraphComment,
  createGraphDocument,
  createGraphEdge,
  createGraphGroup,
  createGraphPoint,
  createGraphPort,
  createGraphSubgraph,
  type GraphComment,
  type GraphDocument,
  type GraphGroup,
  type GraphNode,
  type GraphPort,
  type GraphSubgraph,
  type NodeId,
  type PortId,
} from "../document/graphDocument";
import {
  instantiateGraphNode,
  type GraphDefinitionRegistry,
} from "../definitions/graphDefinitions";
import {
  createReducerGraphCommandBus,
  type GraphCommandBus,
  type GraphCommandBusSnapshot,
  type GraphWorkspaceCommand,
  type GraphWorkspaceRuntimeState,
} from "../commands/graphCommands";
import {
  createInitialGraphViewState,
  type GraphConnectionPreviewState,
  type GraphInteractionState,
  type GraphSelectionState,
  type GraphViewportState,
} from "../state/graphViewState";
import { createDefaultGraphProfile, type GraphProfile } from "../profile/graphProfile";
import { createGraphBehavior, type GraphBehavior } from "./graphBehavior";
import type { GraphSelectionManager } from "./graphSelectionManager";
import { createGraphSelectionManager } from "./graphSelectionManager";
import type { GraphConnectionPolicy } from "./graphConnectionPolicy";
import { createGraphSubgraphRuntime, type GraphSubgraphRuntime } from "./graphSubgraphRuntime";
import { normalizeGraphSelectionState, normalizeGraphWorkspaceRuntimeState } from "./graphRuntimeStateMigration";

export interface GraphWorkspaceRuntime {
  readonly definitions: GraphDefinitionRegistry;
  readonly profile: GraphProfile;
  readonly behavior: GraphBehavior;
  readonly selectionManager: GraphSelectionManager;
  readonly connectionPolicy: GraphConnectionPolicy;
  readonly subgraphRuntime: GraphSubgraphRuntime;

  getState(): GraphWorkspaceRuntimeState;
  getCommandSnapshot(): GraphCommandBusSnapshot;
  execute(command: GraphWorkspaceCommand): GraphWorkspaceRuntimeState;
  undo(): GraphWorkspaceRuntimeState | undefined;
  redo(): GraphWorkspaceRuntimeState | undefined;
  replaceState(state: GraphWorkspaceRuntimeState, options?: { resetHistory?: boolean }): void;
  setSelection(selection: Partial<GraphSelectionState>): GraphWorkspaceRuntimeState;
  patchViewport(patch: Partial<GraphViewportState>): GraphWorkspaceRuntimeState;
  patchInteraction(patch: Partial<GraphInteractionState>): GraphWorkspaceRuntimeState;
  setConnectionPreview(state: GraphConnectionPreviewState): GraphWorkspaceRuntimeState;
  deleteSelection(): GraphWorkspaceRuntimeState;
  disconnectNodeEdges(nodeIds: NodeId[]): GraphWorkspaceRuntimeState;
  disconnectPortEdges(portIds: PortId[]): GraphWorkspaceRuntimeState;
  selectAllNodes(): GraphWorkspaceRuntimeState;
}

export interface CreateGraphWorkspaceRuntimeOptions {
  initialState: GraphWorkspaceRuntimeState;
  definitions: GraphDefinitionRegistry;
  profile?: GraphProfile;
  behavior?: GraphBehavior;
  selectionManager?: GraphSelectionManager;
  connectionPolicy?: GraphConnectionPolicy;
  subgraphRuntime?: GraphSubgraphRuntime;
}

function cloneRuntimeState(state: GraphWorkspaceRuntimeState): GraphWorkspaceRuntimeState {
  return JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState;
}

function createIdFactory() {
  let nodeCounter = 1;
  let edgeCounter = 1;
  let groupCounter = 1;
  let commentCounter = 1;
  let subgraphCounter = 1;

  return {
    nextNodeId() {
      return `node-generated-${nodeCounter++}`;
    },
    nextEdgeId() {
      return `edge-generated-${edgeCounter++}`;
    },
    nextGroupId() {
      return `group-generated-${groupCounter++}`;
    },
    nextCommentId() {
      return `comment-generated-${commentCounter++}`;
    },
    nextSubgraphId() {
      return `subgraph-generated-${subgraphCounter++}`;
    },
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function ensurePayloadRecord(value: unknown): Record<string, unknown> {
  return isRecord(value) ? value : {};
}

function ensureRuntimeStateIntegrity(
  state: GraphWorkspaceRuntimeState,
  selectionManager: GraphSelectionManager,
  subgraphRuntime: GraphSubgraphRuntime = createGraphSubgraphRuntime(),
): GraphWorkspaceRuntimeState {
  return normalizeGraphWorkspaceRuntimeState(state, { selectionManager, subgraphRuntime });
}

function reduceGraphRuntimeState(
  state: GraphWorkspaceRuntimeState,
  command: GraphWorkspaceCommand,
  definitions: GraphDefinitionRegistry,
  selectionManager: GraphSelectionManager,
  connectionPolicy: GraphConnectionPolicy,
  idFactory: ReturnType<typeof createIdFactory>,
  subgraphRuntime: GraphSubgraphRuntime = createGraphSubgraphRuntime(),
): GraphWorkspaceRuntimeState {
  switch (command.type) {
    case "graph.add-node": {
      const definition = definitions.getNode(command.nodeTypeId);
      if (!definition) {
        return state;
      }

      const node = instantiateGraphNode(definition, {
        id: idFactory.nextNodeId(),
        position: command.position,
      });

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: [...state.document.nodes, node],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectNode(node.id),
          },
        },
        selectionManager,
      );
    }

    case "graph.move-nodes": {
      const movedNodeIds = new Set(command.nodeIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: state.document.nodes.map((node) => {
              if (!movedNodeIds.has(node.id)) {
                return node;
              }

              return {
                ...node,
                position: {
                  x: node.position.x + command.delta.x,
                  y: node.position.y + command.delta.y,
                },
              };
            }),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.connect-ports": {
      const evaluation = connectionPolicy.evaluate(state.document, command);
      if (!evaluation.accepted) {
        return ensureRuntimeStateIntegrity(state, selectionManager, subgraphRuntime);
      }

      const filteredEdges = state.document.edges.filter((edge) => !evaluation.displacedEdgeIds.includes(edge.id));
      const edge = createGraphEdge({
        id: idFactory.nextEdgeId(),
        sourceNodeId: evaluation.sourceNodeId,
        sourcePortId: evaluation.sourcePortId,
        targetNodeId: evaluation.targetNodeId,
        targetPortId: evaluation.targetPortId,
      });

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            edges: [...filteredEdges, edge],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectEdge(edge.id),
          },
        },
        selectionManager,
      );
    }

    case "graph.remove-nodes": {
      const removedNodeIds = new Set(command.nodeIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: state.document.nodes.filter((node) => !removedNodeIds.has(node.id)),
            edges: state.document.edges.filter((edge) => {
              return !removedNodeIds.has(edge.sourceNodeId) && !removedNodeIds.has(edge.targetNodeId);
            }),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.remove-edges": {
      const removedEdgeIds = new Set(command.edgeIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            edges: state.document.edges.filter((edge) => !removedEdgeIds.has(edge.id)),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.disconnect-node-edges": {
      const disconnectedNodeIds = new Set(command.nodeIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            edges: state.document.edges.filter((edge) => {
              return !disconnectedNodeIds.has(edge.sourceNodeId) && !disconnectedNodeIds.has(edge.targetNodeId);
            }),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.disconnect-port-edges": {
      const disconnectedPortIds = new Set(command.portIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            edges: state.document.edges.filter((edge) => {
              return !disconnectedPortIds.has(edge.sourcePortId) && !disconnectedPortIds.has(edge.targetPortId);
            }),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.patch-node-payload": {
      const node = state.document.nodes.find((entry) => entry.id === command.nodeId);
      if (!node) {
        return state;
      }

      const nextPayload = {
        ...ensurePayloadRecord(node.payload),
        ...command.payloadPatch,
      };

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: state.document.nodes.map((entry) => entry.id === command.nodeId ? { ...entry, payload: nextPayload } : entry),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.patch-edge-payload": {
      const edge = state.document.edges.find((entry) => entry.id === command.edgeId);
      if (!edge) {
        return state;
      }

      const nextPayload = {
        ...ensurePayloadRecord(edge.payload),
        ...command.payloadPatch,
      };

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            edges: state.document.edges.map((entry) => entry.id === command.edgeId ? { ...entry, payload: nextPayload } : entry),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.add-group": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            groups: [...state.document.groups, command.group],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectGroup(command.group.id),
          },
        },
        selectionManager,
      );
    }

    case "graph.patch-group": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            groups: state.document.groups.map((group) => group.id === command.groupId ? { ...group, ...command.patch } : group),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.remove-groups": {
      const removedIds = new Set(command.groupIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            groups: state.document.groups.filter((group) => !removedIds.has(group.id)),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.add-comment": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            comments: [...state.document.comments, command.comment],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectComment(command.comment.id),
          },
        },
        selectionManager,
      );
    }

    case "graph.patch-comment": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            comments: state.document.comments.map((comment) => comment.id === command.commentId ? { ...comment, ...command.patch } : comment),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.remove-comments": {
      const removedIds = new Set(command.commentIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            comments: state.document.comments.filter((comment) => !removedIds.has(comment.id)),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.add-subgraph": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            subgraphs: [...state.document.subgraphs, command.subgraph],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectSubgraph(command.subgraph.id),
          },
        },
        selectionManager,
      );
    }

    case "graph.patch-subgraph": {
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            subgraphs: state.document.subgraphs.map((subgraph) => subgraph.id === command.subgraphId ? { ...subgraph, ...command.patch } : subgraph),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.remove-subgraphs": {
      const removedIds = new Set(command.subgraphIds);
      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            subgraphs: state.document.subgraphs.filter((subgraph) => !removedIds.has(subgraph.id)),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    case "graph.paste-clipboard": {
      if (command.snapshot.nodes.length === 0) {
        return state;
      }

      const sourceToTargetNodeIds = new Map<NodeId, NodeId>();
      const pastedNodes: GraphNode[] = command.snapshot.nodes.map((nodeSnapshot) => {
        const nextNodeId = idFactory.nextNodeId();
        sourceToTargetNodeIds.set(nodeSnapshot.sourceNodeId, nextNodeId);

        return {
          id: nextNodeId,
          typeId: nodeSnapshot.typeId,
          position: {
            x: nodeSnapshot.position.x + command.offset.x,
            y: nodeSnapshot.position.y + command.offset.y,
          },
          payload: JSON.parse(JSON.stringify(nodeSnapshot.payload)),
          ui: JSON.parse(JSON.stringify(nodeSnapshot.ui)),
          ports: nodeSnapshot.ports.map((port) => {
            return createGraphPort({
              id: `${nextNodeId}:${port.key}`,
              key: port.key,
              name: port.name,
              direction: port.direction,
              kind: port.kind,
              dataType: port.dataType,
              capacity: port.capacity,
            });
          }),
        };
      });

      const pastedNodePortMaps = new Map<NodeId, Map<string, PortId>>();
      pastedNodes.forEach((node) => {
        pastedNodePortMaps.set(node.id, new Map(node.ports.map((port) => [port.key, port.id])));
      });

      const pastedEdges = command.snapshot.edges.flatMap((edgeSnapshot) => {
        const sourceNodeId = sourceToTargetNodeIds.get(edgeSnapshot.sourceNodeId);
        const targetNodeId = sourceToTargetNodeIds.get(edgeSnapshot.targetNodeId);
        if (!sourceNodeId || !targetNodeId) {
          return [];
        }

        const sourcePortId = pastedNodePortMaps.get(sourceNodeId)?.get(edgeSnapshot.sourcePortKey);
        const targetPortId = pastedNodePortMaps.get(targetNodeId)?.get(edgeSnapshot.targetPortKey);
        if (!sourcePortId || !targetPortId) {
          return [];
        }

        return [
          createGraphEdge({
            id: idFactory.nextEdgeId(),
            sourceNodeId,
            sourcePortId,
            targetNodeId,
            targetPortId,
            payload: JSON.parse(JSON.stringify(edgeSnapshot.payload)),
          }),
        ];
      });

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: [...state.document.nodes, ...pastedNodes],
            edges: [...state.document.edges, ...pastedEdges],
          },
          viewState: {
            ...state.viewState,
            selection: selectionManager.selectNodes(pastedNodes.map((node) => node.id)),
          },
        },
        selectionManager,
      );
    }

    case "graph.apply-node-layout": {
      const layoutMap = new Map(command.entries.map((entry) => [entry.nodeId, entry.position]));
      if (layoutMap.size === 0) {
        return state;
      }

      return ensureRuntimeStateIntegrity(
        {
          document: {
            ...state.document,
            nodes: state.document.nodes.map((node) => {
              const position = layoutMap.get(node.id);
              return position ? { ...node, position } : node;
            }),
          },
          viewState: state.viewState,
        },
        selectionManager,
      );
    }

    default:
      return state;
  }
}

export function createBootstrapGraphDocument(definitions: GraphDefinitionRegistry): GraphDocument {
  const startDefinition = definitions.getNode("flow.start");
  const spawnMarkerDefinition = definitions.getNode("scene.spawn-marker");
  const waitSignalDefinition = definitions.getNode("flow.wait-signal");

  if (!startDefinition || !spawnMarkerDefinition || !waitSignalDefinition) {
    throw new Error("默认 Graph 节点定义缺失，无法创建 Graph Workspace 骨架。");
  }

  const startNode = instantiateGraphNode(startDefinition, {
    id: "node-start",
    position: createGraphPoint(96, 120),
  });
  const spawnMarkerNode = instantiateGraphNode(spawnMarkerDefinition, {
    id: "node-spawn-marker",
    position: createGraphPoint(336, 110),
  });
  const waitSignalNode = instantiateGraphNode(waitSignalDefinition, {
    id: "node-wait-signal",
    position: createGraphPoint(628, 122),
  });

  return createGraphDocument({
    id: "graph-workspace-bootstrap",
    metadata: {
      stage: "graph-workspace-skeleton",
      source: "first-batch-bootstrap",
    },
    nodes: [startNode, spawnMarkerNode, waitSignalNode],
    edges: [
      createGraphEdge({
        id: "edge-start-to-spawn",
        sourceNodeId: startNode.id,
        sourcePortId: `${startNode.id}:next`,
        targetNodeId: spawnMarkerNode.id,
        targetPortId: `${spawnMarkerNode.id}:in`,
        payload: {
          label: "进入出生点",
          diagnosticTone: "info",
        },
      }),
      createGraphEdge({
        id: "edge-spawn-to-wait",
        sourceNodeId: spawnMarkerNode.id,
        sourcePortId: `${spawnMarkerNode.id}:completed`,
        targetNodeId: waitSignalNode.id,
        targetPortId: `${waitSignalNode.id}:in`,
        payload: {
          label: "等待战斗信号",
          diagnosticTone: "warning",
        },
      }),
    ],
    groups: [
      createGraphGroup({
        id: "group-bootstrap-flow",
        title: "基础流程",
        nodeIds: [startNode.id, spawnMarkerNode.id],
        color: "rgba(175, 144, 96, 0.16)",
        padding: 36,
      }),
    ],
    comments: [
      createGraphComment({
        id: "comment-bootstrap-note",
        text: "这里后续会与 Scene Viewport 中的 Marker 白模建立联动。",
        position: createGraphPoint(118, 344),
        size: { width: 264, height: 128 },
        tone: "info",
      }),
    ],
    subgraphs: [
      createGraphSubgraph({
        id: "subgraph-bootstrap-main",
        title: "主线子图",
        nodeIds: [startNode.id, spawnMarkerNode.id, waitSignalNode.id],
        entryNodeId: startNode.id,
        description: "主流程收束到 Scene Marker 与等待信号两段逻辑。",
      }),
    ],
  });
}

export function createBootstrapGraphViewState() {
  return createInitialGraphViewState({
    viewport: {
      zoom: 0.92,
      panX: -54,
      panY: 18,
    },
    selection: {
      selectedNodeIds: ["node-spawn-marker"],
      selectedEdgeIds: [],
      selectedGroupIds: [],
      selectedCommentIds: [],
      selectedSubgraphIds: [],
      primarySelectedNodeId: "node-spawn-marker",
      primarySelectedEdgeId: undefined,
      primarySelectedGroupId: undefined,
      primarySelectedCommentId: undefined,
      primarySelectedSubgraphId: undefined,
    },
    interaction: {
      hoveredNodeId: "node-wait-signal",
    },
  });
}

export function createBootstrapGraphWorkspaceRuntimeState(
  definitions: GraphDefinitionRegistry,
): GraphWorkspaceRuntimeState {
  return {
    document: createBootstrapGraphDocument(definitions),
    viewState: createBootstrapGraphViewState(),
  };
}

export function createGraphWorkspaceRuntime(
  options: CreateGraphWorkspaceRuntimeOptions,
): GraphWorkspaceRuntime {
  const { initialState, definitions } = options;
  const profile = options.profile ?? createDefaultGraphProfile();
  const behavior = options.behavior ?? createGraphBehavior({
    topologyPolicy: profile.topologyPolicy,
    connectionPolicy: options.connectionPolicy,
  });
  const selectionManager = options.selectionManager ?? createGraphSelectionManager();
  const connectionPolicy = behavior.connectionPolicy;
  const subgraphRuntime = options.subgraphRuntime ?? createGraphSubgraphRuntime();
  const idFactory = createIdFactory();

  const commandBus: GraphCommandBus = createReducerGraphCommandBus({
    initialState: normalizeGraphWorkspaceRuntimeState(cloneRuntimeState(initialState), {
      selectionManager,
      subgraphRuntime,
    }),
    reduce: (state, command) => {
      return reduceGraphRuntimeState(state, command, definitions, selectionManager, connectionPolicy, idFactory, subgraphRuntime);
    },
  });

  return {
    definitions,
    profile,
    behavior,
    selectionManager,
    connectionPolicy,
    subgraphRuntime,
    getState() {
      return commandBus.getState();
    },
    getCommandSnapshot() {
      return commandBus.getSnapshot();
    },
    execute(command) {
      return commandBus.execute(command);
    },
    undo() {
      return commandBus.undo();
    },
    redo() {
      return commandBus.redo();
    },
    replaceState(state, replaceOptions) {
      commandBus.replaceState(
        normalizeGraphWorkspaceRuntimeState(cloneRuntimeState(state), {
          selectionManager,
          subgraphRuntime,
        }),
        replaceOptions,
      );
    },
    setSelection(selection) {
      const currentState = commandBus.getState();
      const nextState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          selection: selectionManager.replace(normalizeGraphSelectionState(selection), currentState.document),
        },
      };
      commandBus.replaceState(nextState);
      return nextState;
    },
    patchViewport(patch) {
      const currentState = commandBus.getState();
      const nextZoom = patch.zoom ?? currentState.viewState.viewport.zoom;
      const nextState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          viewport: {
            ...currentState.viewState.viewport,
            ...patch,
            zoom: Math.min(Math.max(nextZoom, profile.render.layout.zoomMin), profile.render.layout.zoomMax),
          },
        },
      };
      commandBus.replaceState(nextState);
      return nextState;
    },
    patchInteraction(patch) {
      const currentState = commandBus.getState();
      const nextState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          interaction: {
            ...currentState.viewState.interaction,
            ...patch,
          },
        },
      };
      commandBus.replaceState(nextState);
      return nextState;
    },
    setConnectionPreview(state) {
      const currentState = commandBus.getState();
      const nextState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          connectionPreview: state,
        },
      };
      commandBus.replaceState(nextState);
      return nextState;
    },
    deleteSelection() {
      const currentState = commandBus.getState();
      const selection = currentState.viewState.selection;
      if (selection.selectedNodeIds.length > 0) {
        return this.execute({ type: "graph.remove-nodes", nodeIds: selection.selectedNodeIds });
      }
      if (selection.selectedEdgeIds.length > 0) {
        return this.execute({ type: "graph.remove-edges", edgeIds: selection.selectedEdgeIds });
      }
      if (selection.selectedGroupIds.length > 0) {
        return this.execute({ type: "graph.remove-groups", groupIds: selection.selectedGroupIds });
      }
      if (selection.selectedCommentIds.length > 0) {
        return this.execute({ type: "graph.remove-comments", commentIds: selection.selectedCommentIds });
      }
      if (selection.selectedSubgraphIds.length > 0) {
        return this.execute({ type: "graph.remove-subgraphs", subgraphIds: selection.selectedSubgraphIds });
      }
      return currentState;
    },
    disconnectNodeEdges(nodeIds) {
      return this.execute({ type: "graph.disconnect-node-edges", nodeIds });
    },
    disconnectPortEdges(portIds) {
      return this.execute({ type: "graph.disconnect-port-edges", portIds });
    },
    selectAllNodes() {
      const currentState = commandBus.getState();
      const nextState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          selection: selectionManager.selectAllNodes(currentState.document),
        },
      };
      commandBus.replaceState(nextState);
      return nextState;
    },
  };
}







