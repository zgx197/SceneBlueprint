import { useEffect } from "react";
import { writeAppLog } from "../../shared/logging/LogContext";
import { executeAppCommand } from "./commandRegistry";
import {
  NATIVE_MENU_COMMAND_EVENT,
  type NativeMenuCommandPayload,
} from "./appCommands";

function isTauriRuntime() {
  return typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;
}

export function MenuCommandBridge() {
  useEffect(() => {
    if (!isTauriRuntime()) {
      return;
    }

    let disposed = false;
    let disposeListener: (() => void) | null = null;

    void import("@tauri-apps/api/event")
      .then(async ({ listen }) => {
        const unlisten = await listen<NativeMenuCommandPayload>(NATIVE_MENU_COMMAND_EVENT, (event) => {
          void executeAppCommand(event.payload.commandId, { source: "native-menu" });
        });

        if (disposed) {
          unlisten();
          return;
        }

        disposeListener = unlisten;
        writeAppLog("info", "menu", "原生菜单命令桥接已启用。");
      })
      .catch((error: unknown) => {
        const message = error instanceof Error ? error.message : "菜单事件监听初始化失败";
        writeAppLog("error", "menu", `原生菜单命令桥接初始化失败：${message}`);
      });

    return () => {
      disposed = true;
      disposeListener?.();
    };
  }, []);

  return null;
}
