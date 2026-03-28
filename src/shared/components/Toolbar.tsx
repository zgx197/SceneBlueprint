import type { ReactNode } from "react";

interface ToolbarProps {
  rightSlot?: ReactNode;
}

export function Toolbar({ rightSlot }: ToolbarProps) {
  return (
    <header className="sb-toolbar">
      <div>
        <p className="sb-eyebrow">SceneBlueprint / Authoring Workbench</p>
        <h1>正式工作台区域骨架</h1>
      </div>
      {rightSlot}
    </header>
  );
}
