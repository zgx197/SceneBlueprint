import {
  createGraphDocument,
  createGraphSubgraph,
  type GraphDocument,
  type GraphSubgraph,
} from "../document/graphDocument";
import { createGraphSubgraphIndex, type GraphSubgraphIndex } from "./graphSubgraphIndex";

export type GraphSubgraphIssueCode =
  | "empty-subgraph"
  | "subgraph-nodes-pruned"
  | "subgraph-entry-reassigned"
  | "subgraph-overlap";

export interface GraphSubgraphIssue {
  subgraphId: string;
  code: GraphSubgraphIssueCode;
  severity: "warning" | "error";
  message: string;
}

export interface GraphSubgraphAnalysis {
  readonly normalizedDocument: GraphDocument;
  readonly index: GraphSubgraphIndex;
  readonly issues: GraphSubgraphIssue[];
}

export interface GraphSubgraphRuntime {
  normalizeDocument(document: GraphDocument): GraphDocument;
  analyze(document: GraphDocument): GraphSubgraphAnalysis;
}

function normalizeSingleSubgraph(subgraph: GraphSubgraph, validNodeIds: Set<string>): GraphSubgraph | null {
  const nodeIds = [...new Set(subgraph.nodeIds.filter((nodeId) => validNodeIds.has(nodeId)))];
  if (nodeIds.length === 0) {
    return null;
  }

  const entryNodeId = subgraph.entryNodeId && nodeIds.includes(subgraph.entryNodeId)
    ? subgraph.entryNodeId
    : nodeIds[0];

  return createGraphSubgraph({
    ...subgraph,
    nodeIds,
    entryNodeId,
  });
}

function normalizeGraphSubgraphs(document: GraphDocument): GraphDocument {
  const validNodeIds = new Set(document.nodes.map((node) => node.id));
  const subgraphs = document.subgraphs
    .map((subgraph) => normalizeSingleSubgraph(subgraph, validNodeIds))
    .filter((subgraph): subgraph is GraphSubgraph => subgraph !== null);

  return createGraphDocument({
    ...document,
    subgraphs,
  });
}

export function createGraphSubgraphRuntime(): GraphSubgraphRuntime {
  return {
    normalizeDocument(document) {
      return normalizeGraphSubgraphs(document);
    },
    analyze(document) {
      const normalizedDocument = normalizeGraphSubgraphs(document);
      const issues: GraphSubgraphIssue[] = [];
      const validNodeIds = new Set(document.nodes.map((node) => node.id));

      for (const subgraph of document.subgraphs) {
        const uniqueValidNodeIds = [...new Set(subgraph.nodeIds.filter((nodeId) => validNodeIds.has(nodeId)))];
        if (uniqueValidNodeIds.length === 0) {
          issues.push({
            subgraphId: subgraph.id,
            code: "empty-subgraph",
            severity: "warning",
            message: "子图没有任何有效节点，已在归一化过程中移除。",
          });
          continue;
        }

        if (uniqueValidNodeIds.length !== subgraph.nodeIds.length) {
          issues.push({
            subgraphId: subgraph.id,
            code: "subgraph-nodes-pruned",
            severity: "warning",
            message: "子图包含无效或重复节点，已在归一化过程中裁剪。",
          });
        }

        const nextEntryNodeId = subgraph.entryNodeId && uniqueValidNodeIds.includes(subgraph.entryNodeId)
          ? subgraph.entryNodeId
          : uniqueValidNodeIds[0];
        if (nextEntryNodeId !== subgraph.entryNodeId) {
          issues.push({
            subgraphId: subgraph.id,
            code: "subgraph-entry-reassigned",
            severity: "warning",
            message: "子图入口节点无效，已回退到第一个有效成员节点。",
          });
        }
      }

      for (let index = 0; index < normalizedDocument.subgraphs.length; index += 1) {
        const current = normalizedDocument.subgraphs[index]!;
        const currentNodeSet = new Set(current.nodeIds);
        for (let otherIndex = index + 1; otherIndex < normalizedDocument.subgraphs.length; otherIndex += 1) {
          const other = normalizedDocument.subgraphs[otherIndex]!;
          const otherNodeSet = new Set(other.nodeIds);
          const overlapNodeIds = current.nodeIds.filter((nodeId) => otherNodeSet.has(nodeId));
          if (overlapNodeIds.length === 0) {
            continue;
          }

          const currentIsSubset = current.nodeIds.every((nodeId) => otherNodeSet.has(nodeId));
          const otherIsSubset = other.nodeIds.every((nodeId) => currentNodeSet.has(nodeId));
          if (!currentIsSubset && !otherIsSubset) {
            issues.push({
              subgraphId: current.id,
              code: "subgraph-overlap",
              severity: "error",
              message: `子图 ${current.id} 与 ${other.id} 存在部分重叠，但不是嵌套关系。`,
            });
          }
        }
      }

      return {
        normalizedDocument,
        index: createGraphSubgraphIndex(normalizedDocument),
        issues,
      };
    },
  };
}
