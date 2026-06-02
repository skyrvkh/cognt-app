import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type {
  OpenMentionMessage,
  RequestMentionPreviewMessage,
} from '../mention-controller';
import type { MentionPreviewResult, MentionTarget } from '../mentions';
import type {
  CreateLinkedNoteMessage,
  CreateLinkedNoteResult,
} from './contracts';
import { LinkedDocNavigationController } from './linked-doc-navigation-controller';
import { LinkedDocPreviewController } from './linked-doc-preview-controller';
import {
  type CogntLinkedDoc,
  type LinkedPageReference,
} from '../links/linked-notes';

type LinkedDocWorkflowOptions = {
  editor: PageEditor;
  signal: AbortSignal;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  isLoading(): boolean;
  isLargeDocument(): boolean;
  createLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  requestMentionPreview(message: RequestMentionPreviewMessage): void;
  openMention(message: OpenMentionMessage): void;
  exportContent(): Promise<string>;
  flushHost(): Promise<void>;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  notifyContentChanged(content: string): void;
  scheduleDocumentChangedNotification(): void;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
};

export class LinkedDocWorkflow {
  private readonly navigation: LinkedDocNavigationController;
  private readonly previews: LinkedDocPreviewController;

  public constructor(options: LinkedDocWorkflowOptions) {
    this.previews = new LinkedDocPreviewController({
      editor: options.editor,
      getCurrentDoc: () => options.getCurrentDoc(),
      getCurrentPath: () => options.getCurrentPath(),
      getLoadGeneration: () => options.getLoadGeneration(),
      isLoading: () => options.isLoading(),
      isLargeDocument: () => options.isLargeDocument(),
      requestMentionPreview: message => options.requestMentionPreview(message),
      flushHost: () => options.flushHost(),
      scheduleDocumentChangedNotification: () => options.scheduleDocumentChangedNotification(),
      scheduleContentChanged: () => options.scheduleContentChanged(),
    });
    this.navigation = new LinkedDocNavigationController({
      editor: options.editor,
      signal: options.signal,
      getCurrentDoc: () => options.getCurrentDoc(),
      getCurrentPath: () => options.getCurrentPath(),
      getReferencePath: (pageId, fallback) => this.previews.getReferencePath(pageId, fallback),
      trackReferencePath: (pageId, relativePath) => this.previews.trackPath(pageId, relativePath),
      createLinkedNote: message => options.createLinkedNote(message),
      openMention: message => options.openMention(message),
      exportContent: () => options.exportContent(),
      cancelPendingContentChanged: () => options.cancelPendingContentChanged(),
      recordUserEdit: () => options.recordUserEdit(),
      notifyContentChanged: content => options.notifyContentChanged(content),
      showStatus: message => options.showStatus(message),
    });
  }

  public bindOpenEvents(retryCount = 0) {
    this.navigation.bindOpenEvents(retryCount);
  }

  public dispose() {
    this.navigation.dispose();
    this.previews.dispose();
  }

  public clear() {
    this.previews.clear();
  }

  public mergeMentionTargetTitles(targets: MentionTarget[], replace: boolean) {
    this.previews.mergeMentionTargetTitles(targets, replace);
  }

  public resolveTitle(relativePath: string) {
    return this.previews.resolveTitle(relativePath);
  }

  public refreshCurrentTitlesFromTargets() {
    return this.previews.refreshCurrentTitlesFromTargets();
  }

  public register(linkedDoc: CogntLinkedDoc, generation: number) {
    this.previews.register(linkedDoc, generation);
  }

  public trackDoc(doc: Doc, relativePath: string) {
    this.previews.trackDoc(doc, relativePath);
  }

  public markPreviewed(doc: Doc) {
    this.previews.markPreviewed(doc);
  }

  public requestPreview(doc: Doc, relativePath: string, generation: number) {
    this.previews.requestPreview(doc, relativePath, generation);
  }

  public hasPreviewRequest(requestId: string) {
    return this.previews.hasPreviewRequest(requestId);
  }

  public async handlePreviewResult(result: MentionPreviewResult) {
    await this.previews.handlePreviewResult(result);
  }

  public async openReference(reference: LinkedPageReference) {
    await this.navigation.openReference(reference);
  }

  public async openFromModel(model: unknown) {
    await this.navigation.openFromModel(model);
  }

  public async openDocumentLink(relativePath: string) {
    await this.navigation.openDocumentLink(relativePath);
  }

  public getSelectedModelForOpenIntent(event: MouseEvent) {
    return this.navigation.getSelectedModelForOpenIntent(event);
  }

  public schedulePreviewRefresh() {
    this.previews.scheduleRefresh();
  }

  public hasEmbeddedBlocks() {
    return this.previews.hasEmbeddedBlocks();
  }
}
