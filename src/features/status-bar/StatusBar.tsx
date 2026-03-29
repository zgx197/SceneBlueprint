import type { AppInfo, PingResult } from "../../host/types/host";
import type { AppLogEntry } from "../../shared/logging/LogContext";

interface StatusBarProps {
  entries: AppLogEntry[];
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
}

export function StatusBar({ entries, appInfo, pingResult }: StatusBarProps) {
  const latestLog = entries[0] ? `[${entries[0].scope}] ${entries[0].message}` : "当前无日志";
  const hostLabel = appInfo ? `${appInfo.runtime} / ${appInfo.platform}` : "读取宿主中";
  const commLabel = pingResult ? pingResult.message : "尚未验证";

  return (
    <div className="sb-statusbar">
      <div className="sb-statusbar-section">
        <span className="sb-statusbar-label">Workspace</span>
        <span className="sb-statusbar-pill">主工作台</span>
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
