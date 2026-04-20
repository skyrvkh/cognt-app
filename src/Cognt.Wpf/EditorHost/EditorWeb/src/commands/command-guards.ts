import {
  getCurrentSelection,
  type CurrentSelection,
} from '../documents/selection-export';

export type KeyboardSelectionCommandHost = {
  isInputLocked(): boolean;
  showStatus(message: string): void;
};

export function prepareKeyboardSelectionCommand(
  host: KeyboardSelectionCommandHost,
  event: KeyboardEvent,
  lockedMessage: string,
  emptySelectionMessage: string
): CurrentSelection | null {
  event.preventDefault();
  event.stopPropagation();

  return prepareSelectionCommand(host, lockedMessage, emptySelectionMessage);
}

export function prepareSelectionCommand(
  host: KeyboardSelectionCommandHost,
  lockedMessage: string,
  emptySelectionMessage: string
): CurrentSelection | null {
  if (host.isInputLocked()) {
    host.showStatus(lockedMessage);
    return null;
  }

  const selection = getCurrentSelection();
  if (!selection) {
    host.showStatus(emptySelectionMessage);
    return null;
  }

  return selection;
}
