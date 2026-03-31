import type {
  EdgeId,
  GraphComment,
  GraphCommentId,
  GraphDocument,
  GraphGroup,
  GraphGroupId,
  GraphSubgraph,
  GraphSubgraphId,
  NodeId,
  PortId,
} from "../document/graphDocument";
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

export interface PatchEdgePayloadCommand extends GraphCommand {
  type: "graph.patch-edge-payload";
  edgeId: EdgeId;
  payloadPatch: Record<string, unknown>;
}

export interface AddGroupCommand extends GraphCommand {
  type: "graph.add-group";
  group: GraphGroup;
}

export interface PatchGroupCommand extends GraphCommand {
  type: "graph.patch-group";
  groupId: GraphGroupId;
  patch: Partial<GraphGroup>;
}

export interface RemoveGroupsCommand extends GraphCommand {
  type: "graph.remove-groups";
  groupIds: GraphGroupId[];
}

export interface AddCommentCommand extends GraphCommand {
  type: "graph.add-comment";
  comment: GraphComment;
}

export interface PatchCommentCommand extends GraphCommand {
  type: "graph.patch-comment";
  commentId: GraphCommentId;
  patch: Partial<GraphComment>;
}

export interface RemoveCommentsCommand extends GraphCommand {
  type: "graph.remove-comments";
  commentIds: GraphCommentId[];
}

export interface AddSubgraphCommand extends GraphCommand {
  type: "graph.add-subgraph";
  subgraph: GraphSubgraph;
}

export interface PatchSubgraphCommand extends GraphCommand {
  type: "graph.patch-subgraph";
  subgraphId: GraphSubgraphId;
  patch: Partial<GraphSubgraph>;
}

export interface RemoveSubgraphsCommand extends GraphCommand {
  type: "graph.remove-subgraphs";
  subgraphIds: GraphSubgraphId[];
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
  | PatchEdgePayloadCommand
  | AddGroupCommand
  | PatchGroupCommand
  | RemoveGroupsCommand
  | AddCommentCommand
  | PatchCommentCommand
  | RemoveCommentsCommand
  | AddSubgraphCommand
  | PatchSubgraphCommand
  | RemoveSubgraphsCommand
  | PasteGraphClipboardCommand
  | ApplyNodeLayoutCommand;

export interface GraphWorkspaceRuntimeState {
  document: GraphDocument;
  viewState: GraphViewState;
}

export type GraphCommandCategory =
  | "structure"
  | "layout"
  | "connection"
  | "content"
  | "clipboard"
  | "automation"
  | "annotation";

export interface GraphCommandDescriptor {
  label: string;
  category: GraphCommandCategory;
  mergeKey?: string;
  mergeWindowMs?: number;
}

export interface GraphCommandHistoryEntry {
  sequence: number;
  label: string;
  timestamp: string;
  category: GraphCommandCategory;
  command: GraphWorkspaceCommand;
  commands: GraphWorkspaceCommand[];
  commandCount: number;
  mergeKey?: string;
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

export interface GraphCommandBatchOptions {
  label?: string;
  category?: GraphCommandCategory;
  mergeKey?: string;
  mergeWindowMs?: number;
}

export interface GraphCommandBus {
  execute(command: GraphWorkspaceCommand): GraphWorkspaceRuntimeState;
  executeMany(commands: GraphWorkspaceCommand[], options?: GraphCommandBatchOptions): GraphWorkspaceRuntimeState;
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
  maxHistorySize?: number;
}

interface GraphCommandHistoryFrame {
  entry: GraphCommandHistoryEntry;
  before: GraphWorkspaceRuntimeState;
  after: GraphWorkspaceRuntimeState;
  committedAt: number;
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
  "graph.patch-edge-payload",
  "graph.add-group",
  "graph.patch-group",
  "graph.remove-groups",
  "graph.add-comment",
  "graph.patch-comment",
  "graph.remove-comments",
  "graph.add-subgraph",
  "graph.patch-subgraph",
  "graph.remove-subgraphs",
  "graph.paste-clipboard",
  "graph.apply-node-layout",
];

function cloneRuntimeState(state: GraphWorkspaceRuntimeState): GraphWorkspaceRuntimeState {
  return JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState;
}

function serializeRuntimeState(state: GraphWorkspaceRuntimeState): string {
  return JSON.stringify(state);
}

function uniqueSorted(values: string[]): string[] {
  return [...new Set(values)].sort((left, right) => left.localeCompare(right));
}

function describeCommand(command: GraphWorkspaceCommand): GraphCommandDescriptor {
  switch (command.type) {
    case "graph.add-node":
      return { label: "新增节点", category: "structure" };
    case "graph.move-nodes":
      return {
        label: command.nodeIds.length > 1 ? "移动多个节点" : "移动节点",
        category: "layout",
        mergeKey: `graph.move-nodes:${uniqueSorted(command.nodeIds).join(",")}`,
        mergeWindowMs: 1200,
      };
    case "graph.connect-ports":
      return { label: "连接端口", category: "connection" };
    case "graph.remove-nodes":
      return { label: command.nodeIds.length > 1 ? "删除多个节点" : "删除节点", category: "structure" };
    case "graph.remove-edges":
      return { label: command.edgeIds.length > 1 ? "删除多条连线" : "删除连线", category: "connection" };
    case "graph.disconnect-node-edges":
      return { label: "断开节点连线", category: "connection" };
    case "graph.disconnect-port-edges":
      return { label: "断开端点连线", category: "connection" };
    case "graph.patch-node-payload":
      return {
        label: "修改节点内容",
        category: "content",
        mergeKey: `graph.patch-node-payload:${command.nodeId}`,
        mergeWindowMs: 800,
      };
    case "graph.patch-edge-payload":
      return {
        label: "修改连线内容",
        category: "content",
        mergeKey: `graph.patch-edge-payload:${command.edgeId}`,
        mergeWindowMs: 800,
      };
    case "graph.add-group":
      return { label: "新增分组", category: "annotation" };
    case "graph.patch-group":
      return {
        label: "修改分组",
        category: "annotation",
        mergeKey: `graph.patch-group:${command.groupId}`,
        mergeWindowMs: 800,
      };
    case "graph.remove-groups":
      return { label: "删除分组", category: "annotation" };
    case "graph.add-comment":
      return { label: "新增注释", category: "annotation" };
    case "graph.patch-comment":
      return {
        label: "修改注释",
        category: "annotation",
        mergeKey: `graph.patch-comment:${command.commentId}`,
        mergeWindowMs: 800,
      };
    case "graph.remove-comments":
      return { label: "删除注释", category: "annotation" };
    case "graph.add-subgraph":
      return { label: "新增子图", category: "annotation" };
    case "graph.patch-subgraph":
      return {
        label: "修改子图",
        category: "annotation",
        mergeKey: `graph.patch-subgraph:${command.subgraphId}`,
        mergeWindowMs: 800,
      };
    case "graph.remove-subgraphs":
      return { label: "删除子图", category: "annotation" };
    case "graph.paste-clipboard":
      return { label: "粘贴节点", category: "clipboard" };
    case "graph.apply-node-layout":
      return { label: "应用自动布局", category: "automation" };
  }

  const exhaustiveCheck: never = command;
  return {
    label: String(exhaustiveCheck),
    category: "structure",
  };
}

function createHistoryEntry(
  commands: GraphWorkspaceCommand[],
  sequence: number,
  options?: GraphCommandBatchOptions,
): GraphCommandHistoryEntry {
  const descriptor = describeCommand(commands[0]);
  return {
    sequence,
    label: options?.label ?? descriptor.label,
    timestamp: new Date().toISOString(),
    category: options?.category ?? descriptor.category,
    command: commands[commands.length - 1],
    commands: [...commands],
    commandCount: commands.length,
    mergeKey: options?.mergeKey ?? descriptor.mergeKey,
  };
}

function trimHistory(history: GraphCommandHistoryFrame[], maxHistorySize: number) {
  if (history.length <= maxHistorySize) {
    return;
  }

  history.splice(0, history.length - maxHistorySize);
}

function canMergeFrame(
  previousFrame: GraphCommandHistoryFrame | undefined,
  nextEntry: GraphCommandHistoryEntry,
  mergeWindowMs: number,
  committedAt: number,
): boolean {
  if (!previousFrame || !previousFrame.entry.mergeKey || !nextEntry.mergeKey) {
    return false;
  }

  if (previousFrame.entry.mergeKey !== nextEntry.mergeKey) {
    return false;
  }

  return committedAt - previousFrame.committedAt <= mergeWindowMs;
}

export function createReducerGraphCommandBus(
  options: CreateReducerGraphCommandBusOptions,
): GraphCommandBus {
  const { initialState, reduce, maxHistorySize = 120 } = options;

  let currentState = cloneRuntimeState(initialState);
  let sequence = 0;
  const history: GraphCommandHistoryFrame[] = [];
  const redoHistory: GraphCommandHistoryFrame[] = [];

  const executeMany = (commands: GraphWorkspaceCommand[], batchOptions?: GraphCommandBatchOptions) => {
    if (commands.length === 0) {
      return cloneRuntimeState(currentState);
    }

    const before = cloneRuntimeState(currentState);
    const after = commands.reduce((state, command) => {
      return cloneRuntimeState(reduce(state, command));
    }, before);

    if (serializeRuntimeState(before) === serializeRuntimeState(after)) {
      return cloneRuntimeState(currentState);
    }

    sequence += 1;
    const entry = createHistoryEntry(commands, sequence, batchOptions);
    const descriptor = describeCommand(commands[0]);
    const committedAt = Date.now();
    const mergeWindowMs = batchOptions?.mergeWindowMs ?? descriptor.mergeWindowMs ?? 0;
    const previousFrame = history.at(-1);

    if (canMergeFrame(previousFrame, entry, mergeWindowMs, committedAt)) {
      const mergedFrame = previousFrame!;
      mergedFrame.after = after;
      mergedFrame.committedAt = committedAt;
      mergedFrame.entry = {
        ...entry,
        sequence: mergedFrame.entry.sequence,
        commands: [...mergedFrame.entry.commands, ...entry.commands],
        commandCount: mergedFrame.entry.commandCount + entry.commandCount,
      };
      redoHistory.length = 0;
      currentState = after;
      return cloneRuntimeState(currentState);
    }

    history.push({ entry, before, after, committedAt });
    trimHistory(history, maxHistorySize);
    redoHistory.length = 0;
    currentState = after;
    return cloneRuntimeState(currentState);
  };

  return {
    execute(command) {
      return executeMany([command]);
    },
    executeMany(commands, batchOptions) {
      return executeMany(commands, batchOptions);
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
