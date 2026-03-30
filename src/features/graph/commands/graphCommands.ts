import type { GraphDocument, EdgeId, NodeId, PortId } from "../document/graphDocument";
import type { GraphClipboardSnapshot } from "../runtime/graphClipboard";
import type { GraphViewState } from "../state/graphViewState";

export interface GraphCommand {
  type: string;
}

export interface AddNodeCommand extends GraphCommand {
  type: "graph.add-node";
  nodeTypeId: string;
  position: { x: number; y: number };
}

export interface MoveNodesCommand extends GraphCommand {
  type: "graph.move-nodes";
  nodeIds: NodeId[];
  delta: { x: number; y: number };
}

export interface ConnectPortsCommand extends GraphCommand {
  type: "graph.connect-ports";
  sourceNodeId: NodeId;
  sourcePortId: PortId;
  targetNodeId: NodeId;
  targetPortId: PortId;
}

export interface RemoveNodesCommand extends GraphCommand {
  type: "graph.remove-nodes";
  nodeIds: NodeId[];
}

export interface RemoveEdgesCommand extends GraphCommand {
  type: "graph.remove-edges";
  edgeIds: EdgeId[];
}

export interface DisconnectNodeEdgesCommand extends GraphCommand {
  type: "graph.disconnect-node-edges";
  nodeIds: NodeId[];
}

export interface DisconnectPortEdgesCommand extends GraphCommand {
  type: "graph.disconnect-port-edges";
  portIds: PortId[];
}

export interface PatchNodePayloadCommand extends GraphCommand {
  type: "graph.patch-node-payload";
  nodeId: NodeId;
  payloadPatch: Record<string, unknown>;
}

export interface PasteGraphClipboardCommand extends GraphCommand {
  type: "graph.paste-clipboard";
  snapshot: GraphClipboardSnapshot;
  offset: { x: number; y: number };
}

export interface ApplyNodeLayoutCommand extends GraphCommand {
  type: "graph.apply-node-layout";
  entries: Array<{
    nodeId: NodeId;
    position: { x: number; y: number };
  }>;
}

export type GraphWorkspaceCommand =
  | AddNodeCommand
  | MoveNodesCommand
  | ConnectPortsCommand
  | RemoveNodesCommand
  | RemoveEdgesCommand
  | DisconnectNodeEdgesCommand
  | DisconnectPortEdgesCommand
  | PatchNodePayloadCommand
  | PasteGraphClipboardCommand
  | ApplyNodeLayoutCommand;

export interface GraphWorkspaceRuntimeState {
  document: GraphDocument;
  viewState: GraphViewState;
}

export interface GraphCommandHistoryEntry {
  sequence: number;
  label: string;
  timestamp: string;
  command: GraphWorkspaceCommand;
}

export interface GraphCommandBusSnapshot {
  history: GraphCommandHistoryEntry[];
  redoHistory: GraphCommandHistoryEntry[];
  canUndo: boolean;
  canRedo: boolean;
  historyLength: number;
  redoLength: number;
  lastCommandLabel?: string;
}

export interface GraphCommandBus {
  execute(command: GraphWorkspaceCommand): GraphWorkspaceRuntimeState;
  undo(): GraphWorkspaceRuntimeState | undefined;
  redo(): GraphWorkspaceRuntimeState | undefined;
  replaceState(state: GraphWorkspaceRuntimeState, options?: { resetHistory?: boolean }): void;
  getState(): GraphWorkspaceRuntimeState;
  getSnapshot(): GraphCommandBusSnapshot;
}

export interface CreateReducerGraphCommandBusOptions {
  initialState: GraphWorkspaceRuntimeState;
  reduce: (
    state: GraphWorkspaceRuntimeState,
    command: GraphWorkspaceCommand,
  ) => GraphWorkspaceRuntimeState;
}

interface GraphCommandHistoryFrame {
  entry: GraphCommandHistoryEntry;
  before: GraphWorkspaceRuntimeState;
  after: GraphWorkspaceRuntimeState;
}

export const graphWorkspaceCommandTypes: GraphWorkspaceCommand["type"][] = [
  "graph.add-node",
  "graph.move-nodes",
  "graph.connect-ports",
  "graph.remove-nodes",
  "graph.remove-edges",
  "graph.disconnect-node-edges",
  "graph.disconnect-port-edges",
  "graph.patch-node-payload",
  "graph.paste-clipboard",
  "graph.apply-node-layout",
];

function cloneRuntimeState(state: GraphWorkspaceRuntimeState): GraphWorkspaceRuntimeState {
  return JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState;
}

function serializeRuntimeState(state: GraphWorkspaceRuntimeState): string {
  return JSON.stringify(state);
}

function createHistoryEntry(command: GraphWorkspaceCommand, sequence: number): GraphCommandHistoryEntry {
  return {
    sequence,
    label: command.type,
    timestamp: new Date().toISOString(),
    command,
  };
}

export function createReducerGraphCommandBus(
  options: CreateReducerGraphCommandBusOptions,
): GraphCommandBus {
  const { initialState, reduce } = options;

  let currentState = cloneRuntimeState(initialState);
  let sequence = 0;
  const history: GraphCommandHistoryFrame[] = [];
  const redoHistory: GraphCommandHistoryFrame[] = [];

  return {
    execute(command) {
      const before = cloneRuntimeState(currentState);
      const after = cloneRuntimeState(reduce(before, command));

      if (serializeRuntimeState(before) === serializeRuntimeState(after)) {
        return cloneRuntimeState(currentState);
      }

      sequence += 1;
      history.push({
        entry: createHistoryEntry(command, sequence),
        before,
        after,
      });
      redoHistory.length = 0;
      currentState = after;
      return cloneRuntimeState(currentState);
    },
    undo() {
      const frame = history.pop();
      if (!frame) {
        return undefined;
      }

      redoHistory.push(frame);
      currentState = cloneRuntimeState(frame.before);
      return cloneRuntimeState(currentState);
    },
    redo() {
      const frame = redoHistory.pop();
      if (!frame) {
        return undefined;
      }

      history.push(frame);
      currentState = cloneRuntimeState(frame.after);
      return cloneRuntimeState(currentState);
    },
    replaceState(state, options) {
      currentState = cloneRuntimeState(state);
      if (options?.resetHistory) {
        history.length = 0;
        redoHistory.length = 0;
      }
    },
    getState() {
      return cloneRuntimeState(currentState);
    },
    getSnapshot() {
      return {
        history: history.map((frame) => frame.entry),
        redoHistory: redoHistory.map((frame) => frame.entry),
        canUndo: history.length > 0,
        canRedo: redoHistory.length > 0,
        historyLength: history.length,
        redoLength: redoHistory.length,
        lastCommandLabel: history.at(-1)?.entry.label,
      };
    },
  };
}
