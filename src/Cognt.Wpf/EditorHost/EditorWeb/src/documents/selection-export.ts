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

/*
 * Superseded monolithic selection exporter retained temporarily while its
 * responsibilities are removed in independently buildable milestones.
 *
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

  snapshot.children = snapshot.children.map((child: BlockSnapshot) =>
    trimSnapshotForTextSelection(child, start, end)
  );
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

function hasSnapshotBodyContent(snapshot: BlockSnapshot): boolean {
  const hasText = getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .some(entry => isPlainRecord(entry) && typeof entry.insert === 'string' && entry.insert.trim().length > 0);

  return hasText || snapshot.children.some(hasSnapshotBodyContent);
}

function normalizeExtractSnapshots(snapshots: BlockSnapshot[]): BlockSnapshot[] {
  return snapshots.flatMap(snapshot => {
    if (snapshot.flavour === 'affine:surface') {
      return [];
    }

    if (snapshot.flavour === 'affine:page' || snapshot.flavour === 'affine:note') {
      return snapshot.children
        .filter(isBlockSnapshot)
        .flatMap(child => normalizeExtractSnapshots([child]));
    }

    return [snapshot];
  });
}

function prepareExtractSnapshots(snapshots: BlockSnapshot[]): BlockSnapshot[] {
  return regenerateSnapshotIds(
    removeSnapshotsNestedInOtherSelectedSnapshots(normalizeExtractSnapshots(snapshots))
  );
}

function removeSnapshotsNestedInOtherSelectedSnapshots(snapshots: BlockSnapshot[]): BlockSnapshot[] {
  const filtered: BlockSnapshot[] = [];
  const seenTopLevelIds = new Set<string>();
  const descendantIds = new Set<string>();

  for (const snapshot of snapshots) {
    collectDescendantBlockIds(snapshot, descendantIds);
  }

  for (const snapshot of snapshots) {
    if (seenTopLevelIds.has(snapshot.id) || descendantIds.has(snapshot.id)) {
      continue;
    }

    filtered.push(snapshot);
    seenTopLevelIds.add(snapshot.id);
  }

  return filtered;
}

function collectDescendantBlockIds(snapshot: BlockSnapshot, ids: Set<string>) {
  for (const child of snapshot.children.filter(isBlockSnapshot)) {
    ids.add(child.id);
    collectDescendantBlockIds(child, ids);
  }
}

function regenerateSnapshotIds(snapshots: BlockSnapshot[]): BlockSnapshot[] {
  return snapshots.map(regenerateSnapshotId);
}

function regenerateSnapshotId(snapshot: BlockSnapshot): BlockSnapshot {
  const nextSnapshot = cloneBlockSnapshot(snapshot);
  nextSnapshot.id = createExtractBlockId();
  nextSnapshot.children = nextSnapshot.children
    .filter(isBlockSnapshot)
    .map(regenerateSnapshotId);
  return nextSnapshot;
}

function createExtractBlockId() {
  return `extract-${crypto.randomUUID().replaceAll('-', '').slice(0, 12)}`;
}

function getSnapshotPlainText(snapshot: BlockSnapshot): string {
  const ownText = getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .flatMap(entry => (
      isPlainRecord(entry) && typeof entry.insert === 'string'
        ? [entry.insert]
        : []
    ))
    .join('');
  const childText = snapshot.children
    .filter(isBlockSnapshot)
    .map(getSnapshotPlainText)
    .filter(text => text.length > 0)
    .join(' ');

  return normalizeSelectionText([ownText, childText].filter(Boolean).join(' '));
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

function uniqueBlockIds(values: string[]) {
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

function uniqueModels(models: BlockModel[]) {
  const result: BlockModel[] = [];
  const seen = new Set<BlockModel>();
  for (const model of models) {
    if (seen.has(model)) {
      continue;
    }

    seen.add(model);
    result.push(model);
  }

  return result;
}

function isSelectableTextBlockModel(value: unknown): value is BlockModel {
  return typeof value === 'object'
    && value !== null
    && typeof (value as { id?: unknown }).id === 'string'
    && Boolean((value as { text?: unknown }).text);
}
*/
