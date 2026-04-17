import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import type {
  BlockSnapshot,
  DocSnapshot,
} from '@blocksuite/store';
import {
  createBlockId,
  getSnapshotTextDelta,
  getSnapshotTextValue,
} from '../documents/block-suite-document';
import { parseNoteMentionTarget } from '../mentions';
import {
  getLinkedNoteTitle,
  isCogntNoteRelativePath,
  isRecord,
} from './linked-note-paths';
import { createLinkedPageReference } from './linked-note-insert';

export function upgradeSerializedRawCogntInlineMentionSnapshots(snapshot: DocSnapshot) {
  upgradeBlockSnapshotRawCogntInlineMentions(snapshot.blocks);
}

function upgradeBlockSnapshotRawCogntInlineMentions(snapshot: BlockSnapshot) {
  const textValue = getSnapshotTextValue(snapshot);
  const delta = textValue ? getSnapshotTextDelta(textValue) : [];
  const upgradedDelta = upgradeRawCogntInlineMentionDelta(delta);

  if (upgradedDelta !== delta && textValue) {
    snapshot.props = {
      ...snapshot.props,
      text: {
        ...textValue,
        delta: upgradedDelta,
      },
    };
  }

  snapshot.children.forEach(upgradeBlockSnapshotRawCogntInlineMentions);
}

function upgradeRawCogntInlineMentionDelta(delta: unknown[]) {
  let changed = false;
  const upgraded: unknown[] = [];

  for (const entry of delta) {
    if (!isRecord(entry) || typeof entry.insert !== 'string' || !entry.insert.includes('[[')) {
      upgraded.push(entry);
      continue;
    }

    const segments = splitRawCogntInlineMentionEntry(entry as Record<string, unknown>);
    if (segments === null) {
      upgraded.push(entry);
      continue;
    }

    changed = true;
    upgraded.push(...segments);
  }

  return changed ? upgraded : delta;
}

function splitRawCogntInlineMentionEntry(entry: Record<string, unknown>) {
  const insert = entry.insert;
  if (typeof insert !== 'string') {
    return null;
  }

  const expression = /\[\[([^\]\[\n]+)\]\]/g;
  const segments: unknown[] = [];
  let changed = false;
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = expression.exec(insert)) !== null) {
    const target = parseNoteMentionTarget(match[1]);
    if (!target || !isCogntNoteRelativePath(target.relativePath)) {
      continue;
    }

    if (match.index > lastIndex) {
      segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex, match.index)));
    }

    segments.push({
      insert: REFERENCE_NODE,
      attributes: {
        reference: createLinkedPageReference(
          createBlockId(),
          target.relativePath,
          getLinkedNoteTitle(target.relativePath)
        ),
      },
    });

    changed = true;
    lastIndex = match.index + match[0].length;
  }

  if (!changed) {
    return null;
  }

  if (lastIndex < insert.length) {
    segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex)));
  }

  return segments;
}

function createTextDeltaSegment(source: Record<string, unknown>, insert: string) {
  const segment: Record<string, unknown> = { insert };
  if (isRecord(source.attributes)) {
    segment.attributes = source.attributes;
  }

  return segment;
}
