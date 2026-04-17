import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  createBlockSuiteDocumentJsonFromPlainText,
  createTitleFromPlainText,
} from '../documents/block-suite-document';
import {
  cloneTextSelection,
  createBlockSuiteDocumentJsonFromSelection,
  getCurrentSelection,
  type TextSelectionSnapshot,
} from '../documents/selection-export';

export type PendingExtract = {
  generation: number;
  sourcePath: string;
  selection: TextSelectionSnapshot;
};

export type CreateExtractCommandMessage = {
  type: 'createExtract';
  sourcePath: string;
  selectedContent: string;
  selectedPlainText: string;
};

export type CreateExtractCommandHost = {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  getEditor(): PageEditor;
  getTextSelection(): TextSelection | null | undefined;
  createExtract(message: CreateExtractCommandMessage): void;
  showStatus(message: string): void;
};

export function runCreateExtractCommand(
  host: CreateExtractCommandHost,
  event: KeyboardEvent
): PendingExtract | null {
  event.preventDefault();
  event.stopPropagation();

  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before extracting text.');
    return null;
  }

  const selection = getCurrentSelection();
  if (!selection) {
    host.showStatus('Ctrl+X needs selected text.');
    return null;
  }

  const textSelection = host.getTextSelection();
  const selectionSnapshot = textSelection && !textSelection.isCollapsed()
    ? cloneTextSelection(textSelection)
    : null;
  if (!selectionSnapshot) {
    host.showStatus('Ctrl+X needs a BlockSuite text selection.');
    return null;
  }

  const currentDoc = host.getCurrentDoc();
  const editor = host.getEditor();
  const selectedContent = currentDoc
    ? createBlockSuiteDocumentJsonFromSelection(currentDoc, editor, selection.plainText)
    : null;

  host.createExtract({
    type: 'createExtract',
    sourcePath: host.getCurrentPath(),
    selectedContent: selectedContent ?? createBlockSuiteDocumentJsonFromPlainText(
      selection.plainText,
      createTitleFromPlainText(selection.plainText, 'Extract')
    ),
    selectedPlainText: selection.plainText,
  });
  host.showStatus('Creating extract...');

  return {
    generation: host.getLoadGeneration(),
    sourcePath: host.getCurrentPath(),
    selection: selectionSnapshot,
  };
}
