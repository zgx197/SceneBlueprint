import { useEffect, useState } from "react";
import { WorkbenchLayout } from "../../app/layout/WorkbenchLayout";
import { pingHost, readAppInfo } from "../../host/api/commands";
import type { AppInfo, PingResult } from "../../host/types/host";
import { GraphPanel } from "../graph/GraphPanel";
import { SceneViewportPanel } from "../scene/SceneViewportPanel";
import { InspectorPanel } from "../inspector/InspectorPanel";
import { BottomPanels } from "../bottom-panels/BottomPanels";

export function WorkbenchPage() {
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null);
  const [pingResult, setPingResult] = useState<PingResult | null>(null);
  const [logs, setLogs] = useState<string[]>([
    "SceneBlueprint 第二阶段目标：先把正式工作台区域骨架搭起来。"
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
      scene={<SceneViewportPanel />}
      inspector={<InspectorPanel appInfo={appInfo} pingResult={pingResult} />}
      bottomPanels={<BottomPanels logs={logs} />}
    />
  );
}
