import type { AppInfo, PingResult } from "../../host/types/host";
import type { AppLogEntry } from "../../shared/logging/LogContext";

interface GraphStatusSummary {
  graphId: string;
  nodeCount: number;
  edgeCount: number;
  zoom: number;
  selectionKind: string;
  savedAt?: string;
}

interface StatusBarProps {
  entries: AppLogEntry[];
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
  graphSummary: GraphStatusSummary;
}

function formatSavedAt(value: string | undefined) {
  if (!value) {
    return "未保存";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });
}

export function StatusBar({ entries, appInfo, pingResult, graphSummary }: StatusBarProps) {
  const latestLog = entries[0] ? `[${entries[0].scope}] ${entries[0].message}` : "当前无日志";
  const hostLabel = appInfo ? `${appInfo.runtime} / ${appInfo.platform}` : "读取宿主中";
  const commLabel = pingResult ? pingResult.message : "尚未验证";
  const graphLabel = `${graphSummary.graphId} | N${graphSummary.nodeCount} E${graphSummary.edgeCount} | Zoom ${graphSummary.zoom.toFixed(2)} | ${graphSummary.selectionKind}`;
  const saveLabel = `最近保存 ${formatSavedAt(graphSummary.savedAt)}`;

  return (
    <div className="sb-statusbar">
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Workspace</span>
        <span className="sb-statusbar-pill">主工作台</span>
      </div>
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Graph</span>
        <span className="sb-statusbar-value">{graphLabel}</span>
      </div>
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Save</span>
        <span className="sb-statusbar-value">{saveLabel}</span>
      </div>
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Latest Log</span>
        <span className="sb-statusbar-value">{latestLog}</span>
      </div>
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Host</span>
        <span className="sb-statusbar-value">{hostLabel}</span>
      </div>
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Bridge</span>
        <span className="sb-statusbar-value">{commLabel}</span>
      </div>
    </div>
  );
}

