import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { RequestMentionPreviewMessage } from '../mention-controller';
import type { MentionPreviewResult, MentionTarget } from '../mentions';
import {
  isCogntLinkedDocModel,
  type CogntLinkedDoc,
} from '../links/linked-notes';
import { DeferredTask } from './deferred-task';
import { refreshSerializedLinkedDocBlocks } from './linked-doc-preview-refresh';
import { applyLinkedDocPreviewResult } from './linked-doc-preview-result';
import { LinkedDocPreviewState } from './linked-doc-preview-state';

const linkedDocPreviewRefreshDebounceMs = 450;
const largeDocumentLinkedDocPreviewRefreshDebounceMs = 1800;
const linkedDocPreviewIdleTimeoutMs = 5000;

type LinkedDocPreviewControllerOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  isLoading(): boolean;
  isLargeDocument(): boolean;
  requestMentionPreview(message: RequestMentionPreviewMessage): void;
  flushHost(): Promise<void>;
  scheduleDocumentChangedNotification(): void;
  scheduleContentChanged(): void;
};

export class LinkedDocPreviewController {
  private readonly state = new LinkedDocPreviewState();
  private readonly previewRefreshTask = new DeferredTask();

  public constructor(private readonly options: LinkedDocPreviewControllerOptions) {}

  public dispose() {
    this.clear();
  }

  public clear() {
    this.state.clear();
    this.previewRefreshTask.cancel();
  }

  public mergeMentionTargetTitles(targets: MentionTarget[], replace: boolean) {
    this.state.mergeMentionTargetTitles(targets, replace);
  }

  public resolveTitle(relativePath: string) {
    return this.state.resolveTitle(relativePath);
  }

  public refreshCurrentTitlesFromTargets() {
    const currentDoc = this.options.getCurrentDoc();
    if (!currentDoc) {
      return false;
    }

    return refreshSerializedLinkedDocBlocks(
      currentDoc,
      relativePath => this.resolveTitle(relativePath),
      linkedDoc => this.trackDoc(linkedDoc.doc, linkedDoc.relativePath)
    );
  }

  public register(linkedDoc: CogntLinkedDoc, generation: number) {
    this.trackDoc(linkedDoc.doc, linkedDoc.relativePath);
    if (this.state.hasPreviewed(linkedDoc.doc)) {
      return;
    }

    this.markPreviewed(linkedDoc.doc);
    this.requestPreview(linkedDoc.doc, linkedDoc.relativePath, generation);
  }

  public trackDoc(doc: Doc, relativePath: string) {
    this.state.trackDoc(doc, relativePath);
  }

  public trackPath(pageId: string | null | undefined, relativePath: string | null | undefined) {
    this.state.trackPath(pageId, relativePath);
  }

  public getReferencePath(pageId: string | null | undefined, fallback?: string | null) {
    return this.state.getReferencePath(pageId, fallback);
  }

  public markPreviewed(doc: Doc) {
    this.state.markPreviewed(doc);
  }

  public requestPreview(doc: Doc, relativePath: string, generation: number) {
    const sourcePath = this.options.getCurrentPath();
    if (!sourcePath) {
      return;
    }

    const message = this.state.createPreviewRequest(
      doc,
      relativePath,
      generation,
      sourcePath
    );
    if (message) {
      this.options.requestMentionPreview(message);
    }
  }

  public hasPreviewRequest(requestId: string) {
    return this.state.hasPreviewRequest(requestId);
  }

  public async handlePreviewResult(result: MentionPreviewResult) {
    const pending = this.state.takePreviewRequest(result.requestId);

    if (!pending || pending.generation !== this.options.getLoadGeneration()) {
      return;
    }

    await applyLinkedDocPreviewResult({
      pending,
      result,
      registerLinkedDoc: (linkedDoc, generation) => this.register(linkedDoc, generation),
      getLoadGeneration: () => this.options.getLoadGeneration(),
      hasCurrentDoc: () => Boolean(this.options.getCurrentDoc()),
      isLoading: () => this.options.isLoading(),
      flushHost: () => this.options.flushHost(),
    });
  }

  public scheduleRefresh() {
    const delayMs = this.options.isLargeDocument()
      ? largeDocumentLinkedDocPreviewRefreshDebounceMs
      : linkedDocPreviewRefreshDebounceMs;

    this.previewRefreshTask.schedule(
      delayMs,
      () => this.refreshPreviews(),
      {
        useIdle: true,
        timeout: linkedDocPreviewIdleTimeoutMs,
      }
    );
  }

  public hasEmbeddedBlocks() {
    return this.options.getCurrentDoc()
      ?.getBlocks()
      .some(block => isCogntLinkedDocModel((block as { model?: unknown }).model ?? block)) ?? false;
  }

  private refreshPreviews() {
    const currentDoc = this.options.getCurrentDoc();
    if (!currentDoc || !this.options.getCurrentPath() || this.options.isLoading()) {
      return;
    }

    const changed = refreshSerializedLinkedDocBlocks(
      currentDoc,
      relativePath => this.resolveTitle(relativePath),
      linkedDoc => this.register(linkedDoc, this.options.getLoadGeneration())
    );

    if (changed) {
      if (this.options.isLargeDocument()) {
        this.options.scheduleDocumentChangedNotification();
      } else {
        this.options.scheduleContentChanged();
      }
    }
  }
}
