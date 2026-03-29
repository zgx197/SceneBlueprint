import { useEffect, useMemo, useRef, useState } from "react";
import { useAppLogContext } from "./shared/logging/LogContext";
import { StartupScreen } from "./features/startup/StartupScreen";
import {
  initialStartupStages,
  initialStartupWorkspaceInfo,
  runStartupSequence,
  type StartupResult,
  type StartupStageState,
  type StartupWorkspaceInfo,
} from "./features/startup/startup";
import { WorkbenchPage } from "./features/workbench/WorkbenchPage";

const STARTUP_EXIT_DELAY_MS = 160;
const STARTUP_EXIT_DURATION_MS = 420;

function computeProgress(stages: StartupStageState[]) {
  if (stages.length === 0) {
    return 0;
  }

  const settledCount = stages.filter((stage) => stage.status === "completed" || stage.status === "failed").length;
  const runningStage = stages.find((stage) => stage.status === "running");
  const base = Math.round((settledCount / stages.length) * 100);

  if (runningStage) {
    return Math.min(96, base + 12);
  }

  return Math.min(100, base);
}

function describeError(error: unknown) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string" && error.length > 0) {
    return error;
  }

  return "发生未知错误";
}

function App() {
  const { entries, log } = useAppLogContext();
  const [stages, setStages] = useState<StartupStageState[]>(initialStartupStages);
  const [workspaceInfo, setWorkspaceInfo] = useState<StartupWorkspaceInfo>(initialStartupWorkspaceInfo);
  const [startupResult, setStartupResult] = useState<StartupResult | null>(null);
  const [startupError, setStartupError] = useState<string | null>(null);
  const [isStartupLeaving, setIsStartupLeaving] = useState(false);
  const [isWorkbenchMounted, setIsWorkbenchMounted] = useState(false);
  const [isReady, setIsReady] = useState(false);
  const startupRef = useRef(false);

  const progress = useMemo(() => {
    return startupResult ? 100 : computeProgress(stages);
  }, [stages, startupResult]);

  const activeStage = [...stages].reverse().find((stage) => stage.status === "running");
  const stageLabel = startupError
    ? "启动失败"
    : isStartupLeaving
      ? "正在进入编辑器"
      : startupResult
        ? "正在完成启动收尾"
        : activeStage?.label ?? "正在准备启动环境";

  useEffect(() => {
    if (startupRef.current) {
      return;
    }

    startupRef.current = true;
    let disposed = false;
    let leaveTimer = 0;
    let readyTimer = 0;

    const start = async () => {
      try {
        const result = await runStartupSequence(
          { log },
          (nextStages) => {
            if (!disposed) {
              setStages(nextStages);
            }
          },
          (nextWorkspaceInfo) => {
            if (!disposed) {
              setWorkspaceInfo(nextWorkspaceInfo);
            }
          },
        );

        if (disposed) {
          return;
        }

        setStartupResult(result);
        setStages(result.stages);
        setWorkspaceInfo(result.workspaceInfo);
        setIsWorkbenchMounted(true);
        log("info", "startup", "启动链路完成，开始挂载工作台。") ;

        leaveTimer = window.setTimeout(() => {
          if (!disposed) {
            setIsStartupLeaving(true);
            log("info", "startup", "启动页进入离场过渡。");
          }
        }, STARTUP_EXIT_DELAY_MS);

        readyTimer = window.setTimeout(() => {
          if (!disposed) {
            setIsReady(true);
            log("info", "startup", "启动页已退出，工作台成为主视图。");
          }
        }, STARTUP_EXIT_DELAY_MS + STARTUP_EXIT_DURATION_MS);
      } catch (error) {
        if (disposed) {
          return;
        }

        const message = describeError(error);
        setStartupError(message);
        log("error", "startup", `启动流程已中止：${message}`);
      }
    };

    void start();

    return () => {
      disposed = true;
      window.clearTimeout(leaveTimer);
      window.clearTimeout(readyTimer);
    };
  }, [log]);

  if (!isReady && !isWorkbenchMounted) {
    return (
      <StartupScreen
        progress={progress}
        stageLabel={stageLabel}
        stages={stages}
        workspaceInfo={workspaceInfo}
        logs={entries}
        startupError={startupError}
        isTransitioning={false}
      />
    );
  }

  if (!isReady) {
    return (
      <div className="sb-app-transition-root">
        <div className={`sb-app-workbench-layer ${isStartupLeaving ? "sb-app-workbench-layer-visible" : ""}`}>
          <WorkbenchPage initialAppInfo={startupResult?.appInfo ?? null} />
        </div>
        <StartupScreen
          progress={progress}
          stageLabel={stageLabel}
          stages={stages}
          workspaceInfo={workspaceInfo}
          logs={entries}
          startupError={startupError}
          isTransitioning={isStartupLeaving}
          isOverlay
        />
      </div>
    );
  }

  return <WorkbenchPage initialAppInfo={startupResult?.appInfo ?? null} />;
}

export default App;
