import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import type { BlockModel, Doc } from '@blocksuite/store';
import type { CogntSlashMenuRootComponent } from '../blocksuite-compat';
import {
  appendImageAttachmentBlock,
  insertImageAttachmentBlock,
  tryInsertImageAttachmentBlockAfterModel,
  type ImageAttachmentInsertPayload,
} from './attachment-insertion';
import {
  getEventBlockModel,
  isEmptyTextBlockModel,
} from './block-models';
import type { AttachmentPlacementOptions } from './attachment-placement-types';

export async function insertDroppedImageAttachment(
  options: AttachmentPlacementOptions,
  doc: Doc,
  event: DragEvent,
  payload: ImageAttachmentInsertPayload
) {
  return await insertImageAttachmentNearModel(
    options,
    doc,
    payload,
    getEventBlockModel(event)
  );
}

export async function insertCurrentSelectionImageAttachment(
  options: AttachmentPlacementOptions,
  doc: Doc,
  payload: ImageAttachmentInsertPayload
) {
  return await insertImageAttachmentNearModel(
    options,
    doc,
    payload,
    options.getCurrentInsertionBlockModel()
  );
}

export async function insertSlashMenuImageAttachment(
  options: AttachmentPlacementOptions,
  doc: Doc,
  context: AffineSlashMenuContext,
  payload: ImageAttachmentInsertPayload
) {
  const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
  const { model } = context;
  const parent = rootComponent.doc.getParent(model);
  if (!parent) {
    return await appendImageAttachmentBlock(
      doc,
      options.editor,
      payload,
      () => isCurrentDoc(options, doc)
    );
  }

  const index = parent.children.indexOf(model);
  const blockId = await insertImageAttachmentBlock(
    doc,
    options.editor,
    parent,
    Math.max(0, index + 1),
    payload,
    () => isCurrentDoc(options, doc)
  );
  if (isEmptyTextBlockModel(model)) {
    rootComponent.doc.deleteBlock(model);
  }

  return blockId;
}

async function insertImageAttachmentNearModel(
  options: AttachmentPlacementOptions,
  doc: Doc,
  payload: ImageAttachmentInsertPayload,
  model: BlockModel | null
) {
  let blockId = model
    ? await tryInsertImageAttachmentBlockAfterModel(
        doc,
        options.editor,
        model,
        payload,
        () => isCurrentDoc(options, doc))
    : null;
  if (!blockId) {
    blockId = await appendImageAttachmentBlock(
      doc,
      options.editor,
      payload,
      () => isCurrentDoc(options, doc));
  }

  return blockId;
}

function isCurrentDoc(options: AttachmentPlacementOptions, doc: Doc) {
  return options.getCurrentDoc() === doc;
}
