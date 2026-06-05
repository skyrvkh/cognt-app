export type ShortcutKey = 'c' | 'i' | 'x' | 'z';

export type ShortcutModifiers = {
  primary?: boolean;
  shift?: boolean;
  alt?: boolean;
};

import { reportDiagnostic } from '../host';

export function isEditorShortcut(
  event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
  key: ShortcutKey,
  modifiers: ShortcutModifiers = {}
) {
  const requiresPrimary = modifiers.primary ?? true;
  const requiresShift = modifiers.shift ?? false;
  const requiresAlt = modifiers.alt ?? false;
  const hasPrimary = event.ctrlKey || event.metaKey;

  const matched = (
    hasPrimary === requiresPrimary &&
    event.shiftKey === requiresShift &&
    event.altKey === requiresAlt &&
    event.key.toLowerCase() === key
  );
  reportDiagnostic(
    'info',
    `Editor shortcut checked (eventKey=${event.key}, targetKey=${key}, matched=${matched}, primary=${hasPrimary}, shift=${event.shiftKey}, alt=${event.altKey}).`
  );
  return matched;
}

export function preventReadOnlyKeyboardInput(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) {
    reportDiagnostic('info', `Read-only keyboard input allowed for modified key (key=${event.key}).`);
    return;
  }

  if (event.key.length === 1 || event.key === 'Backspace' || event.key === 'Delete') {
    event.preventDefault();
    reportDiagnostic('info', `Read-only keyboard input prevented (key=${event.key}).`);
    return;
  }

  reportDiagnostic('info', `Read-only keyboard input ignored as navigation/system key (key=${event.key}).`);
}
