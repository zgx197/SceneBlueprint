import type { MouseEvent } from "react";
import type { GraphCommentId, GraphGroupId, GraphPoint, GraphSubgraphId } from "../../document/graphDocument";
import type { GraphFrameComment, GraphFrameGroup, GraphFrameSubgraph } from "../../frame/graphFrame";
import { joinClassNames } from "./graphCanvasUtils";

interface GraphCanvasStructureLayerProps {
  groups: GraphFrameGroup[];
  comments: GraphFrameComment[];
  subgraphs: GraphFrameSubgraph[];
  onGroupClick: (event: MouseEvent<HTMLElement>, groupId: GraphGroupId) => void;
  onGroupContextMenu: (clientPoint: GraphPoint, groupId: GraphGroupId) => void;
  onGroupPointerEnter: (groupId: GraphGroupId) => void;
  onGroupPointerLeave: () => void;
  onCommentClick: (event: MouseEvent<HTMLElement>, commentId: GraphCommentId) => void;
  onCommentContextMenu: (clientPoint: GraphPoint, commentId: GraphCommentId) => void;
  onCommentPointerEnter: (commentId: GraphCommentId) => void;
  onCommentPointerLeave: () => void;
  onSubgraphClick: (event: MouseEvent<HTMLElement>, subgraphId: GraphSubgraphId) => void;
  onSubgraphContextMenu: (clientPoint: GraphPoint, subgraphId: GraphSubgraphId) => void;
  onSubgraphPointerEnter: (subgraphId: GraphSubgraphId) => void;
  onSubgraphPointerLeave: () => void;
}

export function GraphCanvasStructureLayer(props: GraphCanvasStructureLayerProps) {
  const {
    groups,
    comments,
    subgraphs,
    onGroupClick,
    onGroupContextMenu,
    onGroupPointerEnter,
    onGroupPointerLeave,
    onCommentClick,
    onCommentContextMenu,
    onCommentPointerEnter,
    onCommentPointerLeave,
    onSubgraphClick,
    onSubgraphContextMenu,
    onSubgraphPointerEnter,
    onSubgraphPointerLeave,
  } = props;

  return (
    <>
      {subgraphs.map((subgraph) => (
        <section
          key={subgraph.id}
          className={joinClassNames(
            "sb-graph-subgraph",
            subgraph.selected && "sb-graph-subgraph-selected",
            subgraph.hovered && "sb-graph-subgraph-hovered",
          )}
          style={{
            left: subgraph.bounds.x,
            top: subgraph.bounds.y,
            width: subgraph.bounds.width,
            height: subgraph.bounds.height,
            borderColor: subgraph.color,
          }}
          onClick={(event) => onSubgraphClick(event, subgraph.id)}
          onContextMenu={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onSubgraphContextMenu({ x: event.clientX, y: event.clientY }, subgraph.id);
          }}
          onPointerEnter={() => onSubgraphPointerEnter(subgraph.id)}
          onPointerLeave={onSubgraphPointerLeave}
        >
          <header className="sb-graph-subgraph-header">
            <strong>{subgraph.title}</strong>
            {subgraph.entryNodeId ? <span>Entry {subgraph.entryNodeId}</span> : null}
          </header>
          {subgraph.description ? <p className="sb-graph-subgraph-description">{subgraph.description}</p> : null}
        </section>
      ))}

      {groups.map((group) => (
        <section
          key={group.id}
          className={joinClassNames(
            "sb-graph-group",
            group.selected && "sb-graph-group-selected",
            group.hovered && "sb-graph-group-hovered",
          )}
          style={{
            left: group.bounds.x,
            top: group.bounds.y,
            width: group.bounds.width,
            height: group.bounds.height,
            borderColor: group.color,
          }}
          onClick={(event) => onGroupClick(event, group.id)}
          onContextMenu={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onGroupContextMenu({ x: event.clientX, y: event.clientY }, group.id);
          }}
          onPointerEnter={() => onGroupPointerEnter(group.id)}
          onPointerLeave={onGroupPointerLeave}
        >
          <header className="sb-graph-group-header">
            <strong>{group.title}</strong>
            <span>{group.nodeIds.length} Nodes</span>
          </header>
        </section>
      ))}

      {comments.map((comment) => (
        <article
          key={comment.id}
          className={joinClassNames(
            "sb-graph-comment",
            `sb-graph-comment-${comment.tone}`,
            comment.selected && "sb-graph-comment-selected",
            comment.hovered && "sb-graph-comment-hovered",
          )}
          style={{
            left: comment.bounds.x,
            top: comment.bounds.y,
            width: comment.bounds.width,
            minHeight: comment.bounds.height,
          }}
          onClick={(event) => onCommentClick(event, comment.id)}
          onContextMenu={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onCommentContextMenu({ x: event.clientX, y: event.clientY }, comment.id);
          }}
          onPointerEnter={() => onCommentPointerEnter(comment.id)}
          onPointerLeave={onCommentPointerLeave}
        >
          <div className="sb-graph-comment-tag">Comment</div>
          <div className="sb-graph-comment-body">{comment.text}</div>
        </article>
      ))}
    </>
  );
}
