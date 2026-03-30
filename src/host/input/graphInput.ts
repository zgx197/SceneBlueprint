interface GraphPointerInputSnapshot {
  button: number;
  altKey: boolean;
  shiftKey: boolean;
  ctrlKey: boolean;
  metaKey: boolean;
}

interface GraphKeyboardInputSnapshot {
  key: string;
  ctrlKey: boolean;
  metaKey: boolean;
  shiftKey: boolean;
  target?: EventTarget | null;
}

type PointerLikeEvent = Pick<GraphPointerInputSnapshot, "button" | "altKey" | "shiftKey" | "ctrlKey" | "metaKey">;
type KeyboardLikeEvent = Pick<GraphKeyboardInputSnapshot, "key" | "ctrlKey" | "metaKey" | "shiftKey" | "target">;

function readPointerSnapshot(event: PointerLikeEvent): GraphPointerInputSnapshot {
  return {
    button: event.button,
    altKey: event.altKey,
    shiftKey: event.shiftKey,
    ctrlKey: event.ctrlKey,
    metaKey: event.metaKey,
  };
}

function hasCommandModifier(event: Pick<GraphKeyboardInputSnapshot, "ctrlKey" | "metaKey">) {
  return event.ctrlKey || event.metaKey;
}

export function isGraphPrimaryPointer(event: PointerLikeEvent) {
  return readPointerSnapshot(event).button === 0;
}

export function shouldStartGraphPanGesture(event: PointerLikeEvent) {
  const snapshot = readPointerSnapshot(event);
  return snapshot.button === 1 || snapshot.altKey || snapshot.shiftKey;
}

export function isGraphAdditiveSelectionPointer(event: Pick<GraphPointerInputSnapshot, "ctrlKey" | "metaKey">) {
  return event.ctrlKey || event.metaKey;
}

export function isGraphContextMenuPointer(event: PointerLikeEvent) {
  return readPointerSnapshot(event).button === 2;
}

export function isGraphCancelKey(event: Pick<GraphKeyboardInputSnapshot, "key">) {
  return event.key === "Escape";
}

export function isGraphDeleteKey(event: Pick<GraphKeyboardInputSnapshot, "key">) {
  return event.key === "Delete" || event.key === "Backspace";
}

export function isGraphSelectAllShortcut(event: KeyboardLikeEvent) {
  return hasCommandModifier(event) && !event.shiftKey && event.key.toLowerCase() === "a";
}

export function isGraphCopyShortcut(event: KeyboardLikeEvent) {
  return hasCommandModifier(event) && !event.shiftKey && event.key.toLowerCase() === "c";
}

export function isGraphPasteShortcut(event: KeyboardLikeEvent) {
  return hasCommandModifier(event) && !event.shiftKey && event.key.toLowerCase() === "v";
}

export function isGraphFindShortcut(event: KeyboardLikeEvent) {
  return hasCommandModifier(event) && !event.shiftKey && event.key.toLowerCase() === "f";
}

export function isGraphAutoLayoutShortcut(event: KeyboardLikeEvent) {
  return hasCommandModifier(event) && event.shiftKey && event.key.toLowerCase() === "l";
}

export function shouldIgnoreGraphHotkeys(target: EventTarget | null | undefined) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLowerCase();
  if (tagName === "input" || tagName === "textarea" || tagName === "select") {
    return true;
  }

  return target.isContentEditable;
}
