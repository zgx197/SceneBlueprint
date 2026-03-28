import type { ReactNode } from "react";

interface ToolbarProps {
  rightSlot?: ReactNode;
}

export function Toolbar({ rightSlot }: ToolbarProps) {
  return (
    <header className="sb-toolbar">
      <div>
        <p className="sb-eyebrow">SceneBlueprint / Phase 1</p>
        <h1>Tauri 盒子骨架</h1>
      </div>
      {rightSlot}
    </header>
  );
}
