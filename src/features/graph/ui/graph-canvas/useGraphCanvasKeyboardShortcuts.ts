import { useEffect, type RefObject } from "react";
import {
  isGraphAutoLayoutShortcut,
  isGraphCancelKey,
  isGraphCopyShortcut,
  isGraphDeleteKey,
  isGraphFindShortcut,
  isGraphPasteShortcut,
  isGraphSelectAllShortcut,
  shouldIgnoreGraphHotkeys,
} from "../../../../host/input/graphInput";

interface UseGraphCanvasKeyboardShortcutsOptions {
  contextMenuOpen: boolean;
  contextMenuRef: RefObject<HTMLDivElement | null>;
  searchInputRef: RefObject<HTMLInputElement | null>;
  selectedNodeCount: number;
  selectedEdgeCount: number;
  hasActiveConnectionPreview: boolean;
  onDismissContextMenu: () => void;
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
    contextMenuOpen,
    contextMenuRef,
    searchInputRef,
    selectedNodeCount,
    selectedEdgeCount,
    hasActiveConnectionPreview,
    onDismissContextMenu,
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

    const handleKeyDown = (event: KeyboardEvent) => {
      if (shouldIgnoreGraphHotkeys(event.target)) {
        return;
      }

      if (isGraphFindShortcut(event)) {
        event.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
        return;
      }

      if (isGraphSelectAllShortcut(event)) {
        event.preventDefault();
        onSelectAll();
        return;
      }

      if (isGraphCopyShortcut(event)) {
        event.preventDefault();
        onCopy();
        return;
      }

      if (isGraphPasteShortcut(event)) {
        event.preventDefault();
        onPaste();
        return;
      }

      if (isGraphAutoLayoutShortcut(event)) {
        event.preventDefault();
        onAutoLayout();
        return;
      }

      if (isGraphDeleteKey(event)) {
        if (selectedNodeCount > 0 || selectedEdgeCount > 0) {
          event.preventDefault();
          onDeleteSelection();
        }
        return;
      }

      if (!isGraphCancelKey(event)) {
        return;
      }

      onDismissContextMenu();
      onClearSearch();
      onResetTransientInteraction();
      if (hasActiveConnectionPreview) {
        onClearConnectionPreview();
      }
    };

    window.addEventListener("pointerdown", handlePointerDown);
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("pointerdown", handlePointerDown);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [
    contextMenuOpen,
    contextMenuRef,
    searchInputRef,
    selectedNodeCount,
    selectedEdgeCount,
    hasActiveConnectionPreview,
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
