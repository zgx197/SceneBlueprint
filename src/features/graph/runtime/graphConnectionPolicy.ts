import type { EdgeId, GraphDocument, PortId } from "../document/graphDocument";
import type { GraphTopologyPolicy } from "../profile/graphProfile";
import { wouldCreateCycle } from "./graphAlgorithms";
import { createGraphDocumentIndex, type GraphDocumentIndex } from "./graphDocumentIndex";
import {
  createGraphTypeCompatibilityRegistry,
  type GraphTypeCompatibilityRegistry,
} from "./graphTypeCompatibility";

export interface GraphConnectionAttempt {
  sourceNodeId: string;
  sourcePortId: PortId;
  targetNodeId: string;
  targetPortId: PortId;
}

export interface GraphConnectionValidationFailure {
  code:
    | "missing-port"
    | "same-node"
    | "direction-mismatch"
    | "kind-mismatch"
    | "data-type-mismatch"
    | "duplicate-edge"
    | "cycle-detected"
    | "custom-rejected";
  reason: string;
}

export interface GraphConnectionEvaluation extends GraphConnectionAttempt {
  accepted: boolean;
  displacedEdgeIds: EdgeId[];
  reason?: string;
  code?: GraphConnectionValidationFailure["code"];
}

export interface GraphConnectionValidationContext {
  index: GraphDocumentIndex;
  displacedEdgeIds: ReadonlySet<EdgeId>;
  typeCompatibility: GraphTypeCompatibilityRegistry;
  topologyPolicy: GraphTopologyPolicy;
}

export interface GraphConnectionValidator {
  id: string;
  validate(
    document: GraphDocument,
    attempt: GraphConnectionAttempt,
    context: GraphConnectionValidationContext,
  ): GraphConnectionValidationFailure | undefined;
}

export interface GraphConnectionPolicy {
  evaluate(document: GraphDocument, attempt: GraphConnectionAttempt): GraphConnectionEvaluation;
}

export interface CreateGraphConnectionPolicyOptions {
  topologyPolicy?: GraphTopologyPolicy;
  typeCompatibility?: GraphTypeCompatibilityRegistry;
  validators?: GraphConnectionValidator[];
}

function reject(attempt: GraphConnectionAttempt, failure: GraphConnectionValidationFailure): GraphConnectionEvaluation {
  return {
    ...attempt,
    accepted: false,
    displacedEdgeIds: [],
    reason: failure.reason,
    code: failure.code,
  };
}

export function createGraphConnectionPolicy(options: CreateGraphConnectionPolicyOptions = {}): GraphConnectionPolicy {
  const topologyPolicy = options.topologyPolicy ?? "dag";
  const typeCompatibility = options.typeCompatibility ?? createGraphTypeCompatibilityRegistry();
  const validators = options.validators ?? [];

  return {
    evaluate(document, attempt) {
      const index = createGraphDocumentIndex(document);
      let resolvedAttempt: GraphConnectionAttempt = { ...attempt };

      let sourcePort = index.findPortInNode(resolvedAttempt.sourceNodeId, resolvedAttempt.sourcePortId);
      let targetPort = index.findPortInNode(resolvedAttempt.targetNodeId, resolvedAttempt.targetPortId);
      if (!sourcePort || !targetPort) {
        return reject(resolvedAttempt, {
          code: "missing-port",
          reason: "连接端口不存在",
        });
      }

      if (resolvedAttempt.sourceNodeId === resolvedAttempt.targetNodeId) {
        return reject(resolvedAttempt, {
          code: "same-node",
          reason: "不允许节点连接到自身",
        });
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
        return reject(resolvedAttempt, {
          code: "missing-port",
          reason: "连接端口不存在",
        });
      }

      if (sourcePort.direction !== "output" || targetPort.direction !== "input") {
        return reject(resolvedAttempt, {
          code: "direction-mismatch",
          reason: "只允许 output -> input",
        });
      }

      if (sourcePort.kind !== targetPort.kind) {
        return reject(resolvedAttempt, {
          code: "kind-mismatch",
          reason: "端口 kind 不兼容",
        });
      }

      if (sourcePort.kind === "data" && !typeCompatibility.isCompatible(sourcePort.dataType, targetPort.dataType)) {
        return reject(resolvedAttempt, {
          code: "data-type-mismatch",
          reason: "数据端口类型不兼容",
        });
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
        return reject(resolvedAttempt, {
          code: "duplicate-edge",
          reason: "连接已存在",
        });
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

      if (
        topologyPolicy === "dag" &&
        wouldCreateCycle(document, resolvedAttempt.sourceNodeId, resolvedAttempt.targetNodeId, {
          excludeEdgeIds: displacedEdgeIds,
        })
      ) {
        return reject(resolvedAttempt, {
          code: "cycle-detected",
          reason: "当前图配置为 DAG，新增连线会形成环",
        });
      }

      const context: GraphConnectionValidationContext = {
        index,
        displacedEdgeIds,
        typeCompatibility,
        topologyPolicy,
      };

      for (const validator of validators) {
        const failure = validator.validate(document, resolvedAttempt, context);
        if (failure) {
          return reject(resolvedAttempt, failure.code === "custom-rejected"
            ? failure
            : {
                ...failure,
                code: failure.code,
              });
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
