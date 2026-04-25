import type { BlockSnapshot, DocSnapshot } from '@blocksuite/store';
import { isBlockSnapshot } from '../documents/block-suite-document';
import { cogntReferencesFlavour } from './references-schema';

export function stripSerializedCogntReferencesSnapshots(snapshot: DocSnapshot) {
  stripSerializedCogntReferencesBlockSnapshots(snapshot.blocks);
}

function stripSerializedCogntReferencesBlockSnapshots(snapshot: BlockSnapshot) {
  snapshot.children = snapshot.children.filter(child => {
    if (!isBlockSnapshot(child)) {
      return true;
    }

    if (child.flavour === cogntReferencesFlavour) {
      return false;
    }

    stripSerializedCogntReferencesBlockSnapshots(child);
    return true;
  });
}
