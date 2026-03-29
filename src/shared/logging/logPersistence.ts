import type { AppLogEntry } from "./LogContext";

type InvokeFn = <T>(command: string, args?: Record<string, unknown>) => Promise<T>;

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

let invokePromise: Promise<InvokeFn | null> | null = null;
let logFilePathPromise: Promise<string | null> | null = null;
let hasLoggedResolvedPath = false;

function isTauriRuntime() {
  return typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;
}

async function readInvoke() {
  if (!isTauriRuntime()) {
    return null;
  }

  if (!invokePromise) {
    invokePromise = import("@tauri-apps/api/core")
      .then((module) => module.invoke as InvokeFn)
      .catch((error) => {
        console.warn("[logging] 无法加载 Tauri invoke：", error);
        return null;
      });
  }

  return invokePromise;
}

export async function getLogFilePath() {
  if (!isTauriRuntime()) {
    return null;
  }

  if (!logFilePathPromise) {
    logFilePathPromise = readInvoke().then(async (invoke) => {
      if (!invoke) {
        return null;
      }

      try {
        return await invoke<string>("get_log_file_path");
      } catch (error) {
        console.warn("[logging] 无法获取日志文件路径：", error);
        return null;
      }
    });
  }

  return logFilePathPromise;
}

export async function persistLogEntry(entry: AppLogEntry) {
  const invoke = await readInvoke();
  if (!invoke) {
    return;
  }

  try {
    await invoke("write_log_entry", { entry });

    if (!hasLoggedResolvedPath) {
      hasLoggedResolvedPath = true;
      const logFilePath = await getLogFilePath();
      if (logFilePath) {
        console.info(`[logging] 日志文件路径：${logFilePath}`);
      }
    }
  } catch (error) {
    console.warn("[logging] 写入本地日志文件失败：", error);
  }
}
