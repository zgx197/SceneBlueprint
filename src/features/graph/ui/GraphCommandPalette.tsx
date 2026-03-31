import { Command } from "cmdk";
import type { GraphCommandPaletteItem, GraphCommandPaletteModel } from "./graphCommandPaletteModel";

interface GraphCommandPaletteProps {
  open: boolean;
  query: string;
  model: GraphCommandPaletteModel;
  onOpenChange: (open: boolean) => void;
  onQueryChange: (query: string) => void;
  onSelectItem: (item: GraphCommandPaletteItem) => void;
}

export function GraphCommandPalette(props: GraphCommandPaletteProps) {
  const { open, query, model, onOpenChange, onQueryChange, onSelectItem } = props;

  return (
    <Command.Dialog
      open={open}
      onOpenChange={onOpenChange}
      label="Graph Workspace Command Palette"
      contentClassName="sb-command-palette-dialog"
      overlayClassName="sb-command-palette-overlay"
    >
      <div className="sb-command-palette-header">
        <strong>Graph Workspace Command Palette</strong>
        <span>统一入口：动作、节点聚焦、节点创建</span>
      </div>

      <Command.Input
        value={query}
        onValueChange={onQueryChange}
        className="sb-command-palette-input"
        placeholder="输入命令、节点名、类型或分类..."
      />

      <Command.List className="sb-command-palette-list">
        <Command.Empty className="sb-command-palette-empty">{model.emptyLabel}</Command.Empty>
        {model.groups.map((group) => {
          if (group.items.length === 0) {
            return null;
          }

          return (
            <Command.Group key={group.id} heading={group.title} className="sb-command-palette-group">
              {group.items.map((item) => (
                <Command.Item
                  key={item.id}
                  value={`${item.label} ${item.description} ${item.badge ?? ""}`}
                  keywords={item.keywords}
                  className="sb-command-palette-item"
                  onSelect={() => onSelectItem(item)}
                >
                  <div className="sb-command-palette-item-main">
                    <strong>{item.label}</strong>
                    <span>{item.description}</span>
                  </div>
                  {item.badge ? <span className="sb-command-palette-item-badge">{item.badge}</span> : null}
                </Command.Item>
              ))}
            </Command.Group>
          );
        })}
      </Command.List>

      <div className="sb-command-palette-footer">
        <span>`Enter` 执行</span>
        <span>`Esc` 关闭</span>
      </div>
    </Command.Dialog>
  );
}
