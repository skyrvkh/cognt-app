import type { Doc } from '@blocksuite/store';
import {
  getNativeClozeSelection,
  getNextClozeIndex,
  replaceSelectionWithCloze,
  type TextSelection,
} from '../cloze';
import {
  getCurrentSelection,
  type CurrentSelection,
} from '../documents/selection-export';
import { reportDiagnostic } from '../host';

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
  getEditorRoot(): Node;
  getTextSelection(): TextSelection | null | undefined;
  clearTextSelection(): void;
  flushHost(): Promise<void>;
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
  reportDiagnostic(
    'info',
    `Cloze command requested (context=${options.cardContextId ?? ''}, slot=${options.slotId ?? ''}, displayIndex=${options.displayClozeIndex ?? ''}).`
  );

  if (event) {
    event.preventDefault();
    event.stopPropagation();
  }

  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before creating cloze text.');
    reportDiagnostic('info', 'Cloze command skipped: editor input is locked.');
    return;
  }

  const nativeClozeSelection = getNativeClozeSelection(host.getEditorRoot(), host.getCurrentDoc());
  const selection = createSelectionFromNativeCloze(nativeClozeSelection) ?? getCurrentSelection();
  if (!selection) {
    host.showStatus('Select text, code, or an equation before creating a cloze.');
    reportDiagnostic('info', 'Cloze command skipped: no usable selection.');
    return;
  }

  let clozeText: string;
  const textSelection = host.getTextSelection();
  const blockId = getSelectionBlockId(textSelection)
    ?? nativeClozeSelection?.blockId;
  if (!blockId) {
    host.showStatus('Cloze requires a single BlockSuite text, code, or equation selection.');
    reportDiagnostic(
      'info',
      `Cloze command skipped: selected text has no BlockSuite block id (text=${selection.plainText.length}).`
    );
    return;
  }

  let clozeIndex: number;
  try {
    clozeIndex =
      typeof options.displayClozeIndex === 'number' && Number.isFinite(options.displayClozeIndex)
        ? Math.max(1, Math.trunc(options.displayClozeIndex))
        : getNextClozeIndex(host.getEditorRoot());
    reportDiagnostic(
      'info',
      `Cloze command replacing selection (path=${host.getCurrentPath()}, block=${blockId}, text=${selection.plainText.length}, index=${clozeIndex}).`
    );
    clozeText = replaceSelectionWithCloze(
      {
        doc: host.getCurrentDoc(),
        textSelection: host.getTextSelection(),
        editorRoot: host.getEditorRoot(),
      },
      selection.plainText,
      clozeIndex
    );
  } catch (error) {
    host.showStatus(
      error instanceof Error ? error.message : 'Could not hide the selected text in place.'
    );
    reportDiagnostic('error', `Cloze command failed while replacing selection: ${errorMessage(error)}`);
    return;
  }

  host.clearTextSelection();
  await host.flushHost();
  await new Promise(resolve => window.requestAnimationFrame(resolve));
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
    displayClozeIndex:
      typeof options.displayClozeIndex === 'number' && Number.isFinite(options.displayClozeIndex)
        ? Math.max(1, Math.trunc(options.displayClozeIndex))
        : null,
  });
  reportDiagnostic(
    'info',
    `Cloze command saved draft request (path=${host.getCurrentPath()}, block=${blockId}, text=${selection.plainText.length}, cloze=${clozeText.length}, index=${clozeIndex}).`
  );
  host.showStatus('Selection hidden in place with cloze.');
}

function createSelectionFromNativeCloze(
  nativeSelection: ReturnType<typeof getNativeClozeSelection>
): CurrentSelection | null {
  if (!nativeSelection?.plainText.trim()) {
    return null;
  }

  return {
    plainText: nativeSelection.plainText,
  };
}

function getSelectionBlockId(selection: TextSelection | null | undefined) {
  const point = selection?.start ?? selection?.from ?? null;
  return point?.blockId?.trim() || null;
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
