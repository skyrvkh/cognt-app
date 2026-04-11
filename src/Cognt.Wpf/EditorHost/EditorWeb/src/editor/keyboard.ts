export type ShortcutKey = 'c' | 'i' | 'x';

export type ShortcutModifiers = {
  shift?: boolean;
  alt?: boolean;
};

export function isEditorShortcut(
  event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
  key: ShortcutKey,
  modifiers: ShortcutModifiers = {}
) {
  const requiresShift = modifiers.shift ?? false;
  const requiresAlt = modifiers.alt ?? false;

  return (
    (event.ctrlKey || event.metaKey) &&
    event.shiftKey === requiresShift &&
    event.altKey === requiresAlt &&
    event.key.toLowerCase() === key
  );
}

export function preventReadOnlyKeyboardInput(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) {
    return;
  }

  if (event.key.length === 1 || event.key === 'Backspace' || event.key === 'Delete') {
    event.preventDefault();
  }
}
