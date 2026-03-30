import { writeAppLog } from "../../shared/logging/LogContext";
import type {
  AppInfo,
  PingResult,
  ReadWorkspaceGraphFileResult,
  WorkspaceGraphFileInfo,
  WriteWorkspaceGraphFileResult,
} from "../types/host";

type InvokeFn = <T>(command: string, args?: Record<string, unknown>) => Promise<T>;

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

const MOCK_WORKSPACE_GRAPH_FILE_CONTENT_KEY = "sceneblueprint.mock.workspace-graph-file.content";
const MOCK_WORKSPACE_GRAPH_FILE_PATH = "browser://SceneBlueprint.graph.json";

function describeError(error: unknown) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string" && error.length > 0) {
    return error;
  }

  try {
    return JSON.stringify(error);
  } catch {
    return "发生未知错误";
  }
}

function isTauriRuntime(): boolean {
  const detected = typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;
  writeAppLog(
    "debug",
    "bridge",
    detected ? "检测到 Tauri Runtime，准备连接宿主。" : "未检测到 Tauri Runtime，将使用浏览器占位宿主。",
  );
  return detected;
}

async function readInvoke(): Promise<InvokeFn | null> {
  if (!isTauriRuntime()) {
    return null;
  }

  try {
    writeAppLog("debug", "bridge", "开始动态导入 @tauri-apps/api/core。");
    const module = await import("@tauri-apps/api/core");
    writeAppLog("debug", "bridge", "@tauri-apps/api/core 导入成功。");
    return module.invoke as InvokeFn;
  } catch (error) {
    const message = describeError(error);
    writeAppLog("error", "bridge", `导入 @tauri-apps/api/core 失败：${message}`);
    throw error;
  }
}

export async function invokeHost<T>(
  command: string,
  args?: Record<string, unknown>,
): Promise<T> {
  writeAppLog("debug", "bridge", `准备调用宿主命令：${command}`);
  const invoke = await readInvoke();

  if (!invoke) {
    writeAppLog("warn", "bridge", `宿主命令 ${command} 未进入 Tauri Runtime，返回 mock 数据。`);
    return readMock(command, args) as T;
  }

  try {
    const result = await invoke<T>(command, args);
    writeAppLog("info", "bridge", `宿主命令 ${command} 调用成功。`);
    return result;
  } catch (error) {
    const message = describeError(error);
    writeAppLog("error", "bridge", `宿主命令 ${command} 调用失败：${message}`);
    throw error;
  }
}

function buildMockWorkspaceFileInfo(): WorkspaceGraphFileInfo {
  const exists = typeof window !== "undefined" && window.localStorage.getItem(MOCK_WORKSPACE_GRAPH_FILE_CONTENT_KEY) !== null;

  return {
    path: MOCK_WORKSPACE_GRAPH_FILE_PATH,
    exists,
    backend: "browser-mock",
  };
}

function readMock(command: string, args?: Record<string, unknown>) {
  if (command === "get_app_info") {
    return {
      name: "SceneBlueprint",
      version: "0.1.0-dev",
      runtime: "Browser Mock",
      platform: "web",
    } satisfies AppInfo;
  }

  if (command === "get_log_file_path") {
    return "浏览器占位模式下没有本地日志文件路径。";
  }

  if (command === "get_workspace_graph_file_info") {
    return buildMockWorkspaceFileInfo();
  }

  if (command === "read_workspace_graph_file") {
    const content = typeof window !== "undefined"
      ? window.localStorage.getItem(MOCK_WORKSPACE_GRAPH_FILE_CONTENT_KEY)
      : null;

    return {
      ...buildMockWorkspaceFileInfo(),
      content,
      readAt: new Date().toISOString(),
    } satisfies ReadWorkspaceGraphFileResult;
  }

  if (command === "write_workspace_graph_file") {
    const request = args?.request;
    const content =
      request && typeof request === "object" && request !== null && "content" in request
        ? request.content
        : null;

    if (typeof window !== "undefined" && typeof content === "string") {
      window.localStorage.setItem(MOCK_WORKSPACE_GRAPH_FILE_CONTENT_KEY, content);
    }

    return {
      ...buildMockWorkspaceFileInfo(),
      exists: true,
      writtenAt: new Date().toISOString(),
    } satisfies WriteWorkspaceGraphFileResult;
  }

  return {
    message: "当前运行在浏览器占位模式，Rust 宿主尚未接管。",
    timestamp: new Date().toISOString(),
  } satisfies PingResult;
}
