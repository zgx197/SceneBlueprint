import { writeAppLog } from "../../shared/logging/LogContext";
import { getAppCommandDefinition, type AppCommandId } from "./appCommands";

export interface AppCommandExecutionContext {
  source: "native-menu" | "toolbar" | "internal";
}

export type AppCommandHandler = (context: AppCommandExecutionContext) => Promise<void> | void;

const commandHandlers = new Map<AppCommandId, AppCommandHandler>();

function describeError(error: unknown) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string" && error.length > 0) {
    return error;
  }

  return "发生未知错误";
}

export function registerAppCommand(commandId: AppCommandId, handler: AppCommandHandler) {
  commandHandlers.set(commandId, handler);

  return () => {
    const current = commandHandlers.get(commandId);
    if (current === handler) {
      commandHandlers.delete(commandId);
    }
  };
}

export async function executeAppCommand(commandId: string, context: AppCommandExecutionContext) {
  const definition = getAppCommandDefinition(commandId);
  if (!definition) {
    writeAppLog("error", "command", `收到未知命令：${commandId}`);
    return;
  }

  writeAppLog("info", "command", `执行命令：${definition.label}（来源：${context.source}）`);
  const handler = commandHandlers.get(definition.id);

  if (!handler) {
    writeAppLog("warn", "command", `命令尚未接入处理逻辑：${definition.label}`);
    return;
  }

  try {
    await handler(context);
  } catch (error) {
    writeAppLog("error", "command", `${definition.label} 执行失败：${describeError(error)}`);
  }
}
