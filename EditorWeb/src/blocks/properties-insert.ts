import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import {
  cogntPropertiesFlavour,
  type CogntPropertyRow,
} from './properties-schema';
import { normalizeCogntProperties } from './properties-normalize';

export function insertCogntPropertiesBlock(
  context: AffineSlashMenuContext,
  properties: CogntPropertyRow[]
) {
  const { rootComponent, model } = context;
  const parent = rootComponent.doc.getParent(model);

  if (!parent) {
    throw new Error('Could not find the insertion block.');
  }

  const index = parent.children.indexOf(model);
  rootComponent.doc.captureSync();
  const blockId = rootComponent.doc.addBlock(
    cogntPropertiesFlavour as never,
    {
      title: 'Properties',
      properties: normalizeCogntProperties(properties),
    } as never,
    parent,
    Math.max(0, index + 1)
  );

  if (!blockId) {
    throw new Error('BlockSuite rejected the properties block.');
  }

  if (model.text?.length === 0 && model.children.length === 0) {
    rootComponent.doc.deleteBlock(model);
  }

  rootComponent.host.selection.setGroup('note', [
    rootComponent.host.selection.create('block', { blockId }),
  ]);
}
