import type { Doc } from '@blocksuite/store';
import type {
  AttachFileMessage,
  AttachFileResult,
} from './contracts';

export type AttachedFilePayload = {
  result: AttachFileResult;
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

export type AttachedImageFilePayload = AttachedFilePayload & {
  result: AttachFileResult & { relativePath: string };
};

export type PendingImageAttachmentPreview = {
  doc: Doc;
  blockId: string;
  sourceId: string;
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

export type AttachmentAttachedCallback = (
  file: AttachedFilePayload
) => Promise<void | string | null>;

export type AttachmentImagePreviewCallback = (
  file: AttachedImageFilePayload
) => Promise<void | string | null>;

export type AttachmentWorkflowOptions = {
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  attachFile(message: AttachFileMessage): Promise<AttachFileResult>;
  cancelPendingContentChanged(): void;
  persistAttachmentMutation(): Promise<void>;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
};
