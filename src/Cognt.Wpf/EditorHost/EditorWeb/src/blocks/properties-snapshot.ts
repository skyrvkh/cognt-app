import type { BlockSnapshot, DocSnapshot } from '@blocksuite/store';
import { isBlockSnapshot } from '../documents/block-suite-document';
import { reportDiagnostic } from '../host';
import { cogntPropertiesFlavour } from './properties-schema';

export function stripSerializedCogntPropertiesSnapshots(snapshot: DocSnapshot) {
  const removedCount = stripSerializedCogntPropertiesBlockSnapshot(snapshot.blocks);
  reportDiagnostic('info', 'Stripped serialized Cognt properties snapshots.', {
    removedCount,
  });
}

function stripSerializedCogntPropertiesBlockSnapshot(snapshot: BlockSnapshot) {
  if (!Array.isArray(snapshot.children) || snapshot.children.length === 0) {
    reportDiagnostic('info', 'Skipped Cognt properties snapshot strip for block without children.', {
      flavour: snapshot.flavour,
    });
    return 0;
  }

  let removedCount = 0;
  snapshot.children = snapshot.children.filter(child => {
    if (!isBlockSnapshot(child)) {
      return true;
    }

    const childProps = child.props;
    const isLegacyPropertiesParagraph =
      child.flavour === 'affine:paragraph'
      && childProps.cogntBlockKind === 'properties'
      && Array.isArray(childProps.cogntProperties);

    if (child.flavour === cogntPropertiesFlavour || isLegacyPropertiesParagraph) {
      removedCount += 1;
      reportDiagnostic('info', 'Removed serialized Cognt properties block snapshot.', {
        flavour: child.flavour,
        legacy: isLegacyPropertiesParagraph,
      });
      return false;
    }

    removedCount += stripSerializedCogntPropertiesBlockSnapshot(child);
    return true;
  });
  return removedCount;
}
