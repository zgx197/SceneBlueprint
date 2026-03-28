import { useEffect, useState } from "react";
import { WorkbenchLayout } from "../../app/layout/WorkbenchLayout";
import { pingHost, readAppInfo } from "../../host/api/commands";
import type { AppInfo, PingResult } from "../../host/types/host";
import { GraphPanel } from "../graph/GraphPanel";
import { InspectorPanel } from "../inspector/InspectorPanel";
import { TimelinePanel } from "../timeline/TimelinePanel";
import { LogPanel } from "../log/LogPanel";

export function WorkbenchPage() {
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null);
  const [pingResult, setPingResult] = useState<PingResult | null>(null);
  const [logs, setLogs] = useState<string[]>([
    "SceneBlueprint 第一阶段目标：先把 Tauri 盒子搭起来。"
  ]);

  useEffect(() => {
    void readAppInfo().then((info) => {
      setAppInfo(info);
      setLogs((current) => [
        `宿主信息：${info.name} ${info.version} (${info.runtime})`,
        ...current
      ]);
    });
  }, []);

  const handlePing = async () => {
    const result = await pingHost();
    setPingResult(result);
    setLogs((current) => [
      `宿主通信验证：${result.message} @ ${result.timestamp}`,
      ...current
    ]);
  };

  return (
    <WorkbenchLayout
      toolbarRight={
        <div className="sb-toolbar-status">
          <button onClick={handlePing}>验证宿主通信</button>
          <span>
            {appInfo ? `${appInfo.runtime} / ${appInfo.platform}` : "读取宿主中"}
          </span>
        </div>
      }
      graph={<GraphPanel />}
      inspector={<InspectorPanel appInfo={appInfo} pingResult={pingResult} />}
      timeline={<TimelinePanel />}
      log={<LogPanel logs={logs} />}
    />
  );
}
