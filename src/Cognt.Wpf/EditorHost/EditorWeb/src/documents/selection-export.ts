import { type PageEditor } from '@blocksuite/presets';
import {
  Job,
  type BlockModel,
  type BlockSnapshot,
  type Doc,
  type DraftModel,
} from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  createBlockSuiteDocumentJsonFromBlockSnapshots,
  createTitleFromPlainText,
  getSnapshotTextDelta,
  getSnapshotTextValue,
  normalizeSelectionText,
} from './block-suite-document';

export type TextSelectionPointSnapshot = {
  blockId: string;
  index: number;
  length: number;
};

export type TextSelectionSnapshot = {
  from: TextSelectionPointSnapshot;
  to: TextSelectionPointSnapshot | null;
  start?: TextSelectionPointSnapshot;
  end?: TextSelectionPointSnapshot;
};

export function getCurrentSelection() {
  const selectedText = window.getSelection()?.toString() ?? '';
  const plainText = normalizeSelectionText(selectedText);

  if (plainText.length === 0) {
    return null;
  }

  return {
    plainText,
  };
}

export function createBlockSuiteDocumentJsonFromSelection(
  doc: Doc,
  editor: PageEditor,
  plainText: string
) {
  const textSelection = editor.std?.selection.find('text') as TextSelection | null | undefined;
  if (!textSelection || textSelection.isCollapsed()) {
    return null;
  }

  const models = getSelectedTextBlockModels(editor, doc, textSelection);
  if (models.length === 0) {
    return null;
  }

  const job = new Job({ collection: doc.collection });
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  const snapshots = models
    .map(model => job.blockToSnapshot(model as DraftModel))
    .filter(isBlockSnapshot)
    .map(cloneBlockSnapshot)
    .map(snapshot => trimSnapshotForTextSelection(snapshot, start, end));

  if (snapshots.length === 0 || !snapshots.some(hasSnapshotBodyContent)) {
    return null;
  }

  return createBlockSuiteDocumentJsonFromBlockSnapshots(
    snapshots,
    createTitleFromPlainText(plainText, 'Extract')
  );
}

export function cloneTextSelection(selection: TextSelection): TextSelectionSnapshot {
  return {
    from: cloneTextSelectionPoint(selection.from),
    to: selection.to ? cloneTextSelectionPoint(selection.to) : null,
    start: selection.start ? cloneTextSelectionPoint(selection.start) : undefined,
    end: selection.end ? cloneTextSelectionPoint(selection.end) : undefined,
  };
}

export function getTextSelectionSnapshotRange(selection: TextSelectionSnapshot) {
  const start = selection.start ?? selection.from;
  const end = selection.end ?? selection.to ?? selection.from;

  if (start.blockId !== end.blockId) {
    return null;
  }

  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    start.index + Math.max(0, start.length),
    end.index + Math.max(0, end.length)
  );
  const length = selectionEnd - index;

  return length > 0
    ? {
        blockId: start.blockId,
        index,
        length,
      }
    : null;
}

function cloneTextSelectionPoint(point: TextSelectionPointSnapshot): TextSelectionPointSnapshot {
  return {
    blockId: point.blockId,
    index: point.index,
    length: point.length,
  };
}

function getSelectedTextBlockModels(
  editor: PageEditor,
  doc: Doc,
  textSelection: TextSelection
) {
  const range = editor.std?.range.textSelectionToRange(textSelection as never);
  const components = range
    ? editor.std.range.getSelectedBlockComponentsByRange(range, {
        match: (element: { model?: { role?: string } }) => element.model?.role === 'content',
        mode: 'flat',
      })
    : [];

  const models = components
    .map((component: { model?: BlockModel }) => component.model)
    .filter((model): model is BlockModel => Boolean(model));

  if (models.length > 0) {
    return models;
  }

  const fallback = doc.getBlock(textSelection.from.blockId)?.model;
  return fallback ? [fallback as BlockModel] : [];
}

function getTextSelectionStart(textSelection: TextSelection) {
  return textSelection.start
    ?? (isReverseTextSelection(textSelection) ? (textSelection.to ?? textSelection.from) : textSelection.from);
}

function getTextSelectionEnd(textSelection: TextSelection) {
  return textSelection.end
    ?? (isReverseTextSelection(textSelection) ? textSelection.from : (textSelection.to ?? textSelection.from));
}

function isReverseTextSelection(textSelection: TextSelection) {
  return Boolean((textSelection as TextSelection & { reverse?: boolean }).reverse);
}

function cloneBlockSnapshot(snapshot: BlockSnapshot) {
  return JSON.parse(JSON.stringify(snapshot)) as BlockSnapshot;
}

function trimSnapshotForTextSelection(
  snapshot: BlockSnapshot,
  start: TextSelectionPointSnapshot,
  end: TextSelectionPointSnapshot
) {
  if (start.blockId === end.blockId) {
    if (snapshot.id === start.blockId) {
      const range = getSameBlockSelectionRange(start, end);
      trimSnapshotText(snapshot, range.index, range.length);
    }
  } else if (snapshot.id === start.blockId) {
    trimSnapshotText(snapshot, start.index, getSnapshotTextLength(snapshot) - start.index);
  } else if (snapshot.id === end.blockId) {
    trimSnapshotText(snapshot, 0, getSelectionPointEndOffset(end));
  }

  snapshot.children = snapshot.children.map(child => trimSnapshotForTextSelection(child, start, end));
  return snapshot;
}

function getSameBlockSelectionRange(
  start: { index: number; length: number },
  end: { index: number; length: number }
) {
  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    getSelectionPointEndOffset(start),
    getSelectionPointEndOffset(end)
  );

  return {
    index,
    length: Math.max(0, selectionEnd - index),
  };
}

function getSelectionPointEndOffset(point: { index: number; length: number }) {
  return point.index + Math.max(0, point.length);
}

function getSnapshotTextLength(snapshot: BlockSnapshot) {
  return getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .reduce((length, entry) => (
      isRecord(entry) && typeof entry.insert === 'string'
        ? length + entry.insert.length
        : length
    ), 0);
}

function hasSnapshotBodyContent(snapshot: BlockSnapshot): boolean {
  const hasText = getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .some(entry => isRecord(entry) && typeof entry.insert === 'string' && entry.insert.trim().length > 0);

  return hasText || snapshot.children.some(hasSnapshotBodyContent);
}

function trimSnapshotText(snapshot: BlockSnapshot, index: number, length: number) {
  const textValue = getSnapshotTextValue(snapshot);
  if (!textValue) {
    return;
  }

  snapshot.props = {
    ...snapshot.props,
    text: {
      ...textValue,
      delta: sliceTextDelta(getSnapshotTextDelta(textValue), index, length),
    },
  };
}

function sliceTextDelta(delta: unknown[], index: number, length: number) {
  const start = Math.max(0, index);
  const end = Math.max(start, start + Math.max(0, length));
  const result: Array<Record<string, unknown>> = [];
  let offset = 0;

  for (const entry of delta) {
    if (!isRecord(entry) || typeof entry.insert !== 'string') {
      continue;
    }

    const insert = entry.insert;
    const entryStart = offset;
    const entryEnd = offset + insert.length;
    const overlapStart = Math.max(start, entryStart);
    const overlapEnd = Math.min(end, entryEnd);

    if (overlapEnd > overlapStart) {
      result.push({
        ...entry,
        insert: insert.slice(overlapStart - entryStart, overlapEnd - entryStart),
      });
    }

    offset = entryEnd;
  }

  return result;
}

function isBlockSnapshot(value: unknown): value is BlockSnapshot {
  return isRecord(value)
    && value.type === 'block'
    && typeof value.id === 'string'
    && typeof value.flavour === 'string'
    && isRecord(value.props)
    && Array.isArray(value.children);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
