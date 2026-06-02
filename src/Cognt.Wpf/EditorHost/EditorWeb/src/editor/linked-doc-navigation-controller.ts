import { RefNodeSlotsProvider } from '@blocksuite/affine-components/rich-text';
import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';
import type { OpenMentionMessage } from '../mention-controller';
import type { CreateLinkedNoteMessage, CreateLinkedNoteResult } from './contracts';
import {
  getLinkedDocReferenceFromModel,
  getLinkedPageReferencePath,
  getLinkedPageReferenceTitle,
  type LinkedPageReference,
} from '../links/linked-notes';
import {
  materializeNativeLinkedDocReference,
  openLinkedDocRelativePath,
} from './linked-doc-navigation-actions';
import { getSelectedLinkedDocModelForOpenIntent } from './linked-doc-navigation-intent';

type LinkedDocNavigationControllerOptions = {
  editor: PageEditor;
  signal: AbortSignal;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getReferencePath(pageId: string | null | undefined, fallback?: string | null): string | null | undefined;
  trackReferencePath(pageId: string | null | undefined, relativePath: string | null | undefined): void;
  createLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  openMention(message: OpenMentionMessage): void;
  exportContent(): Promise<string>;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  notifyContentChanged(content: string): void;
  showStatus(message: string): void;
};

export class LinkedDocNavigationController {
  private openDisposable: { dispose(): void } | null = null;
  private openRetryTimer: number | undefined;

  public constructor(private readonly options: LinkedDocNavigationControllerOptions) {}

  public bindOpenEvents(retryCount = 0) {
    if (this.openDisposable) {
      return;
    }

    const refNodeSlotsProvider = this.tryGetRefNodeSlotsProvider();
    if (!refNodeSlotsProvider) {
      if (retryCount < 60 && !this.options.signal.aborted) {
        window.clearTimeout(this.openRetryTimer);
        this.openRetryTimer = window.setTimeout(
          () => this.bindOpenEvents(retryCount + 1),
          50
        );
      }
      return;
    }

    window.clearTimeout(this.openRetryTimer);
    this.openRetryTimer = undefined;
    this.openDisposable =
      refNodeSlotsProvider.docLinkClicked.on((reference: LinkedPageReference) => {
        void this.openReference(reference);
      });
  }

  private tryGetRefNodeSlotsProvider() {
    try {
      const editor = this.options.editor;
      const editorStd = editor?.std;
      if (!editorStd || typeof editorStd.getOptional !== 'function') {
        return null;
      }

      return editorStd.getOptional(RefNodeSlotsProvider) ?? null;
    } catch {
      return null;
    }
  }

  public dispose() {
    this.openDisposable?.dispose();
    this.openDisposable = null;
    window.clearTimeout(this.openRetryTimer);
  }

  public async openReference(reference: LinkedPageReference) {
    const pageId = reference.pageId;
    const currentPath = this.options.getCurrentPath();
    if (!pageId || !currentPath) {
      return;
    }

    let relativePath = this.options.getReferencePath(
      pageId,
      getLinkedPageReferencePath(reference)
    );

    if (!relativePath) {
      try {
        relativePath = await materializeNativeLinkedDocReference(this.options, reference);
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        reportDiagnostic('error', `Could not create New Doc: ${message}`);
        this.options.showStatus(`New Doc failed: ${message}`);
        return;
      }
    }

    if (!relativePath) {
      return;
    }

    await openLinkedDocRelativePath(
      this.options,
      relativePath,
      getLinkedPageReferenceTitle(reference) ?? relativePath
    );
  }

  public async openFromModel(model: unknown) {
    const reference = getLinkedDocReferenceFromModel(model);
    if (!reference) {
      return;
    }

    if (reference.relativePath) {
      this.options.trackReferencePath(reference.pageId, reference.relativePath);
    }

    await this.openReference(reference);
  }

  public async openDocumentLink(relativePath: string) {
    await openLinkedDocRelativePath(this.options, relativePath, relativePath);
  }

  public getSelectedModelForOpenIntent(event: MouseEvent) {
    return getSelectedLinkedDocModelForOpenIntent(
      this.options.editor,
      this.options.getCurrentDoc(),
      event
    );
  }
}
