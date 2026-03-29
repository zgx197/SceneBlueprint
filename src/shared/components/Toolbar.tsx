import type { ReactNode } from "react";

export interface ToolbarMetaItem {
  label: string;
  value: string;
}

interface ToolbarProps {
  title: string;
  subtitle: string;
  items: ToolbarMetaItem[];
  rightSlot?: ReactNode;
}

export function Toolbar({ title, subtitle, items, rightSlot }: ToolbarProps) {
  return (
    <header className="sb-toolbar">
      <div className="sb-toolbar-brand">
        <p className="sb-eyebrow">SceneBlueprint / External Authoring Editor</p>
        <div className="sb-toolbar-heading">
          <strong>{title}</strong>
          <span>{subtitle}</span>
        </div>
      </div>

      <div className="sb-toolbar-meta">
        {items.map((item) => (
          <div key={item.label} className="sb-toolbar-meta-item">
            <span className="sb-toolbar-meta-label">{item.label}</span>
            <strong className="sb-toolbar-meta-value">{item.value}</strong>
          </div>
        ))}
        {rightSlot}
      </div>
    </header>
  );
}
