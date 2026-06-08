import { type PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  createBlockSuiteDocumentJsonFromBlockSnapshots,
  createTitleFromPlainText,
  normalizeSelectionText,
} from './block-suite-document';
import { reportDiagnostic } from '../host';
import {
  createSnapshotsFromBlockSelection,
} from './selection-export-block-snapshots';
import {
  getSnapshotPlainText,
  hasSnapshotBodyContent,
  prepareExtractSnapshots,
} from './selection-export-content';
import {
  getSelectedTextBlockModels,
} from './selection-export-models';
import {
  createSnapshotsFromTextBlockModels,
  createSnapshotsFromTextSelection,
} from './selection-export-text-snapshots';
import {
  cloneTextSelection,
  getOrderedTextSelectionPoints,
  getTextSelectionSnapshotRange,
} from './selection-export-selection';

export {
  cloneTextSelection,
  getOrderedTextSelectionPoints,
  getTextSelectionSnapshotRange,
};

export type {
  BlockSelectionExtract,
  BlockSelectionSnapshot,
  CurrentSelection,
  TextSelectionExtract,
  TextSelectionPointSnapshot,
  TextSelectionSnapshot,
} from './selection-export-types';

export function getCurrentSelection() {
  const selectedText = window.getSelection()?.toString() ?? '';
  const plainText = normalizeSelectionText(selectedText);

  if (plainText.length === 0) {
    reportDiagnostic('info', 'Current browser selection read returned no normalized text.');
    return null;
  }

  reportDiagnostic('info', `Current browser selection read (text=${plainText.length}).`);
  return {
    plainText,
  };
}

export function getPlainTextFromTextSelection(
  doc: Doc,
  editor: PageEditor,
  textSelection: TextSelection
) {
  const snapshots = createSnapshotsFromTextSelection(doc, editor, textSelection);
  const text = snapshots
    .map(getSnapshotPlainText)
    .filter(text => text.length > 0)
    .join(' ');
  reportDiagnostic(
    'info',
    `Plain text extracted from text selection (snapshots=${snapshots.length}, text=${text.length}).`
  );
  return text;
}

export function createTextSelectionExtract(
  doc: Doc,
  editor: PageEditor,
  textSelection: TextSelection
) {
  reportDiagnostic('info', 'Creating text selection extract.');
  const models = getSelectedTextBlockModels(editor, doc, textSelection);
  const snapshots = createSnapshotsFromTextBlockModels(doc, models, textSelection);
  const selectedPlainText = snapshots
    .map(getSnapshotPlainText)
    .filter(text => text.length > 0)
    .join(' ');
  const contentSnapshots = prepareExtractSnapshots(snapshots);
  const selectedContent = contentSnapshots.length > 0
    && contentSnapshots.some(hasSnapshotBodyContent)
    ? createBlockSuiteDocumentJsonFromBlockSnapshots(
        contentSnapshots,
        createTitleFromPlainText(selectedPlainText, 'Extract')
      )
    : null;

  reportDiagnostic(
    'info',
    `Text selection extract created (models=${models.length}, snapshots=${snapshots.length}, text=${selectedPlainText.length}, hasContent=${Boolean(selectedContent)}).`
  );
  return {
    selectedPlainText,
    selectedContent,
    blockIds: models
      .map(model => model.id)
      .filter((blockId): blockId is string => typeof blockId === 'string' && blockId.length > 0),
  };
}

export function getBlockIdsFromTextSelection(
  doc: Doc,
  editor: PageEditor,
  textSelection: TextSelection
) {
  const ids = getSelectedTextBlockModels(editor, doc, textSelection)
    .map(model => model.id)
    .filter((blockId): blockId is string => typeof blockId === 'string' && blockId.length > 0);
  reportDiagnostic('info', `Block ids extracted from text selection (count=${ids.length}).`);
  return ids;
}

export function createBlockSuiteDocumentJsonFromSelection(
  doc: Doc,
  editor: PageEditor,
  plainText: string,
  selection?: TextSelection
) {
  reportDiagnostic('info', `Creating BlockSuite JSON from selection (plainText=${plainText.length}).`);
  const textSelection = selection
    ?? editor.std?.selection.find('text') as TextSelection | null | undefined;
  if (!textSelection || textSelection.isCollapsed()) {
    reportDiagnostic('info', 'BlockSuite JSON selection export skipped: text selection is missing or collapsed.');
    return null;
  }

  const snapshots = createSnapshotsFromTextSelection(doc, editor, textSelection);
  const contentSnapshots = prepareExtractSnapshots(snapshots);
  if (contentSnapshots.length === 0 || !contentSnapshots.some(hasSnapshotBodyContent)) {
    reportDiagnostic(
      'info',
      `BlockSuite JSON selection export skipped: no body content (snapshots=${snapshots.length}, prepared=${contentSnapshots.length}).`
    );
    return null;
  }

  const content = createBlockSuiteDocumentJsonFromBlockSnapshots(
    contentSnapshots,
    createTitleFromPlainText(plainText, 'Extract')
  );
  reportDiagnostic(
    'info',
    `BlockSuite JSON selection export completed (snapshots=${contentSnapshots.length}, length=${content.length}).`
  );
  return content;
}

export function createBlockSuiteDocumentJsonFromBlockSelection(
  doc: Doc,
  blockIds: string[]
) {
  reportDiagnostic('info', `Creating BlockSuite JSON from block selection (requestedBlocks=${blockIds.length}).`);
  const content = createBlockSelectionExtract(doc, blockIds).selectedContent;
  reportDiagnostic('info', `Block selection JSON export completed (hasContent=${Boolean(content)}, length=${content?.length ?? 0}).`);
  return content;
}

export function getPlainTextFromBlockSelection(doc: Doc, blockIds: string[]) {
  const text = createBlockSelectionExtract(doc, blockIds).selectedPlainText;
  reportDiagnostic('info', `Plain text extracted from block selection (requestedBlocks=${blockIds.length}, text=${text.length}).`);
  return text;
}

export function createBlockSelectionExtract(
  doc: Doc,
  blockIds: string[]
) {
  reportDiagnostic('info', `Creating block selection extract (requestedBlocks=${blockIds.length}).`);
  const snapshots = createSnapshotsFromBlockSelection(doc, blockIds);
  const contentSnapshots = prepareExtractSnapshots(snapshots);
  const selectedPlainText = contentSnapshots
    .map(getSnapshotPlainText)
    .filter(text => text.length > 0)
    .join(' ');
  const selectedContent = contentSnapshots.length > 0
    && contentSnapshots.some(hasSnapshotBodyContent)
    ? createBlockSuiteDocumentJsonFromBlockSnapshots(
        contentSnapshots,
        createTitleFromPlainText(selectedPlainText, 'Extract')
      )
    : null;

  reportDiagnostic(
    'info',
    `Block selection extract created (snapshots=${snapshots.length}, prepared=${contentSnapshots.length}, text=${selectedPlainText.length}, hasContent=${Boolean(selectedContent)}).`
  );
  return {
    selectedPlainText,
    selectedContent,
  };
}
