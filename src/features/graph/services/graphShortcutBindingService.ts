import { tinykeys } from "tinykeys";

export interface GraphShortcutBinding {
  id: string;
  chords: string[];
  allowInEditable?: boolean;
  handler: (event: KeyboardEvent) => void;
}

export interface GraphShortcutBindingService {
  bind(target: Window | HTMLElement, bindings: GraphShortcutBinding[]): () => void;
}

function shouldIgnoreShortcutTarget(target: EventTarget | null | undefined) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLowerCase();
  if (tagName === "input" || tagName === "textarea" || tagName === "select") {
    return true;
  }

  return target.isContentEditable;
}

export function createGraphShortcutBindingService(): GraphShortcutBindingService {
  return {
    bind(target, bindings) {
      const keyBindingMap = Object.fromEntries(
        bindings.flatMap((binding) => {
          return binding.chords.map((chord) => [
            chord,
            (event: KeyboardEvent) => {
              if (!binding.allowInEditable && shouldIgnoreShortcutTarget(event.target)) {
                return;
              }

              binding.handler(event);
            },
          ]);
        }),
      );

      return tinykeys(target, keyBindingMap);
    },
  };
}
