import Fuse from "fuse.js";
import { projectGraphNodeContent, readGraphNodePayloadRecord } from "../content/graphNodeContent";
import type { GraphDocument, NodeId } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";

export interface GraphNodeSearchResult {
  nodeId: NodeId;
  title: string;
  typeId: string;
  category?: string;
  description?: string;
  summaryText?: string;
  detailText: string;
  score?: number;
}

export interface GraphNodeSearchService {
  search(document: GraphDocument, query: string, options?: { limit?: number }): GraphNodeSearchResult[];
}

interface CreateGraphNodeSearchServiceOptions {
  definitions: GraphDefinitionRegistry;
}

interface SearchableGraphNode extends GraphNodeSearchResult {
  keywords: string[];
}

function buildSearchableNodes(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
): SearchableGraphNode[] {
  return document.nodes.map((node) => {
    const definition = definitions.getNode(node.typeId);
    const payload = readGraphNodePayloadRecord(node.payload);
    const contentProjection = definition
      ? projectGraphNodeContent(definition.content, payload)
      : { summaryText: undefined, detailLines: [] };
    const detailText = contentProjection.detailLines.map((line) => `${line.label} ${line.value}`).join(" ");

    return {
      nodeId: node.id,
      title: definition?.displayName ?? node.typeId,
      typeId: node.typeId,
      category: definition?.category,
      description: definition?.description,
      summaryText: contentProjection.summaryText,
      detailText,
      keywords: [
        node.id,
        node.typeId,
        definition?.displayName ?? "",
        definition?.category ?? "",
        definition?.description ?? "",
        contentProjection.summaryText ?? "",
        detailText,
      ].filter(Boolean),
    };
  });
}

export function createGraphNodeSearchService(options: CreateGraphNodeSearchServiceOptions): GraphNodeSearchService {
  const { definitions } = options;

  return {
    search(document, query, searchOptions) {
      const normalizedQuery = query.trim();
      if (!normalizedQuery) {
        return [];
      }

      const searchableNodes = buildSearchableNodes(document, definitions);
      const fuse = new Fuse(searchableNodes, {
        includeScore: true,
        threshold: 0.38,
        ignoreLocation: true,
        keys: [
          { name: "nodeId", weight: 0.16 },
          { name: "title", weight: 0.32 },
          { name: "typeId", weight: 0.16 },
          { name: "category", weight: 0.08 },
          { name: "description", weight: 0.08 },
          { name: "summaryText", weight: 0.14 },
          { name: "detailText", weight: 0.06 },
        ],
      });

      return fuse.search(normalizedQuery, { limit: searchOptions?.limit ?? 8 }).map((result) => ({
        nodeId: result.item.nodeId,
        title: result.item.title,
        typeId: result.item.typeId,
        category: result.item.category,
        description: result.item.description,
        summaryText: result.item.summaryText,
        detailText: result.item.detailText,
        score: result.score,
      }));
    },
  };
}
