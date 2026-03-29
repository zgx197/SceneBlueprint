import { MenuCommandBridge } from "../commands/MenuCommandBridge";
import type { PropsWithChildren } from "react";
import { LogProvider, writeAppLog } from "../../shared/logging/LogContext";
import { useEffect } from "react";

function describeReason(reason: unknown) {
  if (reason instanceof Error && reason.message) {
    return reason.message;
  }

  if (typeof reason === "string" && reason.length > 0) {
    return reason;
  }

  try {
    return JSON.stringify(reason);
  } catch {
    return "发生未知异常";
  }
}

function RuntimeLoggingBridge({ children }: PropsWithChildren) {
  useEffect(() => {
    const handleError = (event: ErrorEvent) => {
      const location = event.filename
        ? ` (${event.filename}:${event.lineno ?? 0}:${event.colno ?? 0})`
        : "";
      writeAppLog("error", "runtime", `${event.message}${location}`);
    };

    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      writeAppLog("error", "runtime", `未处理的 Promise 异常：${describeReason(event.reason)}`);
    };

    window.addEventListener("error", handleError);
    window.addEventListener("unhandledrejection", handleUnhandledRejection);
    writeAppLog("info", "runtime", "前端运行时异常监听已启用。");

    return () => {
      window.removeEventListener("error", handleError);
      window.removeEventListener("unhandledrejection", handleUnhandledRejection);
    };
  }, []);

  return children;
}

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <LogProvider>
      <RuntimeLoggingBridge>
        <MenuCommandBridge />
        {children}
      </RuntimeLoggingBridge>
    </LogProvider>
  );
}
