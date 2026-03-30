import { useCallback, useEffect, useMemo, useState } from "react";
import { appCommandIds } from "../../app/commands/appCommands";
import { registerAppCommand } from "../../app/commands/commandRegistry";
import { WorkbenchLayout } from "../../app/layout/WorkbenchLayout";
import { pingHost, readAppInfo, readLogFilePath } from "../../host/api/commands";
import type { AppInfo, PingResult } from "../../host/types/host";
import { RuntimeErrorBoundary } from "../../shared/components/RuntimeErrorBoundary";
import { useAppLogContext } from "../../shared/logging/LogContext";
import { GraphPanel } from "../graph/GraphPanel";
import { useGraphWorkspaceController } from "../graph/GraphWorkspaceController";
import { InspectorPanel } from "../inspector/InspectorPanel";
import { SceneViewportPanel } from "../scene/SceneViewportPanel";
import { StatusBar } from "../status-bar/StatusBar";

interface WorkbenchPageProps {
  initialAppInfo?: AppInfo | null;
}

const layoutStorageIds = ["sb-workbench-shell", "sb-workbench-left", "sb-workbench-top"] as const;

function WorkbenchFallback() {
  return (
    <div className="sb-runtime-fallback-screen">
      <div className="sb-runtime-fallback-card">
        <p className="sb-eyebrow">SCENEBLUEPRINT / RUNTIME</p>
        <h1>工作台挂载失败</h1>
        <p>工作台主界面在渲染阶段出现异常，已阻止继续崩溃。请查看本地日志继续排查。</p>
      </div>
    </div>
  );
}

function WorkbenchPageContent(props: WorkbenchPageProps) {
  const { initialAppInfo = null } = props;
  const { entries, log } = useAppLogContext();
  const graphWorkspaceController = useGraphWorkspaceController();
  const [appInfo, setAppInfo] = useState<AppInfo | null>(initialAppInfo);
  const [pingResult, setPingResult] = useState<PingResult | null>(null);

  useEffect(() => {
    log("info", "workbench", "工作台页面已挂载。");

    return () => {
      log("info", "workbench", "工作台页面已卸载。");
    };
  }, [log]);

  useEffect(() => {
    if (initialAppInfo) {
      log("info", "workbench", `使用启动阶段传入的宿主信息：${initialAppInfo.runtime} / ${initialAppInfo.platform}`);
      return;
    }

    void readAppInfo()
      .then((info) => {
        setAppInfo(info);
        log("info", "host", `宿主信息读取完成：${info.name} ${info.version} (${info.runtime})`);
      })
      .catch((error: unknown) => {
        const message = error instanceof Error ? error.message : "宿主信息读取失败";
        log("error", "host", `宿主信息读取失败：${message}`);
      });
  }, [initialAppInfo, log]);

  const handlePing = useCallback(async () => {
    try {
      const result = await pingHost();
      setPingResult(result);
      log("info", "bridge", `宿主通信验证成功：${result.message}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "宿主通信验证失败";
      log("error", "bridge", `宿主通信验证失败：${message}`);
    }
  }, [log]);

  const handlePrintLogPath = useCallback(async () => {
    try {
      const logFilePath = await readLogFilePath();
      log("info", "logging", `当前日志文件路径：${logFilePath}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "日志路径读取失败";
      log("error", "logging", `日志路径读取失败：${message}`);
    }
  }, [log]);

  const handleResetLayout = useCallback(() => {
    for (const layoutId of layoutStorageIds) {
      window.localStorage.removeItem(`react-resizable-panels:${layoutId}`);
    }

    log("info", "layout", "已清除工作台布局缓存，准备重新加载界面。");
    window.location.reload();
  }, [log]);

  useEffect(() => {
    const unregisterPing = registerAppCommand(appCommandIds.developPingHost, async () => {
      await handlePing();
    });
    const unregisterLogPath = registerAppCommand(appCommandIds.developPrintLogPath, async () => {
      await handlePrintLogPath();
    });
    const unregisterSaveProject = registerAppCommand(appCommandIds.fileSaveProject, async () => {
      await graphWorkspaceController.saveWorkspaceFile();
    });
    const unregisterUndo = registerAppCommand(appCommandIds.editUndo, () => {
      graphWorkspaceController.undo();
    });
    const unregisterRedo = registerAppCommand(appCommandIds.editRedo, () => {
      graphWorkspaceController.redo();
    });
    const unregisterResetLayout = registerAppCommand(appCommandIds.viewResetLayout, () => {
      handleResetLayout();
    });
    const unregisterAbout = registerAppCommand(appCommandIds.helpAbout, () => {
      const hostLabel = appInfo ? `${appInfo.runtime} / ${appInfo.platform}` : "宿主信息读取中";
      log("info", "help", `SceneBlueprint ${appInfo?.version ?? "0.1.0"} | ${hostLabel}`);
    });

    return () => {
      unregisterPing();
      unregisterLogPath();
      unregisterSaveProject();
      unregisterUndo();
      unregisterRedo();
      unregisterResetLayout();
      unregisterAbout();
    };
  }, [appInfo, graphWorkspaceController, handlePing, handlePrintLogPath, handleResetLayout, log]);

  const graphSummary = useMemo(() => {
    return {
      graphId: graphWorkspaceController.document.id,
      nodeCount: graphWorkspaceController.document.nodes.length,
      edgeCount: graphWorkspaceController.document.edges.length,
      zoom: graphWorkspaceController.viewState.viewport.zoom,
      selectionKind: graphWorkspaceController.selectionTarget.kind,
      savedAt:
        graphWorkspaceController.workspaceFileSnapshot?.writtenAt ??
        graphWorkspaceController.persistenceSnapshot.savedAt,
    };
  }, [
    graphWorkspaceController.document.edges.length,
    graphWorkspaceController.document.id,
    graphWorkspaceController.document.nodes.length,
    graphWorkspaceController.persistenceSnapshot.savedAt,
    graphWorkspaceController.selectionTarget.kind,
    graphWorkspaceController.viewState.viewport.zoom,
    graphWorkspaceController.workspaceFileSnapshot?.writtenAt,
  ]);

  const toolbarItems = useMemo(() => {
    const hostLabel = appInfo ? `${appInfo.runtime} / ${appInfo.platform}` : "读取宿主中";
    const bridgeLabel = pingResult ? "已验证" : "待验证";

    return [
      { label: "项目", value: graphWorkspaceController.workspaceFileSnapshot?.path ?? "未建立正式项目文件" },
      { label: "工作区", value: "主工作台" },
      { label: "模式", value: "Authoring" },
      { label: "宿主", value: hostLabel },
      { label: "通信", value: bridgeLabel },
    ];
  }, [appInfo, graphWorkspaceController.workspaceFileSnapshot?.path, pingResult]);

  return (
    <WorkbenchLayout
      toolbarTitle="主工作台"
      toolbarSubtitle="原生菜单负责全局命令，顶部工具栏承载当前上下文信息。"
      toolbarItems={toolbarItems}
      graph={<GraphPanel controller={graphWorkspaceController} />}
      scene={<SceneViewportPanel />}
      inspector={
        <InspectorPanel
          appInfo={appInfo}
          pingResult={pingResult}
          selectionTarget={graphWorkspaceController.selectionTarget}
          workspaceFileInfo={graphWorkspaceController.workspaceFileSnapshot}
          onPatchNodePayload={(nodeId, patch) => {
            graphWorkspaceController.patchNodePayload(nodeId, patch);
          }}
        />
      }
      statusBar={
        <StatusBar
          entries={entries}
          appInfo={appInfo}
          pingResult={pingResult}
          graphSummary={graphSummary}
        />
      }
    />
  );
}

export function WorkbenchPage(props: WorkbenchPageProps) {
  return (
    <RuntimeErrorBoundary scope="workbench" fallback={<WorkbenchFallback />}>
      <WorkbenchPageContent {...props} />
    </RuntimeErrorBoundary>
  );
}
