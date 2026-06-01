import {
  isImageAttachmentResult,
} from './image-attachments';
import { AttachmentPreviewGate } from './attachment-preview-gate';
import {
  prepareAttachmentInput,
} from './attachment-workflow-preparation';
import {
  createImageAttachmentPreview,
  finalizeImageAttachmentPreview,
  isAttachmentContextCurrent,
  removeImageAttachmentPreview,
} from './attachment-workflow-preview';
import type {
  AttachedFilePayload,
  AttachmentAttachedCallback,
  AttachmentImagePreviewCallback,
  AttachmentWorkflowOptions,
  PendingImageAttachmentPreview,
} from './attachment-workflow-types';
import { reportDiagnostic } from '../host';

export type { AttachedFilePayload } from './attachment-workflow-types';

export class AttachmentWorkflow {
  private readonly previewGate: AttachmentPreviewGate;

  public constructor(private readonly options: AttachmentWorkflowOptions) {
    this.previewGate = new AttachmentPreviewGate(() =>
      this.options.cancelPendingContentChanged());
  }

  public get hasPendingPreview() {
    return this.previewGate.hasPendingPreview;
  }

  public async attachFiles(
    files: File[],
    onAttached: AttachmentAttachedCallback,
    onImagePreview?: AttachmentImagePreviewCallback
  ) {
    for (const file of files) {
      await this.attachFile(file, onAttached, onImagePreview);
    }
  }

  private async attachFile(
    file: File,
    onAttached: AttachmentAttachedCallback,
    onImagePreview?: AttachmentImagePreviewCallback
  ) {
    let preview: PendingImageAttachmentPreview | null = null;
    let previewSaveGateActive = false;
    let rescheduleAfterPreviewFailure = false;
    const attachmentDoc = this.options.getCurrentDoc();
    const attachmentPath = this.options.getCurrentPath();

    try {
      const prepared = await prepareAttachmentInput(file, attachmentDoc, onImagePreview);

      if (prepared.localImagePayload && onImagePreview && attachmentDoc) {
        this.previewGate.begin();
        previewSaveGateActive = true;
        preview = await createImageAttachmentPreview(
          attachmentDoc,
          prepared.localImagePayload,
          onImagePreview
        );
        if (!preview) {
          this.previewGate.end();
          previewSaveGateActive = false;
        }
      }

      const result = await this.options.attachFile({
        type: 'attachFile',
        sourcePath: attachmentPath,
        fileName: prepared.fallbackName,
        contentBase64: prepared.contentBase64,
      });

      if (!result.ok) {
        throw new Error(result.error ?? 'The file could not be attached.');
      }

      if (!isAttachmentContextCurrent(this.options, attachmentDoc, attachmentPath)) {
        throw new Error('The note changed while the file was being attached.');
      }

      if (preview && isImageAttachmentResult(result)) {
        await finalizeImageAttachmentPreview(preview, result);
        await this.options.persistAttachmentMutation();
      } else {
        if (preview) {
          removeImageAttachmentPreview(preview);
          preview = null;
        }

        await onAttached({
          result,
          fallbackName: prepared.fallbackName,
          sourceFile: prepared.sourceFile,
          contentBase64: prepared.contentBase64,
        });
      }

      this.options.showStatus(`Attached ${result.displayName ?? prepared.fallbackName}.`);
    } catch (error) {
      if (preview) {
        removeImageAttachmentPreview(preview);
        rescheduleAfterPreviewFailure = true;
      }

      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Attachment failed: ${message}`);
      this.options.showStatus(`Attach failed: ${message}`);
    } finally {
      if (previewSaveGateActive) {
        this.previewGate.end();
        if (rescheduleAfterPreviewFailure) {
          this.options.scheduleContentChanged();
        }
      }
    }
  }
}
