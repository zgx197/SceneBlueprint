import type { GraphPoint, NodeId } from "../document/graphDocument";
import type { MoveNodesCommand } from "../commands/graphCommands";
import type { GraphFrame } from "../frame/graphFrame";
import { buildGraphBezierGeometry } from "../frame/graphEdgeGeometry";

export interface GraphNodeDragSession {
  nodeIds: NodeId[];
  startWorld: GraphPoint;
  delta: GraphPoint;
}

export function createGraphNodeDragSession(nodeIds: NodeId[], startWorld: GraphPoint): GraphNodeDragSession {
  return {
    nodeIds,
    startWorld,
    delta: { x: 0, y: 0 },
  };
}

export function updateGraphNodeDragSession(
  session: GraphNodeDragSession,
  currentWorld: GraphPoint,
): GraphNodeDragSession {
  return {
    ...session,
    delta: {
      x: Math.round(currentWorld.x - session.startWorld.x),
      y: Math.round(currentWorld.y - session.startWorld.y),
    },
  };
}

export function buildGraphNodeMoveCommand(session: GraphNodeDragSession): MoveNodesCommand | null {
  if (session.delta.x === 0 && session.delta.y === 0) {
    return null;
  }

  return {
    type: "graph.move-nodes",
    nodeIds: session.nodeIds,
    delta: session.delta,
  };
}

export function applyGraphNodeDragPreview(frame: GraphFrame, session: GraphNodeDragSession | null): GraphFrame {
  if (!session || session.nodeIds.length === 0) {
    return frame;
  }

  const draggedNodeIdSet = new Set(session.nodeIds);
  const nodes = frame.nodes.map((node) => {
    if (!draggedNodeIdSet.has(node.id)) {
      return node;
    }

    const bounds = {
      ...node.bounds,
      x: node.bounds.x + session.delta.x,
      y: node.bounds.y + session.delta.y,
    };

    const inputs = node.inputs.map((port) => ({
      ...port,
      anchor: {
        x: port.anchor.x + session.delta.x,
        y: port.anchor.y + session.delta.y,
      },
    }));
    const outputs = node.outputs.map((port) => ({
      ...port,
      anchor: {
        x: port.anchor.x + session.delta.x,
        y: port.anchor.y + session.delta.y,
      },
    }));

    const inputMap = new Map(inputs.map((port) => [port.id, port]));
    const outputMap = new Map(outputs.map((port) => [port.id, port]));

    return {
      ...node,
      bounds,
      inputs,
      outputs,
      rows: node.rows.map((row) => ({
        input: row.input ? inputMap.get(row.input.id) : undefined,
        output: row.output ? outputMap.get(row.output.id) : undefined,
      })),
    };
  });

  const portMap = new Map<string, { anchor: GraphPoint }>();
  for (const node of nodes) {
    for (const port of [...node.inputs, ...node.outputs]) {
      portMap.set(port.id, port);
    }
  }

  const edges = frame.edges.map((edge) => {
    const sourcePort = portMap.get(edge.sourcePortId);
    const targetPort = portMap.get(edge.targetPortId);
    if (!sourcePort || !targetPort) {
      return edge;
    }

    const geometry = buildGraphBezierGeometry(sourcePort.anchor, targetPort.anchor);
    return {
      ...edge,
      start: sourcePort.anchor,
      end: targetPort.anchor,
      path: geometry.path,
      midpoint: geometry.midpoint,
    };
  });

  const overlays = frame.overlays.map((overlay) => {
    if (overlay.kind !== "connection-preview") {
      return overlay;
    }

    const sourcePort = portMap.get(overlay.sourcePortId);
    if (!sourcePort) {
      return overlay;
    }

    /**
     * 拖拽预览阶段也必须同步重算预览线起点。
     * 否则节点与正式边都跟着端口走了，但橙色预览线还停留在旧锚点，
     * 看起来就像“连线不是从端口发出的”。
     */
    const geometry = buildGraphBezierGeometry(sourcePort.anchor, overlay.end);
    return {
      ...overlay,
      start: sourcePort.anchor,
      path: geometry.path,
    };
  });

  return {
    ...frame,
    nodes,
    edges,
    overlays,
    summary: frame.summary,
  };
}
