import type { RefObject } from "react";
import type { GraphNodeDefinition } from "../definitions/graphDefinitions";
import type {
  GraphContextMenuActionId,
  GraphContextMenuModel,
  GraphContextMenuState,
} from "./graphCanvasContextMenuModel";

interface GraphCanvasContextMenuProps {
  containerRef: RefObject<HTMLDivElement | null>;
  state: GraphContextMenuState;
  model: GraphContextMenuModel;
  onAction: (actionId: GraphContextMenuActionId) => void;
  onCategoryChange: (category: string) => void;
  onCreateNode: (definition: GraphNodeDefinition) => void;
}

function joinClassNames(...classNames: Array<string | false | null | undefined>) {
  return classNames.filter(Boolean).join(" ");
}

export function GraphCanvasContextMenu(props: GraphCanvasContextMenuProps) {
  const { containerRef, state, model, onAction, onCategoryChange, onCreateNode } = props;

  return (
    <div
      ref={containerRef}
      className="sb-graph-context-menu"
      style={{ left: state.screenX, top: state.screenY }}
      onPointerDown={(event) => {
        event.stopPropagation();
      }}
      onContextMenu={(event) => {
        event.preventDefault();
        event.stopPropagation();
      }}
    >
      <div className="sb-graph-context-menu-section">
        {model.actions.map((action) => (
          <button
            key={action.id}
            type="button"
            className={joinClassNames(
              "sb-graph-context-menu-item",
              action.tone === "danger" && "sb-graph-context-menu-item-danger",
            )}
            onClick={() => onAction(action.id)}
          >
            <span>{action.label}</span>
          </button>
        ))}
      </div>
      {model.showDefinitionBrowser ? <div className="sb-graph-context-menu-divider" /> : null}
      {model.showDefinitionBrowser ? (
        <div className="sb-graph-context-menu-browser">
          <div className="sb-graph-context-menu-categories">
            {model.definitionGroups.map((group) => (
              <button
                key={group.category}
                type="button"
                className={joinClassNames(
                  "sb-graph-context-menu-item",
                  model.activeCategory === group.category && "sb-graph-context-menu-item-active",
                )}
                onMouseEnter={() => onCategoryChange(group.category)}
                onClick={() => onCategoryChange(group.category)}
              >
                <span>{group.category}</span>
                <span className="sb-graph-context-menu-arrow">&gt;</span>
              </button>
            ))}
          </div>
          <div className="sb-graph-context-menu-items">
            {model.visibleDefinitions.map((definition) => (
              <button
                key={definition.typeId}
                type="button"
                className="sb-graph-context-menu-item"
                onClick={() => onCreateNode(definition)}
                title={definition.summary ?? definition.displayName}
              >
                <span>{definition.displayName}</span>
              </button>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );
}