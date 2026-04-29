export type ShortcutKey = 'i' | 'x' | 'z';

export type ShortcutOptions = {
  shift?: boolean;
};

export function isEditorShortcut(
  event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
  key: ShortcutKey,
  options: ShortcutOptions = {}
) {
  return (
    (event.ctrlKey || event.metaKey) &&
    event.shiftKey === Boolean(options.shift) &&
    !event.altKey &&
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
