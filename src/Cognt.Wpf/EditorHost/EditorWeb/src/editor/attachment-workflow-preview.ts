import type { Doc } from '@blocksuite/store';
import {
  isImageAttachmentResult,
  registerImageAttachmentBlob,
} from './image-attachments';
import { isImageBlockModel } from './image-source-models';
import type {
  AttachedImageFilePayload,
  AttachmentImagePreviewCallback,
  AttachmentWorkflowOptions,
  PendingImageAttachmentPreview,
} from './attachment-workflow-types';
import type { AttachFileResult } from './contracts';

export async function createImageAttachmentPreview(
  doc: Doc,
  payload: AttachedImageFilePayload,
  onImagePreview: AttachmentImagePreviewCallback
): Promise<PendingImageAttachmentPreview | null> {
  const blockId = await onImagePreview(payload);
  if (typeof blockId !== 'string' || blockId.trim().length === 0) {
    return null;
  }

  return {
    doc,
    blockId,
    sourceId: payload.result.relativePath,
    fallbackName: payload.fallbackName,
    sourceFile: payload.sourceFile,
    contentBase64: payload.contentBase64,
  };
}

export function isAttachmentContextCurrent(
  options: Pick<AttachmentWorkflowOptions, 'getCurrentDoc' | 'getCurrentPath'>,
  doc: Doc | null,
  path: string
) {
  return Boolean(doc)
    && options.getCurrentDoc() === doc
    && options.getCurrentPath() === path;
}

export async function finalizeImageAttachmentPreview(
  preview: PendingImageAttachmentPreview,
  result: AttachFileResult
) {
  if (!isImageAttachmentResult(result)) {
    return;
  }

  const finalSourceId = await registerImageAttachmentBlob(
    preview.doc,
    result,
    preview.fallbackName,
    preview.sourceFile,
    preview.contentBase64
  );

  if (finalSourceId === preview.sourceId) {
    return;
  }

  const model = preview.doc.getBlock(preview.blockId)?.model;
  if (!isImageBlockModel(model)) {
    return;
  }

  preview.doc.updateBlock(model, {
    sourceId: finalSourceId,
    size: preview.sourceFile.size,
  } as never);
}

export function removeImageAttachmentPreview(preview: PendingImageAttachmentPreview) {
  const model = preview.doc.getBlock(preview.blockId)?.model;
  if (model) {
    preview.doc.deleteBlock(model as never, { deleteChildren: true });
  }
}
