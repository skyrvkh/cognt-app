import type { Doc } from '@blocksuite/store';
import {
  createLocalImageAttachmentPayload,
  getSafeAttachmentFileName,
} from './image-attachments';
import { readFileAsBase64 } from './image-data';
import type {
  AttachedImageFilePayload,
  AttachmentImagePreviewCallback,
} from './attachment-workflow-types';

export type PreparedAttachmentInput = {
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
  localImagePayload: AttachedImageFilePayload | null;
};

export async function prepareAttachmentInput(
  file: File,
  attachmentDoc: Doc | null,
  onImagePreview?: AttachmentImagePreviewCallback
): Promise<PreparedAttachmentInput> {
  const fallbackName = getSafeAttachmentFileName(file);
  const contentBase64 = await readFileAsBase64(file);
  const localImagePayload = onImagePreview && attachmentDoc
    ? await createLocalImageAttachmentPayload(file, fallbackName, contentBase64)
    : null;

  return {
    fallbackName,
    sourceFile: file,
    contentBase64,
    localImagePayload,
  };
}
