import { describe, expect, it, vi } from "vitest";
import { createGraphDocument } from "../document/graphDocument";
import type { GraphWorkspaceExportResult } from "../runtime/graphWorkspaceExport";
import { GRAPH_RUNTIME_CONTRACT_SCHEMA } from "../runtime/graphWorkspaceExport";
import { createGraphSubgraphRuntime } from "../runtime/graphSubgraphRuntime";
import { createInitialGraphViewState } from "../state/graphViewState";
import {
  deserializeGraphDocumentFile,
  serializeGraphDocumentFile,
} from "../serialization/graphDocumentFile";
import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphWorkspaceStorage, GraphWorkspaceStorageSnapshot } from "../storage/graphWorkspaceStorage";
import { createTestRuntimeState } from "../testing/graphTestUtils";
import {
  exportGraphRuntimeContractToHost,
  hydrateGraphWorkspaceFromHost,
  loadGraphWorkspaceFromHost,
  saveGraphWorkspaceToHost,
} from "./graphWorkspaceHostPersistence";

function createMockStorage() {
  let snapshot: GraphWorkspaceStorageSnapshot = {
    hasSavedSnapshot: false,
    storageKey: "test-storage",
  };
  const savedStates: GraphWorkspaceRuntimeState[] = [];

  const storage: GraphWorkspaceStorage = {
    getSnapshot() {
      return snapshot;
    },
    load() {
      return null;
    },
    save(runtimeState) {
      savedStates.push(runtimeState);
      snapshot = {
        hasSavedSnapshot: true,
        storageKey: "test-storage",
        savedAt: "2026-03-31T00:00:00.000Z",
      };
      return snapshot;
    },
    clear() {
      snapshot = {
        hasSavedSnapshot: false,
        storageKey: "test-storage",
      };
      return snapshot;
    },
  };

  return { storage, savedStates };
}

function createState() {
  return createTestRuntimeState(
    createGraphDocument({
      id: "graph-host-persistence",
    }),
    createInitialGraphViewState(),
  );
}

const emptySubgraphAnalysis = createGraphSubgraphRuntime().analyze(createGraphDocument({
  id: "graph-host-persistence-analysis",
}));

function createExportResult(overrides: Partial<GraphWorkspaceExportResult>): GraphWorkspaceExportResult {
  return {
    ok: true,
    analysis: {
      topologyPolicy: "dag",
      hasCycle: false,
      topologicalOrder: [],
      rootNodeIds: [],
      leafNodeIds: [],
      connectedComponents: [],
      subgraphAnalysis: emptySubgraphAnalysis,
    },
    validation: {
      valid: true,
      issues: [],
      blockingIssues: [],
      warningCount: 0,
      errorCount: 0,
    },
    issues: [],
    runtimeContract: null,
    artifact: {
      format: "json",
      schema: GRAPH_RUNTIME_CONTRACT_SCHEMA,
      suggestedFileName: "graph-host-persistence.runtime.json",
      content: "{\"ok\":true}",
    },
    ...overrides,
  };
}

describe("graphWorkspaceHostPersistence", () => {
  it("hydrates from host content and persists the loaded state", async () => {
    const state = createState();
    const serialized = serializeGraphDocumentFile(state);
    const { storage, savedStates } = createMockStorage();
    const readWorkspaceGraphFileInfo = vi.fn(async () => ({
      path: "graph.json",
      exists: true,
      backend: "tauri",
    }));
    const readWorkspaceGraphFile = vi.fn(async () => ({
      path: "graph.json",
      exists: true,
      backend: "tauri",
      readAt: "2026-03-31T01:00:00.000Z",
      content: serialized,
    }));

    const result = await hydrateGraphWorkspaceFromHost({
      readWorkspaceGraphFileInfo,
      readWorkspaceGraphFile,
      deserializeGraphDocumentFile,
      storage,
    });

    expect(readWorkspaceGraphFileInfo).toHaveBeenCalledTimes(1);
    expect(readWorkspaceGraphFile).toHaveBeenCalledTimes(1);
    expect(result.workspaceFileSnapshot).toEqual({
      path: "graph.json",
      exists: true,
      backend: "tauri",
      readAt: "2026-03-31T01:00:00.000Z",
    });
    expect(result.nextState?.document.id).toBe("graph-host-persistence");
    expect(result.persistenceSnapshot).toEqual({
      hasSavedSnapshot: true,
      storageKey: "test-storage",
      savedAt: "2026-03-31T00:00:00.000Z",
    });
    expect(savedStates).toHaveLength(1);
  });

  it("returns null state when host file is empty", async () => {
    const { storage, savedStates } = createMockStorage();

    const result = await loadGraphWorkspaceFromHost({
      readWorkspaceGraphFile: async () => ({
        path: "graph.json",
        exists: false,
        backend: "tauri",
        readAt: "2026-03-31T01:05:00.000Z",
        content: null,
      }),
      deserializeGraphDocumentFile,
      storage,
    });

    expect(result.workspaceFileSnapshot).toEqual({
      path: "graph.json",
      exists: false,
      backend: "tauri",
      readAt: "2026-03-31T01:05:00.000Z",
    });
    expect(result.nextState).toBeNull();
    expect(result.persistenceSnapshot).toBeNull();
    expect(savedStates).toHaveLength(0);
  });

  it("serializes and saves workspace state through host writer", async () => {
    const state = createState();
    const { storage, savedStates } = createMockStorage();
    const writeWorkspaceGraphFile = vi.fn(async ({ content }: { content: string }) => ({
      path: "graph.json",
      exists: true,
      backend: "tauri",
      writtenAt: "2026-03-31T02:00:00.000Z",
      content,
    }));

    const result = await saveGraphWorkspaceToHost({
      state,
      serializeGraphDocumentFile,
      writeWorkspaceGraphFile: async ({ content }) => {
        const response = await writeWorkspaceGraphFile({ content });
        return {
          path: response.path,
          exists: response.exists,
          backend: response.backend,
          writtenAt: response.writtenAt,
        };
      },
      storage,
    });

    expect(writeWorkspaceGraphFile).toHaveBeenCalledTimes(1);
    const writtenPayload = JSON.parse(writeWorkspaceGraphFile.mock.calls[0]?.[0].content ?? "{}");
    expect(writtenPayload.schema).toBe("sceneblueprint.graph-document.v1");
    expect(writtenPayload.graph.id).toBe("graph-host-persistence");
    expect(result.workspaceFileSnapshot).toEqual({
      path: "graph.json",
      exists: true,
      backend: "tauri",
      writtenAt: "2026-03-31T02:00:00.000Z",
    });
    expect(result.persistenceSnapshot.savedAt).toBe("2026-03-31T00:00:00.000Z");
    expect(savedStates).toHaveLength(1);
  });

  it("exports runtime contract and surfaces blocking summaries", async () => {
    const invalidResult = await exportGraphRuntimeContractToHost({
      exportResult: createExportResult({
        ok: false,
        artifact: null,
        validation: {
          valid: false,
          issues: [{
            code: "graph-invalid",
            severity: "error",
            blocking: true,
            message: "图结构无效",
            location: { entityKind: "graph" },
          }],
          blockingIssues: [{
            code: "graph-invalid",
            severity: "error",
            blocking: true,
            message: "图结构无效",
            location: { entityKind: "graph" },
          }],
          warningCount: 0,
          errorCount: 1,
        },
      }),
      writeWorkspaceRuntimeContractFile: async () => {
        throw new Error("should not be called");
      },
    });

    expect(invalidResult).toEqual({
      ok: false,
      summary: "graph-invalid: 图结构无效",
    });

    const writeWorkspaceRuntimeContractFile = vi.fn(async ({ content }: { content: string }) => ({
      path: "graph.runtime.json",
      exists: true,
      backend: "tauri",
      writtenAt: "2026-03-31T03:00:00.000Z",
      content,
    }));
    const validResult = await exportGraphRuntimeContractToHost({
      exportResult: createExportResult({
        validation: {
          valid: true,
          issues: [{
            code: "graph-warning",
            severity: "warning",
            blocking: false,
            message: "存在非阻塞问题",
            location: { entityKind: "graph" },
          }],
          blockingIssues: [],
          warningCount: 1,
          errorCount: 0,
        },
      }),
      writeWorkspaceRuntimeContractFile: async ({ content }) => {
        const response = await writeWorkspaceRuntimeContractFile({ content });
        return {
          path: response.path,
          exists: response.exists,
          backend: response.backend,
          writtenAt: response.writtenAt,
        };
      },
    });

    expect(writeWorkspaceRuntimeContractFile).toHaveBeenCalledTimes(1);
    expect(validResult).toEqual({
      ok: true,
      runtimeContractFileSnapshot: {
        path: "graph.runtime.json",
        exists: true,
        backend: "tauri",
        exportedAt: "2026-03-31T03:00:00.000Z",
      },
      warningCount: 1,
    });
  });
});
