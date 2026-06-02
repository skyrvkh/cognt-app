import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import type {
  BlockSnapshot,
  DocSnapshot,
} from '@blocksuite/store';
import {
  createBlockId,
  getSnapshotTextDelta,
  getSnapshotTextValue,
  isPlainRecord,
} from '../documents/block-suite-document';
import {
  findRawCogntInlineMentionMatches,
  getLinkedNoteTitle,
} from './linked-note-paths';
import { createLinkedPageReference } from './linked-note-reference';
import { reportDiagnostic } from '../host';

export function upgradeSerializedRawCogntInlineMentionSnapshots(snapshot: DocSnapshot) {
  reportDiagnostic('info', 'Upgrading serialized raw Cognt inline mention snapshots.');
  upgradeBlockSnapshotRawCogntInlineMentions(snapshot.blocks);
  reportDiagnostic('info', 'Serialized raw Cognt inline mention snapshot upgrade completed.');
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
    reportDiagnostic(
      'info',
      `Serialized raw Cognt inline mentions upgraded in snapshot block (block=${snapshot.id}, oldDelta=${delta.length}, newDelta=${upgradedDelta.length}).`
    );
  }

  snapshot.children.forEach(upgradeBlockSnapshotRawCogntInlineMentions);
}

function upgradeRawCogntInlineMentionDelta(delta: unknown[]) {
  let changed = false;
  const upgraded: unknown[] = [];

  for (const entry of delta) {
    if (!isPlainRecord(entry) || typeof entry.insert !== 'string' || !entry.insert.includes('[[')) {
      upgraded.push(entry);
      continue;
    }

    const segments = splitRawCogntInlineMentionEntry(entry as Record<string, unknown>);
    if (segments === null) {
      reportDiagnostic('warn', 'Raw Cognt inline mention entry contained markers but produced no segments.');
      upgraded.push(entry);
      continue;
    }

    changed = true;
    reportDiagnostic('info', `Raw Cognt inline mention delta upgraded (segments=${segments.length}).`);
    upgraded.push(...segments);
  }

  return changed ? upgraded : delta;
}

function splitRawCogntInlineMentionEntry(entry: Record<string, unknown>) {
  const insert = entry.insert;
  if (typeof insert !== 'string') {
    return null;
  }

  const matches = findRawCogntInlineMentionMatches(insert);
  if (matches.length === 0) {
    reportDiagnostic('warn', 'Raw Cognt inline mention split skipped: no matches.');
    return null;
  }

  reportDiagnostic('info', `Splitting raw Cognt inline mention entry (matches=${matches.length}).`);
  const segments: unknown[] = [];
  let lastIndex = 0;

  for (const match of matches) {
    if (match.index > lastIndex) {
      segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex, match.index)));
    }

    segments.push({
      insert: REFERENCE_NODE,
      attributes: {
        reference: createLinkedPageReference(
          createBlockId(),
          match.relativePath,
          getLinkedNoteTitle(match.relativePath)
        ),
      },
    });

    lastIndex = match.index + match.length;
  }

  if (lastIndex < insert.length) {
    segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex)));
  }

  return segments;
}

function createTextDeltaSegment(source: Record<string, unknown>, insert: string) {
  const segment: Record<string, unknown> = { insert };
  if (isPlainRecord(source.attributes)) {
    segment.attributes = source.attributes;
  }

  return segment;
}
