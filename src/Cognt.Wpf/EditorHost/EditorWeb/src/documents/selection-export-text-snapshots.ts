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
  getSnapshotTextDelta,
  getSnapshotTextValue,
  isBlockSnapshot,
  isPlainRecord,
} from './block-suite-document';
import { reportDiagnostic } from '../host';
import { getSelectedTextBlockModels } from './selection-export-models';
import {
  getSameBlockSelectionRange,
  getSelectionPointEndOffset,
  getTextSelectionEnd,
  getTextSelectionStart,
} from './selection-export-selection';
import { cloneBlockSnapshot } from './selection-export-shared';
import type { TextSelectionPointSnapshot } from './selection-export-types';

export function createSnapshotsFromTextSelection(
  doc: Doc,
  editor: PageEditor,
  textSelection: TextSelection
) {
  const models = getSelectedTextBlockModels(editor, doc, textSelection);
  if (models.length === 0) {
    reportDiagnostic('info', 'Text selection snapshot creation skipped: no selected text block models.');
    return [];
  }

  const snapshots = createSnapshotsFromTextBlockModels(doc, models, textSelection);
  reportDiagnostic(
    'info',
    `Created snapshots from text selection (models=${models.length}, snapshots=${snapshots.length}).`
  );
  return snapshots;
}

export function createSnapshotsFromTextBlockModels(
  doc: Doc,
  models: BlockModel[],
  textSelection: TextSelection
) {
  const job = new Job({ collection: doc.collection });
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  const snapshots = models
    .map((model: BlockModel) => createTextSelectionSnapshot(job, model, start, end))
    .filter(isBlockSnapshot);
  reportDiagnostic(
    'info',
    `Created snapshots from text block models (models=${models.length}, snapshots=${snapshots.length}).`
  );
  return snapshots;
}

function createTextSelectionSnapshot(
  job: Job,
  model: BlockModel,
  start: TextSelectionPointSnapshot,
  end: TextSelectionPointSnapshot
) {
  const directSnapshot = createDirectParagraphSelectionSnapshot(model, start, end);
  if (directSnapshot) {
    reportDiagnostic('info', `Created direct paragraph selection snapshot (block=${model.id}).`);
    return directSnapshot;
  }

  const snapshot = job.blockToSnapshot(model as DraftModel);
  const result = isBlockSnapshot(snapshot)
    ? trimSnapshotForTextSelection(snapshot, start, end)
    : snapshot;
  reportDiagnostic(
    'info',
    `Created text selection snapshot via BlockSuite job (block=${model.id}, isBlockSnapshot=${isBlockSnapshot(result)}).`
  );
  return result;
}

function createDirectParagraphSelectionSnapshot(
  model: BlockModel,
  start: TextSelectionPointSnapshot,
  end: TextSelectionPointSnapshot
): BlockSnapshot | null {
  const candidate = model as BlockModel & {
    children?: unknown[];
    collapsed?: unknown;
    type?: unknown;
    version?: unknown;
    text?: {
      toDelta?: () => unknown[];
      length?: number;
    };
  };

  if (candidate.flavour !== 'affine:paragraph'
      || !candidate.text
      || typeof candidate.id !== 'string'
      || !Array.isArray(candidate.children)
      || candidate.children.length > 0) {
    return null;
  }

  const delta = candidate.text.toDelta?.();
  if (!Array.isArray(delta)) {
    return null;
  }

  const textLength: number = typeof candidate.text.length === 'number'
    ? candidate.text.length
    : getDeltaTextLength(delta);
  const range = getModelSelectionRange(candidate.id, textLength, start, end);

  return {
    type: 'block',
    id: candidate.id,
    flavour: candidate.flavour,
    version: typeof candidate.version === 'number' ? candidate.version : 1,
    props: {
      type: typeof candidate.type === 'string' ? candidate.type : 'text',
      text: {
        '$blocksuite:internal:text$': true,
        delta: sliceTextDelta(delta, range.index, range.length),
      },
      collapsed: candidate.collapsed === true,
    },
    children: [],
  };
}

function getModelSelectionRange(
  blockId: string,
  textLength: number,
  start: TextSelectionPointSnapshot,
  end: TextSelectionPointSnapshot
) {
  if (start.blockId === end.blockId && blockId === start.blockId) {
    return getSameBlockSelectionRange(start, end);
  }

  if (blockId === start.blockId) {
    const index = clampTextIndex(start.index, textLength);
    return {
      index,
      length: Math.max(0, textLength - index),
    };
  }

  if (blockId === end.blockId) {
    return {
      index: 0,
      length: clampTextIndex(getSelectionPointEndOffset(end), textLength),
    };
  }

  return {
    index: 0,
    length: textLength,
  };
}

function clampTextIndex(index: number, textLength: number) {
  return Math.max(0, Math.min(Math.max(0, index), Math.max(0, textLength)));
}

function trimSnapshotForTextSelection(
  snapshot: BlockSnapshot,
  start: TextSelectionPointSnapshot,
  end: TextSelectionPointSnapshot
) {
  const nextSnapshot = cloneBlockSnapshot(snapshot);

  if (start.blockId === end.blockId) {
    if (nextSnapshot.id === start.blockId) {
      const range = getSameBlockSelectionRange(start, end);
      trimSnapshotText(nextSnapshot, range.index, range.length);
    }
  } else if (nextSnapshot.id === start.blockId) {
    trimSnapshotText(nextSnapshot, start.index, getSnapshotTextLength(nextSnapshot) - start.index);
  } else if (nextSnapshot.id === end.blockId) {
    trimSnapshotText(nextSnapshot, 0, getSelectionPointEndOffset(end));
  }

  nextSnapshot.children = nextSnapshot.children.map((child: BlockSnapshot) =>
    trimSnapshotForTextSelection(child, start, end)
  );
  return nextSnapshot;
}

function getSnapshotTextLength(snapshot: BlockSnapshot) {
  return getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .reduce((length, entry) => (
      isPlainRecord(entry) && typeof entry.insert === 'string'
        ? length + entry.insert.length
        : length
    ), 0);
}

function getDeltaTextLength(delta: unknown[]) {
  return delta.reduce<number>((length, entry) => (
    isPlainRecord(entry) && typeof entry.insert === 'string'
      ? length + entry.insert.length
      : length
  ), 0);
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
    if (!isPlainRecord(entry) || typeof entry.insert !== 'string') {
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
