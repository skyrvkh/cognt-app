import type { BlockSnapshot, DocSnapshot } from '@blocksuite/store';
import { isBlockSnapshot } from '../documents/block-suite-document';
import { reportDiagnostic } from '../host';
import { cogntReferencesFlavour } from './references-schema';

export function stripSerializedCogntReferencesSnapshots(snapshot: DocSnapshot) {
  const removedCount = stripSerializedCogntReferencesBlockSnapshots(snapshot.blocks);
  reportDiagnostic('info', 'Stripped serialized Cognt references snapshots.', {
    removedCount,
  });
}

function stripSerializedCogntReferencesBlockSnapshots(snapshot: BlockSnapshot) {
  let removedCount = 0;
  snapshot.children = snapshot.children.filter(child => {
    if (!isBlockSnapshot(child)) {
      return true;
    }

    if (child.flavour === cogntReferencesFlavour) {
      removedCount += 1;
      reportDiagnostic('info', 'Removed serialized Cognt references block snapshot.', {
        flavour: child.flavour,
      });
      return false;
    }

    removedCount += stripSerializedCogntReferencesBlockSnapshots(child);
    return true;
  });
  return removedCount;
}
