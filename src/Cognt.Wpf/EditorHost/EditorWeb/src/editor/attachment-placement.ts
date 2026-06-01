import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import {
  type ImageAttachmentInsertPayload,
} from './attachment-insertion';
import {
  insertCurrentSelectionImageAttachment,
  insertDroppedImageAttachment,
  insertSlashMenuImageAttachment,
} from './attachment-placement-image';
import {
  insertCurrentSelectionAttachmentReference,
  insertDroppedAttachmentReference,
} from './attachment-placement-reference';
import type { AttachmentPlacementOptions } from './attachment-placement-types';
import type { AttachFileResult } from './contracts';
import {
  isImageAttachmentResult,
} from './image-attachments';

export class AttachmentPlacementController {
  public constructor(private readonly options: AttachmentPlacementOptions) {}

  public async insertDroppedAttachmentReference(
    event: DragEvent,
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    const doc = this.options.getCurrentDoc();
    if (!doc) {
      return null;
    }

    if (isImageAttachmentResult(result)) {
      const payload: ImageAttachmentInsertPayload = { result, fallbackName, sourceFile, contentBase64 };
      const blockId = await insertDroppedImageAttachment(this.options, doc, event, payload);

      if (persist) {
        await this.persistAttachmentMutation();
      }

      return blockId;
    }

    await insertDroppedAttachmentReference(this.options, doc, event, result, fallbackName);

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return null;
  }

  public async insertAttachmentReferenceAtCurrentSelection(
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    const doc = this.options.getCurrentDoc();
    if (!doc) {
      return null;
    }

    if (isImageAttachmentResult(result)) {
      const payload: ImageAttachmentInsertPayload = { result, fallbackName, sourceFile, contentBase64 };
      const blockId = await insertCurrentSelectionImageAttachment(this.options, doc, payload);

      if (persist) {
        await this.persistAttachmentMutation();
      }

      return blockId;
    }

    await insertCurrentSelectionAttachmentReference(this.options, doc, result, fallbackName);

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return null;
  }

  public async insertImageAttachmentFromSlashContext(
    context: AffineSlashMenuContext,
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    const doc = this.options.getCurrentDoc();
    if (!doc) {
      return null;
    }

    if (!isImageAttachmentResult(result)) {
      await this.insertAttachmentReferenceAtCurrentSelection(
        result,
        fallbackName,
        sourceFile,
        contentBase64,
        persist);
      return null;
    }

    const payload: ImageAttachmentInsertPayload = { result, fallbackName, sourceFile, contentBase64 };
    const blockId = await insertSlashMenuImageAttachment(this.options, doc, context, payload);

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return blockId;
  }

  public async persistAttachmentMutation() {
    await this.options.flushHost();
    this.options.refreshMentionHighlights();
    this.options.cancelPendingContentChanged();
    this.options.recordUserEdit();
    await this.options.notifyContentChanged();
  }
}
