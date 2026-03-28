import { Panel } from "../../shared/components/Panel";

interface LogPanelProps {
  logs: string[];
}

export function LogPanel({ logs }: LogPanelProps) {
  return (
    <Panel
      title="Log / Output"
      description="承接宿主状态、问题输出、验证日志与后续调试信息。"
    >
      <div className="sb-log-list">
        {logs.map((log) => (
          <p key={log}>{log}</p>
        ))}
      </div>
    </Panel>
  );
}
