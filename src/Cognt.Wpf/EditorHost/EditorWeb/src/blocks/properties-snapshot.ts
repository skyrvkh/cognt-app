import type { BlockSnapshot, DocSnapshot } from '@blocksuite/store';
import { isBlockSnapshot } from '../documents/block-suite-document';
import { cogntPropertiesFlavour } from './properties-schema';
import { normalizeCogntProperties } from './properties-normalize';

export function upgradeSerializedCogntPropertiesSnapshots(snapshot: DocSnapshot) {
  upgradeSerializedCogntPropertiesBlockSnapshot(snapshot.blocks);
}

function upgradeSerializedCogntPropertiesBlockSnapshot(snapshot: BlockSnapshot) {
  const props = snapshot.props;
  const isLegacyPropertiesParagraph =
    snapshot.flavour === 'affine:paragraph'
    && props.cogntBlockKind === 'properties'
    && Array.isArray(props.cogntProperties);

  if (snapshot.flavour === cogntPropertiesFlavour || isLegacyPropertiesParagraph) {
    snapshot.flavour = cogntPropertiesFlavour;
    snapshot.version = 1;
    snapshot.props = {
      title: typeof props.title === 'string' ? props.title : 'Properties',
      properties: normalizeCogntProperties(
        Array.isArray(props.cogntProperties) ? props.cogntProperties : props.properties
      ),
    };
    snapshot.children = [];
    return;
  }

  snapshot.children.forEach(child => {
    if (isBlockSnapshot(child)) {
      upgradeSerializedCogntPropertiesBlockSnapshot(child);
    }
  });
}
