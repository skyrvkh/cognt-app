import type { BlockSnapshot } from '@blocksuite/store';
import {
  getSnapshotTextDelta,
  getSnapshotTextValue,
  isBlockSnapshot,
  isPlainRecord,
  normalizeSelectionText,
} from './block-suite-document';
import { cloneBlockSnapshot } from './selection-export-shared';

export function hasSnapshotBodyContent(snapshot: BlockSnapshot): boolean {
  const hasText = getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .some(entry => isPlainRecord(entry) && typeof entry.insert === 'string' && entry.insert.trim().length > 0);

  return hasText || snapshot.children.some(hasSnapshotBodyContent);
}

export function prepareExtractSnapshots(snapshots: BlockSnapshot[]): BlockSnapshot[] {
  return regenerateSnapshotIds(
    removeSnapshotsNestedInOtherSelectedSnapshots(normalizeExtractSnapshots(snapshots))
  );
}

export function getSnapshotPlainText(snapshot: BlockSnapshot): string {
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
