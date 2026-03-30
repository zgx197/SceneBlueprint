import { readAppInfo } from "../../host/api/commands";
import type { AppInfo } from "../../host/types/host";
import type { AppLogLevel } from "../../shared/logging/LogContext";

const layoutStorageIds = [
  "sb-workbench-shell",
  "sb-workbench-left",
  "sb-workbench-top",
] as const;

const HOST_TIMEOUT_MS = 3000;

export interface StartupWorkspaceInfo {
  recentProjectName: string;
  recentProjectPath: string | null;
  recentProjectUpdatedAt: string | null;
  restoredLayoutCount: number;
  restoredLayoutLabels: string[];
  workspaceResumeLabel: string;
}

export interface StartupStageState {
  id: string;
  label: string;
  detail: string;
  status: "pending" | "running" | "completed" | "failed";
}

export interface StartupResult {
  appInfo: AppInfo | null;
  workspaceInfo: StartupWorkspaceInfo;
  stages: StartupStageState[];
}

interface StoredRecentProject {
  name?: string;
  path?: string;
  updatedAt?: string;
}

interface StoredWorkspaceSession {
  name?: string;
  lastOpenedDocument?: string;
}

interface StartupLogger {
  log: (level: AppLogLevel, scope: string, message: string) => void;
}

export const initialStartupStages: StartupStageState[] = [
  {
    id: "host",
    label: "桌面宿主",
    detail: "连接 Tauri 宿主并读取应用基础信息。",
    status: "pending",
  },
  {
    id: "workspace",
    label: "工作区恢复",
    detail: "检查最近项目与工作区恢复缓存。",
    status: "pending",
  },
  {
    id: "layout",
    label: "布局状态",
    detail: "确认分栏尺寸缓存是否可恢复。",
    status: "pending",
  },
  {
    id: "viewport",
    label: "场景视口",
    detail: "预热 Scene Viewport 模块与 3D 运行时。",
    status: "pending",
  },
];

export const initialStartupWorkspaceInfo: StartupWorkspaceInfo = {
  recentProjectName: "正在检查最近项目",
  recentProjectPath: null,
  recentProjectUpdatedAt: null,
  restoredLayoutCount: 0,
  restoredLayoutLabels: [],
  workspaceResumeLabel: "正在分析工作区恢复信息",
};

function readJson<T>(key: string): T | null {
  const raw = window.localStorage.getItem(key);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

function cloneStages(stages: StartupStageState[]) {
  return stages.map((stage) => ({ ...stage }));
}

function toErrorMessage(error: unknown) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string" && error.length > 0) {
    return error;
  }

  return "发生未知错误";
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number, timeoutLabel: string): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timeoutId = window.setTimeout(() => {
      reject(new Error(`${timeoutLabel}超时（>${timeoutMs}ms）`));
    }, timeoutMs);

    promise.then(
      (value) => {
        window.clearTimeout(timeoutId);
        resolve(value);
      },
      (error: unknown) => {
        window.clearTimeout(timeoutId);
        reject(error);
      },
    );
  });
}

export function readStartupWorkspaceInfo(): StartupWorkspaceInfo {
  const recentProject = readJson<StoredRecentProject>("sb.recent-project");
  const workspaceSession = readJson<StoredWorkspaceSession>("sb.workspace.session");

  const restoredLayoutLabels = layoutStorageIds.filter((id) => {
    return window.localStorage.getItem(`react-resizable-panels:${id}`) !== null;
  });

  const workspaceResumeLabel =
    workspaceSession?.name ??
    workspaceSession?.lastOpenedDocument ??
    (restoredLayoutLabels.length > 0 ? "检测到布局恢复信息" : "暂无恢复工作区");

  return {
    recentProjectName: recentProject?.name ?? "暂无最近项目",
    recentProjectPath: recentProject?.path ?? null,
    recentProjectUpdatedAt: recentProject?.updatedAt ?? null,
    restoredLayoutCount: restoredLayoutLabels.length,
    restoredLayoutLabels,
    workspaceResumeLabel,
  };
}

export async function runStartupSequence(
  logger: StartupLogger,
  onStagesChanged: (stages: StartupStageState[]) => void,
  onWorkspaceInfoChanged?: (workspaceInfo: StartupWorkspaceInfo) => void,
): Promise<StartupResult> {
  const stages = cloneStages(initialStartupStages);
  let appInfo: AppInfo | null = null;
  let workspaceInfo = readStartupWorkspaceInfo();

  const setStage = (
    stageId: string,
    status: StartupStageState["status"],
    detail?: string,
  ) => {
    const target = stages.find((stage) => stage.id === stageId);
    if (!target) {
      return;
    }

    target.status = status;
    if (detail) {
      target.detail = detail;
    }

    onStagesChanged(cloneStages(stages));
  };

  const refreshWorkspaceInfo = () => {
    workspaceInfo = readStartupWorkspaceInfo();
    onWorkspaceInfoChanged?.(workspaceInfo);
    return workspaceInfo;
  };

  onStagesChanged(cloneStages(stages));
  onWorkspaceInfoChanged?.(workspaceInfo);

  setStage("host", "running");
  logger.log("info", "startup", "开始连接桌面宿主并读取应用信息");

  try {
    appInfo = await withTimeout(readAppInfo(), HOST_TIMEOUT_MS, "读取桌面宿主信息");
    logger.log("info", "startup", `宿主信息读取完成：${appInfo.runtime} / ${appInfo.platform}`);
    setStage("host", "completed", `${appInfo.runtime} / ${appInfo.platform}`);
  } catch (error) {
    const message = toErrorMessage(error);
    logger.log("error", "startup", `桌面宿主初始化失败：${message}`);
    setStage("host", "failed", `桌面宿主初始化失败：${message}`);
    throw error;
  }

  setStage("workspace", "running");
  refreshWorkspaceInfo();
  logger.log("info", "startup", `最近项目检查完成：${workspaceInfo.recentProjectName}`);
  setStage("workspace", "completed", workspaceInfo.workspaceResumeLabel);

  setStage("layout", "running");
  refreshWorkspaceInfo();
  const layoutLabel =
    workspaceInfo.restoredLayoutCount > 0
      ? `已检测到 ${workspaceInfo.restoredLayoutCount} 组布局缓存`
      : "当前没有布局缓存";
  logger.log("info", "startup", `布局缓存检查完成：${layoutLabel}`);
  setStage("layout", "completed", layoutLabel);

  setStage("viewport", "running");
  logger.log("info", "startup", "Scene Viewport 将在首次显示时按需懒加载，当前跳过 3D 模块预热。");
  setStage("viewport", "completed", "Scene Viewport 将在首次打开时懒加载");

  return {
    appInfo,
    workspaceInfo,
    stages: cloneStages(stages),
  };
}

