import type { AppInfo, PingResult } from "../types/host";

type InvokeFn = <T>(command: string, args?: Record<string, unknown>) => Promise<T>;

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

function isTauriRuntime(): boolean {
  return typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;
}

async function readInvoke(): Promise<InvokeFn | null> {
  if (!isTauriRuntime()) {
    return null;
  }

  const module = await import("@tauri-apps/api/core");
  return module.invoke as InvokeFn;
}

export async function invokeHost<T>(
  command: string,
  args?: Record<string, unknown>
): Promise<T> {
  const invoke = await readInvoke();

  if (!invoke) {
    return readMock(command) as T;
  }

  return invoke<T>(command, args);
}

function readMock(command: string): AppInfo | PingResult {
  if (command === "get_app_info") {
    return {
      name: "SceneBlueprint",
      version: "0.1.0-dev",
      runtime: "Browser Mock",
      platform: "web"
    };
  }

  return {
    message: "当前运行在浏览器占位模式，Rust 宿主尚未接管。",
    timestamp: new Date().toISOString()
  };
}
