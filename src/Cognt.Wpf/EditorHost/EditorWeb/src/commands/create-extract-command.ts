import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  createBlockSuiteDocumentJsonFromPlainText,
  createTitleFromPlainText,
  normalizeSelectionText,
} from '../documents/block-suite-document';
import {
  createBlockSelectionExtract,
  createTextSelectionExtract,
  getCurrentSelection,
  getOrderedTextSelectionPoints,
} from '../documents/selection-export';
import { reportDiagnostic } from '../host';

export type ExtractTextReplacement = {
  kind: 'text';
  blockId: string;
  index: number;
  length: number;
};

export type ExtractBlocksReplacement = {
  kind: 'blocks';
  blockIds: string[];
};

export type ExtractTextRangeReplacement = {
  kind: 'text-range';
  start: {
    blockId: string;
    index: number;
    length: number;
  };
  end: {
    blockId: string;
    index: number;
    length: number;
  };
  blockIds: string[];
};

export type ExtractReplacement =
  | ExtractTextReplacement
  | ExtractTextRangeReplacement
  | ExtractBlocksReplacement;

export type PendingExtract = {
  generation: number;
  sourcePath: string;
  replacement: ExtractReplacement;
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
  getBlockSelectionBlockIds(): string[];
  createExtract(message: CreateExtractCommandMessage): void;
  showStatus(message: string): void;
};

export async function runCreateExtractCommand(
  host: CreateExtractCommandHost,
  event: KeyboardEvent
): Promise<PendingExtract | null> {
  event.preventDefault();
  event.stopPropagation();

  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before extracting text.');
    return null;
  }

  const currentDoc = host.getCurrentDoc();
  if (!currentDoc) {
    host.showStatus('Open a note before extracting content.');
    return null;
  }

  const sourcePath = host.getCurrentPath();
  const generation = host.getLoadGeneration();
  const editor = host.getEditor();
  const textSelection = host.getTextSelection();
  if (textSelection && !textSelection.isCollapsed()) {
    return runCreateTextExtractCommand(
      host,
      currentDoc,
      editor,
      sourcePath,
      generation,
      textSelection);
  }

  return runCreateBlockExtractCommand(
    host,
    currentDoc,
    sourcePath,
    generation,
    host.getBlockSelectionBlockIds());
}

async function runCreateTextExtractCommand(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  editor: PageEditor,
  sourcePath: string,
  generation: number,
  textSelection: TextSelection
): Promise<PendingExtract | null> {
  const fastExtract = tryCreateSameBlockTextExtract(
    host,
    sourcePath,
    generation,
    textSelection);
  if (fastExtract) {
    return fastExtract;
  }

  host.showStatus('Preparing extract...');
  await yieldExtractPreparation();
  if (!isStillCurrentExtractSource(host, sourcePath, generation)) {
    host.showStatus('Extract cancelled because the note changed.');
    return null;
  }

  const extract = createTextSelectionExtract(
    currentDoc,
    editor,
    textSelection
  );
  const selectedPlainText = extract.selectedPlainText;
  if (normalizeSelectionText(selectedPlainText).length === 0) {
    host.showStatus('The selected text is empty.');
    reportDiagnostic(
      'warn',
      'Extract cancelled: selected text range resolved to empty content.'
    );
    return null;
  }

  const selectedContent = extract.selectedContent ?? createPlainTextExtractContent(selectedPlainText);
  const replacement = createTextSelectionReplacement(
    host,
    currentDoc,
    textSelection,
    extract.blockIds);
  if (!replacement) {
    return null;
  }

  return createPendingExtract(host, sourcePath, generation, selectedPlainText, selectedContent, replacement);
}

function tryCreateSameBlockTextExtract(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number,
  textSelection: TextSelection
): PendingExtract | null {
  const points = getOrderedTextSelectionPoints(textSelection);
  if (points.start.blockId !== points.end.blockId) {
    return null;
  }

  const replacement = getTextSelectionReplacement(points);
  const selectedPlainText = getCurrentSelection()?.plainText ?? '';
  if (!replacement || normalizeSelectionText(selectedPlainText).length === 0) {
    return null;
  }

  const selectedContent = createPlainTextExtractContent(selectedPlainText);
  reportDiagnostic(
    'info',
    `Extract using same-block text fast path (text=${selectedPlainText.length}, json=${selectedContent.length}).`
  );
  return createPendingExtract(host, sourcePath, generation, selectedPlainText, selectedContent, replacement);
}

function createTextSelectionReplacement(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  textSelection: TextSelection,
  selectedBlockIds: string[]
): ExtractReplacement | null {
  const points = getOrderedTextSelectionPoints(textSelection);

  if (points.start.blockId === points.end.blockId) {
    const replacement = getTextSelectionReplacement(points);
    if (!replacement) {
      host.showStatus('Ctrl+X needs selected text.');
      return null;
    }

    return replacement;
  }

  const textRangeSelection = getReplaceableBlockSelection(
    currentDoc,
    [
      points.start.blockId,
      ...selectedBlockIds,
      points.end.blockId,
    ]
  );
  if (textRangeSelection.error) {
    const message = textRangeSelection.error === blockParentMismatchMessage
      ? 'Extract text ranges across sibling blocks only.'
      : textRangeSelection.error;
    host.showStatus(message);
    reportDiagnostic('warn', `Extract cancelled: ${message}`);
    return null;
  }

  if (textRangeSelection.blockIds.length < 2) {
    host.showStatus('Ctrl+X needs selected text or a selected block.');
    return null;
  }

  return {
    kind: 'text-range',
    start: points.start,
    end: points.end,
    blockIds: textRangeSelection.blockIds,
  };
}

async function runCreateBlockExtractCommand(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  sourcePath: string,
  generation: number,
  selectedBlockIds: string[]
): Promise<PendingExtract | null> {
  const selection = getReplaceableBlockSelection(currentDoc, selectedBlockIds);
  if (selection.error) {
    host.showStatus(selection.error);
    reportDiagnostic('warn', `Extract cancelled: ${selection.error}`);
    return null;
  }

  if (selection.blockIds.length === 0) {
    host.showStatus('Ctrl+X needs selected text or a selected block.');
    return null;
  }

  host.showStatus('Preparing extract...');
  await yieldExtractPreparation();
  if (!isStillCurrentExtractSource(host, sourcePath, generation)) {
    host.showStatus('Extract cancelled because the note changed.');
    return null;
  }

  const extract = createBlockSelectionExtract(currentDoc, selection.blockIds);
  const selectedPlainText = extract.selectedPlainText;
  if (normalizeSelectionText(selectedPlainText).length === 0) {
    host.showStatus('The selected block does not contain extractable text.');
    reportDiagnostic(
      'warn',
      `Extract cancelled: block selection contained no text (blocks=${selection.blockIds.length}).`
    );
    return null;
  }

  const selectedContent = extract.selectedContent ?? createPlainTextExtractContent(selectedPlainText);

  return createPendingExtract(
    host,
    sourcePath,
    generation,
    selectedPlainText,
    selectedContent,
    {
      kind: 'blocks',
      blockIds: selection.blockIds,
    });
}

function createPendingExtract(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number,
  selectedPlainText: string,
  selectedContent: string,
  replacement: ExtractReplacement
): PendingExtract {
  host.createExtract({
    type: 'createExtract',
    sourcePath,
    selectedContent,
    selectedPlainText,
  });
  reportDiagnostic(
    'info',
    `Extract payload: mode=${replacement.kind}, text=${selectedPlainText.length}, json=${selectedContent.length}.`
  );
  host.showStatus('Creating extract...');

  return {
    generation,
    sourcePath,
    replacement,
  };
}

function isStillCurrentExtractSource(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number
) {
  return host.getCurrentPath() === sourcePath
    && host.getLoadGeneration() === generation;
}

function yieldExtractPreparation() {
  return new Promise<void>(resolve => {
    const resolveAfterTimer = () => window.setTimeout(resolve, 0);
    if (typeof window.requestAnimationFrame === 'function') {
      window.requestAnimationFrame(() => resolveAfterTimer());
      return;
    }

    resolveAfterTimer();
  });
}

function createPlainTextExtractContent(selectedPlainText: string) {
  return createBlockSuiteDocumentJsonFromPlainText(
    selectedPlainText,
    createTitleFromPlainText(selectedPlainText, 'Extract')
  );
}

function getTextSelectionReplacement(
  points: {
    start: { blockId: string; index: number; length: number };
    end: { blockId: string; index: number; length: number };
  }
): ExtractTextReplacement | null {
  const { start, end } = points;
  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    getSelectionPointEndOffset(start),
    getSelectionPointEndOffset(end)
  );
  const length = selectionEnd - index;

  return length > 0
    ? {
        kind: 'text',
        blockId: start.blockId,
        index,
        length,
      }
    : null;
}

const blockParentMismatchMessage = 'Extract selected blocks from one parent at a time.';

function getReplaceableBlockSelection(doc: Doc, blockIds: string[]) {
  const uniqueBlockIds = getUniqueBlockIds(blockIds);
  const entries = uniqueBlockIds.flatMap(blockId => {
    const model = doc.getBlockById(blockId);
    const parent = model ? doc.getParent(model) : null;
    return model && parent ? [{ blockId, model, parent }] : [];
  });

  if (entries.length !== uniqueBlockIds.length) {
    return {
      blockIds: [],
      error: 'One of the selected blocks is no longer available.',
    };
  }

  const [firstEntry] = entries;
  if (firstEntry && entries.some(entry => entry.parent !== firstEntry.parent)) {
    return {
      blockIds: [],
      error: blockParentMismatchMessage,
    };
  }

  return {
    blockIds: uniqueBlockIds,
    error: null,
  };
}

function getSelectionPointEndOffset(point: { index: number; length: number }) {
  return point.index + Math.max(0, point.length);
}

function getUniqueBlockIds(values: string[]) {
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    const blockId = value.trim();
    if (!blockId || seen.has(blockId)) {
      continue;
    }

    seen.add(blockId);
    ids.push(blockId);
  }

  return ids;
}
