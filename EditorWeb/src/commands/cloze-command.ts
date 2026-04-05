import type { Doc } from '@blocksuite/store';
import {
  replaceSelectionWithCloze,
  type TextSelection,
} from '../cloze';
import { getCurrentSelection } from '../documents/selection-export';

export type SaveClozeCommandMessage = {
  type: 'saveCloze';
  sourcePath: string;
  selectedPlainText: string;
  clozeText: string;
  content: string;
};

export type ClozeCommandHost = {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getTextSelection(): TextSelection | null | undefined;
  clearTextSelection(): void;
  flushHost(): Promise<void>;
  refreshClozeHighlights(): void;
  cancelPendingContentChanged(): void;
  exportContent(): Promise<string>;
  saveCloze(message: SaveClozeCommandMessage): void;
  showStatus(message: string): void;
};

export async function runClozeCommand(host: ClozeCommandHost, event: KeyboardEvent) {
  event.preventDefault();
  event.stopPropagation();

  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before creating cloze text.');
    return;
  }

  const selection = getCurrentSelection();
  if (!selection) {
    host.showStatus('Ctrl+Z is disabled unless text is selected.');
    return;
  }

  let clozeText: string;
  try {
    clozeText = replaceSelectionWithCloze(
      {
        doc: host.getCurrentDoc(),
        textSelection: host.getTextSelection(),
      },
      selection.plainText
    );
  } catch (error) {
    host.showStatus(
      error instanceof Error ? error.message : 'Could not hide the selected text in place.'
    );
    return;
  }

  host.clearTextSelection();
  await host.flushHost();
  await new Promise(resolve => window.requestAnimationFrame(resolve));
  host.refreshClozeHighlights();
  host.cancelPendingContentChanged();
  host.saveCloze({
    type: 'saveCloze',
    sourcePath: host.getCurrentPath(),
    selectedPlainText: selection.plainText,
    clozeText,
    content: await host.exportContent(),
  });
  host.showStatus('Selection hidden in place with cloze.');
}
