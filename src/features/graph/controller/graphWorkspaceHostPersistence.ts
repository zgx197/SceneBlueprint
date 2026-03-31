import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphWorkspaceStorage, GraphWorkspaceStorageSnapshot } from "../storage/graphWorkspaceStorage";
import type { GraphWorkspaceExportResult } from "../runtime/graphWorkspaceExport";
import type {
  ReadWorkspaceGraphFileResult,
  WorkspaceGraphFileInfo,
  WriteWorkspaceGraphFileResult,
  WriteWorkspaceRuntimeContractFileResult,
} from "../../../host/types/host";

export interface GraphWorkspaceFileSnapshot extends WorkspaceGraphFileInfo {
  readAt?: string;
  writtenAt?: string;
}

export interface GraphRuntimeContractFileSnapshot extends Omit<WriteWorkspaceRuntimeContractFileResult, "writtenAt"> {
  exportedAt?: string;
}

export interface HydrateGraphWorkspaceFromHostResult {
  workspaceFileSnapshot: GraphWorkspaceFileSnapshot;
  nextState: GraphWorkspaceRuntimeState | null;
  persistenceSnapshot: GraphWorkspaceStorageSnapshot | null;
}

export async function hydrateGraphWorkspaceFromHost(options: {
  readWorkspaceGraphFileInfo: () => Promise<WorkspaceGraphFileInfo>;
  readWorkspaceGraphFile: () => Promise<ReadWorkspaceGraphFileResult>;
  deserializeGraphDocumentFile: (content: string) => GraphWorkspaceRuntimeState;
  storage: GraphWorkspaceStorage;
}): Promise<HydrateGraphWorkspaceFromHostResult> {
  await options.readWorkspaceGraphFileInfo();
  const result = await options.readWorkspaceGraphFile();

  const workspaceFileSnapshot: GraphWorkspaceFileSnapshot = {
    path: result.path,
    exists: result.exists,
    backend: result.backend,
    readAt: result.readAt,
  };

  if (!result.content) {
    return {
      workspaceFileSnapshot,
      nextState: null,
      persistenceSnapshot: null,
    };
  }

  const nextState = options.deserializeGraphDocumentFile(result.content);
  const persistenceSnapshot = options.storage.save(nextState);

  return {
    workspaceFileSnapshot,
    nextState,
    persistenceSnapshot,
  };
}

export async function saveGraphWorkspaceToHost(options: {
  state: GraphWorkspaceRuntimeState;
  serializeGraphDocumentFile: (state: GraphWorkspaceRuntimeState) => string;
  writeWorkspaceGraphFile: (request: { content: string }) => Promise<WriteWorkspaceGraphFileResult>;
  storage: GraphWorkspaceStorage;
}): Promise<{
  workspaceFileSnapshot: GraphWorkspaceFileSnapshot;
  persistenceSnapshot: GraphWorkspaceStorageSnapshot;
}> {
  const content = options.serializeGraphDocumentFile(options.state);
  const result = await options.writeWorkspaceGraphFile({ content });
  const persistenceSnapshot = options.storage.save(options.state);

  return {
    workspaceFileSnapshot: {
      path: result.path,
      exists: result.exists,
      backend: result.backend,
      writtenAt: result.writtenAt,
    },
    persistenceSnapshot,
  };
}

export async function loadGraphWorkspaceFromHost(options: {
  readWorkspaceGraphFile: () => Promise<ReadWorkspaceGraphFileResult>;
  deserializeGraphDocumentFile: (content: string) => GraphWorkspaceRuntimeState;
  storage: GraphWorkspaceStorage;
}): Promise<{
  workspaceFileSnapshot: GraphWorkspaceFileSnapshot;
  nextState: GraphWorkspaceRuntimeState | null;
  persistenceSnapshot: GraphWorkspaceStorageSnapshot | null;
}> {
  const result = await options.readWorkspaceGraphFile();
  const workspaceFileSnapshot: GraphWorkspaceFileSnapshot = {
    path: result.path,
    exists: result.exists,
    backend: result.backend,
    readAt: result.readAt,
  };

  if (!result.content) {
    return {
      workspaceFileSnapshot,
      nextState: null,
      persistenceSnapshot: null,
    };
  }

  const nextState = options.deserializeGraphDocumentFile(result.content);
  const persistenceSnapshot = options.storage.save(nextState);

  return {
    workspaceFileSnapshot,
    nextState,
    persistenceSnapshot,
  };
}

export async function exportGraphRuntimeContractToHost(options: {
  exportResult: GraphWorkspaceExportResult;
  writeWorkspaceRuntimeContractFile: (request: { content: string }) => Promise<WriteWorkspaceRuntimeContractFileResult>;
}): Promise<
  | {
      ok: false;
      summary: string;
    }
  | {
      ok: true;
      runtimeContractFileSnapshot: GraphRuntimeContractFileSnapshot;
      warningCount: number;
    }
> {
  const { exportResult } = options;
  if (!exportResult.ok || !exportResult.artifact) {
    const summary = exportResult.validation.blockingIssues
      .slice(0, 3)
      .map((issue) => `${issue.code}: ${issue.message}`)
      .join(" | ");

    return {
      ok: false,
      summary: summary.length > 0
        ? summary
        : "当前导出流程没有返回有效产物。",
    };
  }

  const result = await options.writeWorkspaceRuntimeContractFile({
    content: exportResult.artifact.content,
  });

  return {
    ok: true,
    runtimeContractFileSnapshot: {
      path: result.path,
      exists: result.exists,
      backend: result.backend,
      exportedAt: result.writtenAt,
    },
    warningCount: exportResult.validation.issues.length,
  };
}

