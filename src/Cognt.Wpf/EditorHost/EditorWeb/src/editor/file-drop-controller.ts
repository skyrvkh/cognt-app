import type { Doc } from '@blocksuite/store';
import type { AttachmentPlacementController } from './attachment-placement';
import type { AttachmentWorkflow } from './attachment-workflow';

type FileDropControllerOptions = {
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  showStatus(message: string): void;
  attachmentPlacement: AttachmentPlacementController;
  attachmentWorkflow: AttachmentWorkflow;
};

export class FileDropController {
  public constructor(private readonly options: FileDropControllerOptions) {}

  public hasDroppedFiles(event: DragEvent) {
    const dataTransfer = event.dataTransfer;
    if (!dataTransfer) {
      return false;
    }

    return Array.from(dataTransfer.types ?? []).includes('Files')
      || dataTransfer.files.length > 0;
  }

  public handleFileDragOver(event: DragEvent) {
    if (!this.hasDroppedFiles(event)) {
      return;
    }

    this.cancelNativeFileEvent(event);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect =
        this.options.getCurrentPath() && !this.options.isInputLocked()
          ? 'copy'
          : 'none';
    }
  }

  public async handleDroppedFiles(event: DragEvent) {
    this.cancelNativeFileEvent(event);

    if (this.options.isInputLocked()) {
      this.options.showLockedInputStatus();
      return;
    }

    if (!this.options.getCurrentDoc() || !this.options.getCurrentPath()) {
      this.options.showStatus('Open a note before attaching files.');
      return;
    }

    const files = Array.from(event.dataTransfer?.files ?? [])
      .filter(file => file.size > 0);
    if (files.length === 0) {
      return;
    }

    await this.options.attachmentWorkflow.attachFiles(
      files,
      file =>
        this.options.attachmentPlacement.insertDroppedAttachmentReference(
          event,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64),
      file =>
        this.options.attachmentPlacement.insertDroppedAttachmentReference(
          event,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64,
          false));
  }

  public cancelNativeFileEvent(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    if (typeof event.stopImmediatePropagation === 'function') {
      event.stopImmediatePropagation();
    }
  }
}
