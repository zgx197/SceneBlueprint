import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";
import { persistLogEntry } from "./logPersistence";

export type AppLogLevel = "debug" | "info" | "warn" | "error";

export interface AppLogEntry {
  id: string;
  timestamp: string;
  level: AppLogLevel;
  scope: string;
  message: string;
}

interface LogContextValue {
  entries: AppLogEntry[];
  log: (level: AppLogLevel, scope: string, message: string) => void;
  clear: () => void;
}

type LogListener = (entries: AppLogEntry[]) => void;

const LogContext = createContext<LogContextValue | null>(null);
const MAX_LOG_ENTRIES = 200;
const logListeners = new Set<LogListener>();
let logEntries: AppLogEntry[] = [];

function createLogEntry(level: AppLogLevel, scope: string, message: string): AppLogEntry {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    timestamp: new Date().toISOString(),
    level,
    scope,
    message,
  };
}

function mirrorToConsole(entry: AppLogEntry) {
  const text = `[${entry.scope}] ${entry.message}`;

  if (entry.level === "error") {
    console.error(text);
    return;
  }

  if (entry.level === "warn") {
    console.warn(text);
    return;
  }

  if (entry.level === "debug") {
    console.debug(text);
    return;
  }

  console.info(text);
}

function notifyLogListeners() {
  for (const listener of logListeners) {
    listener(logEntries);
  }
}

export function writeAppLog(level: AppLogLevel, scope: string, message: string) {
  const entry = createLogEntry(level, scope, message);
  mirrorToConsole(entry);
  logEntries = [entry, ...logEntries].slice(0, MAX_LOG_ENTRIES);
  notifyLogListeners();
  void persistLogEntry(entry);
}

export function clearAppLogs() {
  logEntries = [];
  notifyLogListeners();
}

export function getAppLogEntries() {
  return logEntries;
}

export function subscribeAppLogs(listener: LogListener) {
  logListeners.add(listener);
  listener(logEntries);

  return () => {
    logListeners.delete(listener);
  };
}

export function LogProvider({ children }: PropsWithChildren) {
  const [entries, setEntries] = useState<AppLogEntry[]>(getAppLogEntries);

  useEffect(() => {
    return subscribeAppLogs(setEntries);
  }, []);

  const log = useCallback((level: AppLogLevel, scope: string, message: string) => {
    writeAppLog(level, scope, message);
  }, []);

  const clear = useCallback(() => {
    clearAppLogs();
  }, []);

  const value = useMemo<LogContextValue>(() => {
    return {
      entries,
      log,
      clear,
    };
  }, [entries, log, clear]);

  return <LogContext.Provider value={value}>{children}</LogContext.Provider>;
}

export function useAppLogContext() {
  const context = useContext(LogContext);
  if (!context) {
    throw new Error("useAppLogContext must be used within LogProvider");
  }

  return context;
}
