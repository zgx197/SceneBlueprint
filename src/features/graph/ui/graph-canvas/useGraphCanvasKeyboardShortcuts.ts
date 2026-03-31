import { useEffect, type RefObject } from "react";
import type { GraphShortcutBindingService } from "../../services/graphShortcutBindingService";

interface UseGraphCanvasKeyboardShortcutsOptions {
  shortcutBindingService: GraphShortcutBindingService;
  contextMenuOpen: boolean;
  commandPaletteOpen: boolean;
  contextMenuRef: RefObject<HTMLDivElement | null>;
  searchInputRef: RefObject<HTMLInputElement | null>;
  totalSelectionCount: number;
  hasActiveConnectionPreview: boolean;
  onDismissContextMenu: () => void;
  onOpenCommandPalette: () => void;
  onCloseCommandPalette: () => void;
  onClearSearch: () => void;
  onResetTransientInteraction: () => void;
  onSelectAll: () => void;
  onCopy: () => void;
  onPaste: () => void;
  onAutoLayout: () => void;
  onDeleteSelection: () => void;
  onClearConnectionPreview: () => void;
}

export function useGraphCanvasKeyboardShortcuts(options: UseGraphCanvasKeyboardShortcutsOptions) {
  const {
    shortcutBindingService,
    contextMenuOpen,
    commandPaletteOpen,
    contextMenuRef,
    searchInputRef,
    totalSelectionCount,
    hasActiveConnectionPreview,
    onDismissContextMenu,
    onOpenCommandPalette,
    onCloseCommandPalette,
    onClearSearch,
    onResetTransientInteraction,
    onSelectAll,
    onCopy,
    onPaste,
    onAutoLayout,
    onDeleteSelection,
    onClearConnectionPreview,
  } = options;

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      if (!contextMenuOpen) {
        return;
      }

      const target = event.target;
      if (contextMenuRef.current && target instanceof Node && contextMenuRef.current.contains(target)) {
        return;
      }

      onDismissContextMenu();
    };

    window.addEventListener("pointerdown", handlePointerDown);
    return () => {
      window.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [contextMenuOpen, contextMenuRef, onDismissContextMenu]);

  useEffect(() => {
    return shortcutBindingService.bind(window, [
      {
        id: "graph.command-palette",
        chords: ["$mod+k"],
        handler: (event) => {
          event.preventDefault();
          onOpenCommandPalette();
        },
      },
      {
        id: "graph.focus-search",
        chords: ["$mod+f"],
        handler: (event) => {
          if (commandPaletteOpen) {
            return;
          }

          event.preventDefault();
          searchInputRef.current?.focus();
          searchInputRef.current?.select();
        },
      },
      {
        id: "graph.select-all",
        chords: ["$mod+a"],
        handler: (event) => {
          if (commandPaletteOpen) {
            return;
          }

          event.preventDefault();
          onSelectAll();
        },
      },
      {
        id: "graph.copy",
        chords: ["$mod+c"],
        handler: (event) => {
          if (commandPaletteOpen) {
            return;
          }

          event.preventDefault();
          onCopy();
        },
      },
      {
        id: "graph.paste",
        chords: ["$mod+v"],
        handler: (event) => {
          if (commandPaletteOpen) {
            return;
          }

          event.preventDefault();
          onPaste();
        },
      },
      {
        id: "graph.auto-layout",
        chords: ["$mod+shift+l"],
        handler: (event) => {
          if (commandPaletteOpen) {
            return;
          }

          event.preventDefault();
          onAutoLayout();
        },
      },
      {
        id: "graph.delete-forward",
        chords: ["Delete"],
        handler: (event) => {
          if (commandPaletteOpen || totalSelectionCount <= 0) {
            return;
          }

          event.preventDefault();
          onDeleteSelection();
        },
      },
      {
        id: "graph.delete-backward",
        chords: ["Backspace"],
        handler: (event) => {
          if (commandPaletteOpen || totalSelectionCount <= 0) {
            return;
          }

          event.preventDefault();
          onDeleteSelection();
        },
      },
      {
        id: "graph.cancel",
        chords: ["Escape"],
        allowInEditable: true,
        handler: (event) => {
          event.preventDefault();
          onCloseCommandPalette();
          onDismissContextMenu();
          onClearSearch();
          onResetTransientInteraction();
          if (hasActiveConnectionPreview) {
            onClearConnectionPreview();
          }
        },
      },
    ]);
  }, [
    shortcutBindingService,
    commandPaletteOpen,
    searchInputRef,
    totalSelectionCount,
    hasActiveConnectionPreview,
    onOpenCommandPalette,
    onCloseCommandPalette,
    onDismissContextMenu,
    onClearSearch,
    onResetTransientInteraction,
    onSelectAll,
    onCopy,
    onPaste,
    onAutoLayout,
    onDeleteSelection,
    onClearConnectionPreview,
  ]);
}
