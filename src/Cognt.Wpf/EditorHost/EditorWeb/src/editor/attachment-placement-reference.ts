import type { Doc } from '@blocksuite/store';
import { appendAttachmentReferenceBlock, tryInsertAttachmentReferenceIntoTextModel, tryInsertInlineAttachmentReference } from './attachment-insertion';
import { isTextBlockModel } from './block-models';
import type { AttachFileResult } from './contracts';
import { createAttachmentReferenceText } from './image-attachments';
import type { AttachmentPlacementOptions } from './attachment-placement-types';

export async function insertDroppedAttachmentReference(
  options: AttachmentPlacementOptions,
  doc: Doc,
  event: DragEvent,
  result: AttachFileResult,
  fallbackName: string
) {
  const referenceText = createAttachmentReferenceText(result, fallbackName);
  if (!tryInsertInlineAttachmentReference(options.editor, event, referenceText)) {
    await appendAttachmentReferenceBlock(doc, referenceText);
  }
}

export async function insertCurrentSelectionAttachmentReference(
  options: AttachmentPlacementOptions,
  doc: Doc,
  result: AttachFileResult,
  fallbackName: string
) {
  const referenceText = createAttachmentReferenceText(result, fallbackName);
  if (!tryInsertCurrentSelectionAttachmentReference(options, referenceText)) {
    await appendAttachmentReferenceBlock(doc, referenceText);
  }
}

function tryInsertCurrentSelectionAttachmentReference(
  options: AttachmentPlacementOptions,
  referenceText: string
) {
  const model = options.getCurrentTextSelectionBlockModel();
  if (!model || !isTextBlockModel(model)) {
    return false;
  }

  return tryInsertAttachmentReferenceIntoTextModel(options.editor, model, referenceText);
}
