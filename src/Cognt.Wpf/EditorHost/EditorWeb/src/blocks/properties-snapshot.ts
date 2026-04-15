import type { BlockSnapshot, DocSnapshot } from '@blocksuite/store';
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

function isBlockSnapshot(value: unknown): value is BlockSnapshot {
  return isRecord(value)
    && value.type === 'block'
    && typeof value.id === 'string'
    && typeof value.flavour === 'string'
    && isRecord(value.props)
    && Array.isArray(value.children);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
