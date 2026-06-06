import { reportDiagnostic } from '../host';
import { DeferredTask } from './deferred-task';
import { logEditorTiming } from './timing';

const saveDebounceMs = 500;
const largeDocumentCharacterThreshold = 200_000;
const largeDocumentSaveDebounceMs = 1600;
const largeDocumentDirtyNotifyDebounceMs = 900;
const largeDocumentDirtyNotifyIdleTimeoutMs = 2500;
const contentSaveIdleTimeoutMs = 5000;
const postExtractSaveDelayMs = 1500;

type ContentSaveCoordinatorOptions = {
  hostElement: HTMLElement;
  isLoading(): boolean;
  isInputLocked(): boolean;
  hasCurrentDoc(): boolean;
  hasCurrentPath(): boolean;
  hasPendingAttachmentPreview(): boolean;
  refreshMentionHighlights(): void;
  exportContent(): Promise<string>;
  onContentChanged(content: string): void;
  onDocumentChanged(): void;
};

export class ContentSaveCoordinator {
  private lastKnownContentLength = 0;
  private readonly contentSaveTask = new DeferredTask();
  private readonly dirtyNotifyTask = new DeferredTask();
  private readonly postExtractSaveTask = new DeferredTask();

  public constructor(private readonly options: ContentSaveCoordinatorOptions) {}

  public setContentLength(length: number) {
    this.lastKnownContentLength = Math.max(0, length);
  }

  public get contentLength() {
    return this.lastKnownContentLength;
  }

  public isLargeDocument() {
    return this.lastKnownContentLength >= largeDocumentCharacterThreshold;
  }

  public updateLargeDocumentState() {
    this.options.hostElement.dataset.cogntLargeDocument = String(this.isLargeDocument());
  }

  public scheduleContentChanged(delayMs = saveDebounceMs, refreshMentions = true) {
    if (
      this.options.isLoading()
      || this.options.isInputLocked()
      || !this.options.hasCurrentDoc()
      || this.options.hasPendingAttachmentPreview()
    ) {
      return;
    }

    this.contentSaveTask.cancel();
    const effectiveDelayMs = delayMs <= 0
      ? delayMs
      : Math.max(delayMs, this.getContentSaveDebounceMs());
    const isLargeDocumentSave = this.isLargeDocument();
    if (isLargeDocumentSave) {
      reportDiagnostic(
        'info',
        `Queued large document content export after ${effectiveDelayMs}ms idle debounce (${this.lastKnownContentLength} chars).`
      );
    }

    this.contentSaveTask.schedule(
      effectiveDelayMs,
      async () => {
        const startedAt = performance.now();
        try {
          if (refreshMentions) {
            this.options.refreshMentionHighlights();
          }

          const content = await this.options.exportContent();
          this.setContentLength(content.length);
          if (isLargeDocumentSave) {
            logEditorTiming('large document content export', startedAt, `${content.length} chars`);
          }

          this.options.onContentChanged(content);
        } catch (error) {
          const message = error instanceof Error ? error.message : String(error);
          reportDiagnostic('error', `Could not export editor content after edit: ${message}`);
        }
      },
      {
        useIdle: isLargeDocumentSave,
        timeout: contentSaveIdleTimeoutMs,
      }
    );
  }

  public scheduleDocumentChangedNotification() {
    if (this.options.isLoading() || !this.options.hasCurrentDoc()) {
      return;
    }

    if (this.dirtyNotifyTask.isScheduled) {
      return;
    }

    const isLargeDocument = this.isLargeDocument();
    const delayMs = isLargeDocument
      ? largeDocumentDirtyNotifyDebounceMs
      : 0;

    this.dirtyNotifyTask.schedule(
      delayMs,
      () => {
        if (!this.options.isLoading() && this.options.hasCurrentDoc()) {
          this.options.onDocumentChanged();
        }
      },
      {
        useIdle: isLargeDocument,
        timeout: largeDocumentDirtyNotifyIdleTimeoutMs,
      }
    );
  }

  public schedulePostExtractSourceSave() {
    this.postExtractSaveTask.schedule(
      postExtractSaveDelayMs,
      () => {
        if (this.options.isLoading() || !this.options.hasCurrentDoc() || !this.options.hasCurrentPath()) {
          return;
        }

        this.scheduleContentChanged(0, false);
        reportDiagnostic('info', 'Queued source note save after extract replacement.');
      },
      {
        useIdle: true,
        timeout: 5000,
      }
    );
  }

  public cancelAllScheduledSaves() {
    this.contentSaveTask.cancel();
    this.dirtyNotifyTask.cancel();
    this.postExtractSaveTask.cancel();
  }

  public cancelDocumentChangedNotification() {
    this.dirtyNotifyTask.cancel();
  }

  private getContentSaveDebounceMs() {
    return this.isLargeDocument()
      ? largeDocumentSaveDebounceMs
      : saveDebounceMs;
  }
}
