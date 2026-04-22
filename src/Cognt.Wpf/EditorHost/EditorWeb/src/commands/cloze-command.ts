import type { Doc } from '@blocksuite/store';
import {
  getNextClozeIndex,
  replaceSelectionWithCloze,
  type TextSelection,
} from '../cloze';
import {
  prepareKeyboardSelectionCommand,
  prepareSelectionCommand,
} from './command-guards';

export type SaveClozeCommandMessage = {
  type: 'saveCloze';
  sourcePath: string;
  blockId: string;
  selectedPlainText: string;
  clozeText: string;
  content: string;
  cardContextId?: string | null;
  slotId?: string | null;
  displayClozeIndex?: number | null;
};

export type ClozeCommandOptions = {
  cardContextId?: string | null;
  slotId?: string | null;
  displayClozeIndex?: number | null;
};

export type ClozeCommandHost = {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getEditorRoot?(): Node;
  getTextSelection(): TextSelection | null | undefined;
  clearTextSelection(): void;
  flushHost(): Promise<void>;
  refreshClozeHighlights(): void;
  cancelPendingContentChanged(): void;
  exportContent(): Promise<string>;
  saveCloze(message: SaveClozeCommandMessage): void;
  showStatus(message: string): void;
};

export async function runClozeCommand(
  host: ClozeCommandHost,
  event?: KeyboardEvent,
  options: ClozeCommandOptions = {}
) {
  const selection = event
    ? prepareKeyboardSelectionCommand(
        host,
        event,
        'Wait for the full note to finish loading before creating cloze text.',
        'Select text before creating a cloze.'
      )
    : prepareSelectionCommand(
        host,
        'Wait for the full note to finish loading before creating cloze text.',
        'Select text before creating a cloze.'
      );
  if (!selection) {
    return;
  }

  let clozeText: string;
  let clozeIndex = 1;
  const textSelection = host.getTextSelection();
  const blockId = getSelectionBlockId(textSelection);
  if (!blockId) {
    host.showStatus('Cloze requires a single BlockSuite text selection.');
    return;
  }

  try {
    const requestedIndex =
      typeof options.displayClozeIndex === 'number' && Number.isFinite(options.displayClozeIndex)
        ? Math.max(1, Math.trunc(options.displayClozeIndex))
        : null;
    clozeIndex = requestedIndex ?? (
      host.getEditorRoot ? getNextClozeIndex(host.getEditorRoot()) : 1
    );
    clozeText = replaceSelectionWithCloze(
      {
        doc: host.getCurrentDoc(),
        textSelection: host.getTextSelection(),
      },
      selection.plainText,
      clozeIndex
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
    blockId,
    selectedPlainText: selection.plainText,
    clozeText,
    content: await host.exportContent(),
    cardContextId: options.cardContextId ?? null,
    slotId: options.slotId ?? null,
    displayClozeIndex: clozeIndex,
  });
  host.showStatus('Selection hidden in place with cloze.');
}

function getSelectionBlockId(selection: TextSelection | null | undefined) {
  const point = selection?.start ?? selection?.from ?? null;
  return point?.blockId?.trim() || null;
}
