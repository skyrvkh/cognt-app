import {
  Job,
  type BlockModel,
  type Doc,
  type DraftModel,
} from '@blocksuite/store';
import { isBlockSnapshot } from './block-suite-document';
import { reportDiagnostic } from '../host';
import { cloneBlockSnapshot } from './selection-export-shared';

export function createSnapshotsFromBlockSelection(
  doc: Doc,
  blockIds: string[]
) {
  const job = new Job({ collection: doc.collection });
  const uniqueIds = uniqueBlockIds(blockIds);
  const snapshots = uniqueIds
    .map(blockId => doc.getBlock(blockId)?.model)
    .filter((model: BlockModel | undefined): model is BlockModel => Boolean(model))
    .map((model: BlockModel) => job.blockToSnapshot(model as DraftModel))
    .filter(isBlockSnapshot)
    .map(cloneBlockSnapshot);
  reportDiagnostic(
    'info',
    `Created snapshots from block selection (requested=${blockIds.length}, unique=${uniqueIds.length}, snapshots=${snapshots.length}).`
  );
  return snapshots;
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
