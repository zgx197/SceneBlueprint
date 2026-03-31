import { useMemo } from "react";
import type { GraphWorkspaceExportPreflight } from "../runtime/graphWorkspaceKernel";
import { createGraphWorkspaceIssueFeedbackSummary } from "./graphWorkspaceIssueFeedback";

interface GraphWorkspaceIssueBannerProps {
  preflight: GraphWorkspaceExportPreflight;
}

export function GraphWorkspaceIssueBanner(props: GraphWorkspaceIssueBannerProps) {
  const { preflight } = props;
  const summary = useMemo(() => {
    return createGraphWorkspaceIssueFeedbackSummary(preflight);
  }, [preflight]);

  return (
    <section className={`sb-graph-issue-banner sb-graph-issue-banner-${summary.tone}`}>
      <div className="sb-graph-issue-banner-header">
        <div className="sb-graph-issue-banner-copy">
          <span className="sb-graph-issue-banner-pill">预检 {summary.statusLabel}</span>
          <strong>{summary.headline}</strong>
          <span>{summary.detail}</span>
        </div>
        <div className="sb-graph-issue-banner-stats">
          <span>总计 {summary.totalCount}</span>
          <span>阻塞 {summary.blockingCount}</span>
          <span>警告 {summary.warningCount}</span>
        </div>
      </div>

      {summary.topIssues.length > 0 ? (
        <div className="sb-graph-issue-banner-list">
          {summary.topIssues.map((issue) => (
            <div key={`${issue.code}:${issue.location.entityKind}:${issue.location.entityId ?? "global"}`} className="sb-graph-issue-banner-item">
              <span className="sb-graph-issue-banner-item-code">{issue.code}</span>
              <span className="sb-graph-issue-banner-item-message">{issue.message}</span>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  );
}
