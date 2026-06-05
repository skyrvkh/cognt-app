import {
  getCurrentSelection,
  type CurrentSelection,
} from '../documents/selection-export';
import { reportDiagnostic } from '../host';

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
  reportDiagnostic('info', `Keyboard selection command preparing (key=${event.key}).`);
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
    reportDiagnostic('info', 'Selection command skipped: editor input is locked.');
    return null;
  }

  const selection = getCurrentSelection();
  if (!selection) {
    host.showStatus(emptySelectionMessage);
    reportDiagnostic('info', 'Selection command skipped: selection is empty.');
    return null;
  }

  reportDiagnostic('info', `Selection command prepared (text=${selection.plainText.length}).`);
  return selection;
}
