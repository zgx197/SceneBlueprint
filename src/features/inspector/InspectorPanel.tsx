import type { AppInfo, PingResult } from "../../host/types/host";
import { Panel } from "../../shared/components/Panel";

interface InspectorPanelProps {
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
}

export function InspectorPanel(props: InspectorPanelProps) {
  const { appInfo, pingResult } = props;

  return (
    <Panel
      title="Inspector"
      description="统一响应 Graph 与 Scene 的选择态，后续承接属性、分析、调试与概览信息。"
    >
      <dl className="sb-kv">
        <dt>应用</dt>
        <dd>{appInfo?.name ?? "未读取"}</dd>
        <dt>版本</dt>
        <dd>{appInfo?.version ?? "未读取"}</dd>
        <dt>平台</dt>
        <dd>{appInfo?.platform ?? "未读取"}</dd>
        <dt>运行时</dt>
        <dd>{appInfo?.runtime ?? "未读取"}</dd>
        <dt>通信结果</dt>
        <dd>{pingResult?.message ?? "尚未验证"}</dd>
        <dt>当前职责</dt>
        <dd>统一信息面板</dd>
      </dl>
    </Panel>
  );
}
