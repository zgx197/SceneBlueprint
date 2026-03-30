import type { EdgeId, GraphDocument, NodeId, PortId } from "../document/graphDocument";
import { createGraphDocumentIndex } from "./graphDocumentIndex";

export interface GraphConnectionAttempt {
  sourceNodeId: NodeId;
  sourcePortId: PortId;
  targetNodeId: NodeId;
  targetPortId: PortId;
}

export interface GraphConnectionEvaluation extends GraphConnectionAttempt {
  accepted: boolean;
  displacedEdgeIds: EdgeId[];
  reason?: string;
}

export interface GraphConnectionPolicy {
  evaluate(document: GraphDocument, attempt: GraphConnectionAttempt): GraphConnectionEvaluation;
}

function reject(attempt: GraphConnectionAttempt, reason: string): GraphConnectionEvaluation {
  return {
    ...attempt,
    accepted: false,
    displacedEdgeIds: [],
    reason,
  };
}

export function createGraphConnectionPolicy(): GraphConnectionPolicy {
  return {
    evaluate(document, attempt) {
      const index = createGraphDocumentIndex(document);
      let resolvedAttempt: GraphConnectionAttempt = { ...attempt };

      let sourcePort = index.findPortInNode(resolvedAttempt.sourceNodeId, resolvedAttempt.sourcePortId);
      let targetPort = index.findPortInNode(resolvedAttempt.targetNodeId, resolvedAttempt.targetPortId);
      if (!sourcePort || !targetPort) {
        return reject(resolvedAttempt, "连接端口不存在");
      }

      if (resolvedAttempt.sourceNodeId === resolvedAttempt.targetNodeId) {
        return reject(resolvedAttempt, "不允许节点连接到自身");
      }

      if (sourcePort.direction === "input" && targetPort.direction === "output") {
        resolvedAttempt = {
          sourceNodeId: resolvedAttempt.targetNodeId,
          sourcePortId: resolvedAttempt.targetPortId,
          targetNodeId: resolvedAttempt.sourceNodeId,
          targetPortId: resolvedAttempt.sourcePortId,
        };
        sourcePort = index.findPortInNode(resolvedAttempt.sourceNodeId, resolvedAttempt.sourcePortId);
        targetPort = index.findPortInNode(resolvedAttempt.targetNodeId, resolvedAttempt.targetPortId);
      }

      if (!sourcePort || !targetPort) {
        return reject(resolvedAttempt, "连接端口不存在");
      }

      if (sourcePort.direction !== "output" || targetPort.direction !== "input") {
        return reject(resolvedAttempt, "只允许 output -> input");
      }

      if (sourcePort.kind !== targetPort.kind) {
        return reject(resolvedAttempt, "端口 kind 不兼容");
      }

      if (sourcePort.kind === "data") {
        const sourceType = sourcePort.dataType;
        const targetType = targetPort.dataType;
        if (sourceType && targetType && sourceType !== targetType) {
          return reject(resolvedAttempt, "数据端口类型不兼容");
        }
      }

      const duplicateEdge = document.edges.some((edge) => {
        return (
          edge.sourceNodeId === resolvedAttempt.sourceNodeId &&
          edge.sourcePortId === resolvedAttempt.sourcePortId &&
          edge.targetNodeId === resolvedAttempt.targetNodeId &&
          edge.targetPortId === resolvedAttempt.targetPortId
        );
      });
      if (duplicateEdge) {
        return reject(resolvedAttempt, "连接已存在");
      }

      const displacedEdgeIds = new Set<EdgeId>();
      if (sourcePort.capacity === "single") {
        for (const edge of index.getPortEdges(sourcePort.id)) {
          if (edge.sourcePortId === sourcePort.id) {
            displacedEdgeIds.add(edge.id);
          }
        }
      }

      if (targetPort.capacity === "single") {
        for (const edge of index.getPortEdges(targetPort.id)) {
          if (edge.targetPortId === targetPort.id) {
            displacedEdgeIds.add(edge.id);
          }
        }
      }

      return {
        ...resolvedAttempt,
        accepted: true,
        displacedEdgeIds: [...displacedEdgeIds],
      };
    },
  };
}
