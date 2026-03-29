import type { AppLogEntry } from "../../shared/logging/LogContext";
import type { StartupStageState, StartupWorkspaceInfo } from "./startup";

interface StartupScreenProps {
  progress: number;
  stageLabel: string;
  stages: StartupStageState[];
  workspaceInfo: StartupWorkspaceInfo;
  logs: AppLogEntry[];
  startupError: string | null;
  isTransitioning: boolean;
  isOverlay?: boolean;
}

function SceneBlueprintMark() {
  return (
    <div className="sb-startup-mark" aria-hidden="true">
      <span className="sb-startup-mark-block sb-startup-mark-block-a" />
      <span className="sb-startup-mark-block sb-startup-mark-block-b" />
      <span className="sb-startup-mark-block sb-startup-mark-block-c" />
      <span className="sb-startup-mark-line" />
    </div>
  );
}

export function StartupScreen(props: StartupScreenProps) {
  const { progress, stageLabel, stages, workspaceInfo, logs, startupError, isTransitioning, isOverlay } = props;
  const recentLogs = logs.slice(0, 6);
  const screenClassName = [
    "sb-startup-screen",
    isOverlay ? "sb-startup-screen-overlay" : "",
    isTransitioning ? "sb-startup-screen-leaving" : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={screenClassName}>
      <div className="sb-startup-card">
        <div className="sb-startup-hero">
          <SceneBlueprintMark />
          <div className="sb-startup-copy">
            <p className="sb-startup-eyebrow">SCENEBLUEPRINT / STARTUP</p>
            <h1>{startupError ? "启动未完成" : "正在准备 SceneBlueprint"}</h1>
            <p className="sb-startup-description">
              {startupError
                ? "启动链路在宿主初始化阶段被中断，请根据下方日志继续排查。"
                : isTransitioning
                  ? "启动链路已完成，正在平滑切换到编辑工作台。"
                  : "正在加载工作台外壳、主题资源与场景视口模块，请稍候。"}
            </p>
            {startupError ? <div className="sb-startup-error-banner">{startupError}</div> : null}
          </div>
        </div>

        <div className="sb-startup-progress">
          <div className="sb-startup-progress-track">
            <div className="sb-startup-progress-fill" style={{ width: `${progress}%` }} />
          </div>
          <div className="sb-startup-progress-meta">
            <span>{stageLabel}</span>
            <strong>{progress}%</strong>
          </div>
        </div>

        <div className="sb-startup-grid">
          <div className="sb-startup-panel">
            <div className="sb-startup-panel-header">初始化阶段</div>
            <div className="sb-startup-stage-list">
              {stages.map((stage) => (
                <div key={stage.id} className={`sb-startup-stage sb-startup-stage-${stage.status}`}>
                  <span className="sb-startup-stage-dot" />
                  <div className="sb-startup-stage-copy">
                    <strong>{stage.label}</strong>
                    <span>{stage.detail}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="sb-startup-panel">
            <div className="sb-startup-panel-header">工作区恢复</div>
            <dl className="sb-startup-meta-list">
              <div>
                <dt>最近项目</dt>
                <dd>{workspaceInfo.recentProjectName}</dd>
              </div>
              <div>
                <dt>恢复状态</dt>
                <dd>{workspaceInfo.workspaceResumeLabel}</dd>
              </div>
              <div>
                <dt>布局缓存</dt>
                <dd>{workspaceInfo.restoredLayoutCount} 组</dd>
              </div>
              <div>
                <dt>项目路径</dt>
                <dd>{workspaceInfo.recentProjectPath ?? "尚未建立项目记录"}</dd>
              </div>
            </dl>
          </div>
        </div>

        <div className="sb-startup-panel sb-startup-log-panel">
          <div className="sb-startup-panel-header">运行日志</div>
          <div className="sb-startup-log-list">
            {recentLogs.length > 0 ? (
              recentLogs.map((entry) => (
                <div key={entry.id} className={`sb-startup-log sb-startup-log-${entry.level}`}>
                  <span className="sb-startup-log-scope">{entry.scope}</span>
                  <span className="sb-startup-log-message">{entry.message}</span>
                </div>
              ))
            ) : (
              <div className="sb-startup-log sb-startup-log-empty">当前还没有运行日志</div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
