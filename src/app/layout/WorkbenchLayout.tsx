import { useEffect, useState, type ReactNode } from "react";
import {
  Group as PanelGroup,
  Panel,
  Separator as PanelResizeHandle,
  useDefaultLayout,
} from "react-resizable-panels";
import { Toolbar, type ToolbarMetaItem } from "../../shared/components/Toolbar";

interface WorkbenchLayoutProps {
  toolbarTitle: string;
  toolbarSubtitle: string;
  toolbarItems: ToolbarMetaItem[];
  toolbarRight?: ReactNode;
  graph: ReactNode;
  scene: ReactNode;
  inspector: ReactNode;
  statusBar: ReactNode;
}

const COMPACT_MEDIA_QUERY = "(max-width: 1024px)";

function getCompactLayoutMatches() {
  if (typeof window === "undefined") {
    return false;
  }

  return window.matchMedia(COMPACT_MEDIA_QUERY).matches;
}

function useCompactWorkbenchLayout() {
  const [isCompact, setIsCompact] = useState(getCompactLayoutMatches);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const mediaQuery = window.matchMedia(COMPACT_MEDIA_QUERY);
    const handleChange = (event: MediaQueryListEvent) => {
      setIsCompact(event.matches);
    };

    setIsCompact(mediaQuery.matches);
    mediaQuery.addEventListener("change", handleChange);

    return () => {
      mediaQuery.removeEventListener("change", handleChange);
    };
  }, []);

  return isCompact;
}

interface ResizeHandleProps {
  direction: "horizontal" | "vertical";
}

function ResizeHandle({ direction }: ResizeHandleProps) {
  return <PanelResizeHandle className={`sb-resize-handle sb-resize-handle-${direction}`} />;
}

export function WorkbenchLayout(props: WorkbenchLayoutProps) {
  const {
    toolbarTitle,
    toolbarSubtitle,
    toolbarItems,
    toolbarRight,
    graph,
    scene,
    inspector,
    statusBar,
  } = props;
  const isCompact = useCompactWorkbenchLayout();

  const shellLayout = useDefaultLayout({
    id: "sb-workbench-shell",
    panelIds: ["workspace", "inspector"],
  });
  const leftLayout = useDefaultLayout({
    id: "sb-workbench-left",
    panelIds: ["main", "status"],
  });
  const topLayout = useDefaultLayout({
    id: "sb-workbench-top",
    panelIds: ["scene", "graph"],
  });

  return (
    <div className="sb-shell">
      <Toolbar
        title={toolbarTitle}
        subtitle={toolbarSubtitle}
        items={toolbarItems}
        rightSlot={toolbarRight}
      />
      <main className={`sb-workbench${isCompact ? " sb-workbench-compact" : ""}`}>
        {isCompact ? (
          <>
            <section className="sb-area sb-area-scene">{scene}</section>
            <section className="sb-area sb-area-graph">{graph}</section>
            <aside className="sb-area sb-area-inspector">{inspector}</aside>
            <section className="sb-area sb-area-status">{statusBar}</section>
          </>
        ) : (
          <PanelGroup
            orientation="horizontal"
            id="sb-workbench-shell"
            className="sb-workbench-group"
            defaultLayout={shellLayout.defaultLayout}
            onLayoutChanged={shellLayout.onLayoutChanged}
          >
            <Panel id="workspace" defaultSize="76%" minSize="60%" className="sb-panel-region">
              <PanelGroup
                orientation="vertical"
                id="sb-workbench-left"
                className="sb-workbench-group"
                defaultLayout={leftLayout.defaultLayout}
                onLayoutChanged={leftLayout.onLayoutChanged}
              >
                <Panel id="main" defaultSize="94%" minSize="70%" className="sb-panel-region">
                  <PanelGroup
                    orientation="horizontal"
                    id="sb-workbench-top"
                    className="sb-workbench-group"
                    defaultLayout={topLayout.defaultLayout}
                    onLayoutChanged={topLayout.onLayoutChanged}
                  >
                    <Panel id="scene" defaultSize="32%" minSize="22%" className="sb-panel-region">
                      <section className="sb-area sb-area-scene">{scene}</section>
                    </Panel>
                    <ResizeHandle direction="horizontal" />
                    <Panel id="graph" defaultSize="68%" minSize="35%" className="sb-panel-region">
                      <section className="sb-area sb-area-graph">{graph}</section>
                    </Panel>
                  </PanelGroup>
                </Panel>
                <ResizeHandle direction="vertical" />
                <Panel id="status" defaultSize="6%" minSize="4%" maxSize="10%" className="sb-panel-region">
                  <section className="sb-area sb-area-status">{statusBar}</section>
                </Panel>
              </PanelGroup>
            </Panel>
            <ResizeHandle direction="horizontal" />
            <Panel id="inspector" defaultSize="24%" minSize="18%" maxSize="30%" className="sb-panel-region">
              <aside className="sb-area sb-area-inspector">{inspector}</aside>
            </Panel>
          </PanelGroup>
        )}
      </main>
    </div>
  );
}
