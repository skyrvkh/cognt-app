import type { Doc } from '@blocksuite/store';
import { cogntReferencesFlavour } from '../blocks/references';
import { reportDiagnostic } from '../host';
import { formatElapsed } from './timing';
import {
  DocumentChangeTracker,
  type BlockUpdatePayload,
} from './document-change-tracker';

const saveDebounceMs = 500;
const largeDocumentStructureChangeDebounceMs = 350;
const largeDocumentBlockUpdateLogThreshold = 1;

type DocumentChangeCoordinatorOptions = {
  isLoading(): boolean;
  getCurrentDoc(): Doc | null;
  isLargeDocument(): boolean;
  ensureReferencesBlock(doc: Doc): boolean;
  clearOutlineCache(): void;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  scheduleDocumentChangedNotification(): void;
  scheduleLinkedDocPreviewRefresh(): void;
  showStatus(message: string): void;
};

export class DocumentChangeCoordinator {
  private readonly tracker = new DocumentChangeTracker();
  private workTimer: number | undefined;
  private blockUpdatedDisposable: { dispose(): void } | null = null;

  public constructor(private readonly options: DocumentChangeCoordinatorOptions) {}

  public bind(doc: Doc) {
    this.blockUpdatedDisposable?.dispose();
    this.blockUpdatedDisposable = doc.slots.blockUpdated.on(payload => {
      this.queue(payload);
    });
  }

  public dispose() {
    this.blockUpdatedDisposable?.dispose();
    this.blockUpdatedDisposable = null;
    this.cancelQueuedWork();
  }

  public cancelQueuedWork() {
    window.clearTimeout(this.workTimer);
    this.workTimer = undefined;
    this.tracker.reset();
  }

  private queue(payload?: BlockUpdatePayload) {
    if (this.shouldRestoreReferencesBlock(payload)) {
      this.restoreReferencesBlockAfterDelete();
    }

    this.tracker.record(payload);
    this.options.scheduleDocumentChangedNotification();

    if (this.workTimer !== undefined) {
      return;
    }

    const delayMs = this.options.isLargeDocument()
      ? largeDocumentStructureChangeDebounceMs
      : 0;

    this.workTimer = window.setTimeout(() => {
      const batch = this.tracker.snapshot();
      const isLargeDocument = this.options.isLargeDocument();

      this.workTimer = undefined;
      this.tracker.reset();

      this.options.clearOutlineCache();
      if (!isLargeDocument) {
        this.options.scheduleContentChanged(saveDebounceMs, batch.shouldRefreshMentions);
      }

      if (batch.shouldRefreshLinks) {
        this.options.scheduleLinkedDocPreviewRefresh();
      }

      if (isLargeDocument && batch.eventCount >= largeDocumentBlockUpdateLogThreshold) {
        reportDiagnostic(
          'info',
          `Processed large document block update batch: ${batch.eventCount} update(s), ${batch.updateSummary}, linkRefresh=${String(batch.shouldRefreshLinks)}, contentExport=deferred, ${formatElapsed(batch.startedAt)}.`
        );
      }
    }, delayMs);
  }

  private shouldRestoreReferencesBlock(payload?: BlockUpdatePayload) {
    return payload?.type === 'delete'
      && payload.flavour?.toLowerCase() === cogntReferencesFlavour;
  }

  private restoreReferencesBlockAfterDelete() {
    window.queueMicrotask(() => {
      const currentDoc = this.options.getCurrentDoc();
      if (this.options.isLoading() || !currentDoc) {
        return;
      }

      if (!this.options.ensureReferencesBlock(currentDoc)) {
        return;
      }

      this.options.scheduleContentChanged(0, false);
      this.options.showStatus('References are managed by Cognt and cannot be deleted.');
    });
  }
}
