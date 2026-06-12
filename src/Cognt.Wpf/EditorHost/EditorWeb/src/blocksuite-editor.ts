import { type AffineSlashMenuContext } from '@blocksuite/blocks';
import type {
  EditorCaretState,
  LinkedNoteViewMode,
  LoadMarkdownOptions,
  MarkdownEditorOptions,
  SelectedCardContextBlocks,
  SelectedImageBlock,
} from './editor/contracts';
import { reportDiagnostic } from './host';
import type { EditorBootstrapResult } from './editor/editor-bootstrap-types';
import { createEditorBootstrap } from './editor/editor-bootstrap';
import { EditorSessionState } from './editor/editor-session-state';
import {
  ensureMarkdownEditorRegistration,
  trackMarkdownEditorDoc,
  untrackMarkdownEditorDoc,
} from './editor/markdown-editor-registration';
import { logEditorTiming } from './editor/timing';
import {
  applyCogntEditorTheme,
  type CogntEditorTheme,
} from './editor-theme';
import type { DocumentLinksPayload } from './document-links';
import type { MentionPreviewResult, MentionTarget } from './mentions';
import type { CogntSlashMenuEditor } from './editor/slash-menu';
import { pasteRichHtmlForAutomation as pasteRichHtmlForAutomationInEditor } from './editor/rich-html-paste-automation';
import { readHostImageSourceAsDataUrl } from './editor/rich-html-paste-host-source';

const saveDebounceMs = 500;

ensureMarkdownEditorRegistration();

type BlockSuiteEditorRuntimeCallbacks = {
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  cancelPendingContentChanged(): void;
  isLargeDocument(): boolean;
};

type BlockSuiteEditorRuntime = {
  state: EditorSessionState;
  bootstrap: EditorBootstrapResult;
};

export class MarkdownBlockSuiteEditor {
  private readonly eventAbort = new AbortController();
  private readonly hostElement: HTMLElement;
  private readonly state: EditorSessionState;
  private readonly bootstrap: EditorBootstrapResult;
  private readonly options: MarkdownEditorOptions;

  public constructor(options: MarkdownEditorOptions) {
    this.options = options;
    this.hostElement = options.hostElement;
    const runtime = createBlockSuiteEditorRuntime(
      options,
      this.eventAbort.signal,
      this,
      {
        isInputLocked: () => this.isEditorInputLocked(),
        showLockedInputStatus: () => this.showLockedInputStatus(),
        scheduleContentChanged: (delayMs, refreshMentions) =>
          this.scheduleContentChanged(delayMs, refreshMentions),
        cancelPendingContentChanged: () => this.cancelPendingContentChanged(),
        isLargeDocument: () => this.isLargeDocument(),
      }
    );
    this.state = runtime.state;
    this.bootstrap = runtime.bootstrap;
  }

  public applyTheme(theme: CogntEditorTheme) {
    applyCogntEditorTheme(this.hostElement, theme);
  }

  public async load(options: LoadMarkdownOptions) {
    await this.bootstrap.documentLoad.load(options);
    this.bootstrap.outlineViewport.refresh();
  }

  public async focusEditor() {
    await this.bootstrap.interactions.focusEditor();
  }

  public async exportContentForHost() {
    return await exportBlockSuiteEditorContent(
      this.bootstrap,
      () => this.cancelPendingContentChanged(),
      () => this.isLargeDocument()
    );
  }

  public async pasteRichHtmlForAutomation(html: string, plainText = '') {
    await pasteRichHtmlForAutomationInEditor({
      editor: this.bootstrap.editor,
      getCurrentDoc: () => this.state.doc,
      readNativeClipboardImage: () => this.options.onReadNativeClipboardImage(),
      readImageSourceAsDataUrl: (imageSource, fallbackFileName) =>
        readHostImageSourceAsDataUrl({
          getCurrentPath: () => this.state.path,
          attachImageSource: message => this.options.onAttachImageSource(message),
          readAttachment: message => this.options.onReadAttachment(message),
        }, imageSource, fallbackFileName),
      persistMutation: () => this.persistAutomationMutation(),
    }, html, plainText);
  }

  public async focusBlock(blockId: string) {
    await this.bootstrap.interactions.focusBlock(blockId);
  }

  public getCaretState(): EditorCaretState | null {
    return this.bootstrap.interactions.getCaretState();
  }

  public async restoreCaretState(state?: EditorCaretState | null) {
    await this.bootstrap.interactions.restoreCaretState(state);
  }

  public getSelectedImageBlock(): SelectedImageBlock | null {
    return this.bootstrap.selection.getSelectedImageBlock();
  }

  public getSelectedCardContextBlocks(): SelectedCardContextBlocks | null {
    return this.bootstrap.selection.getSelectedCardContextBlocks();
  }

  public async activateEditorMode() {
    await this.bootstrap.interactions.activateEditorMode();
  }

  public async setReadOnlyMode(readOnly: boolean) {
    await this.bootstrap.interactions.setReadOnlyMode(readOnly);
  }

  private isEditorInputLocked() {
    return this.bootstrap.interactions.isLocked;
  }

  private showLockedInputStatus() {
    this.bootstrap.interactions.showLockedInputStatus();
  }

  public showStatus(message: string) {
    this.bootstrap.runtime.showStatus(message);
  }

  public async handleExtractResult(ok: boolean, createdPath?: string, error?: string) {
    await this.bootstrap.extractWorkflow.handleResult(ok, createdPath, error);
  }

  public setMentionTargets(targets: MentionTarget[]) {
    applyBlockSuiteEditorMentionTargets(
      this.state,
      this.bootstrap,
      targets,
      () => this.scheduleContentChanged()
    );
  }

  public setDocumentLinks(payload: DocumentLinksPayload) {
    applyBlockSuiteEditorDocumentLinks(this.bootstrap, payload);
  }

  public handleMentionPreviewResult(result: MentionPreviewResult) {
    handleBlockSuiteEditorMentionPreviewResult(this.bootstrap, result);
  }

  public handleMentionOpenResult(_ok: boolean, _error?: string) {}

  public dispose() {
    this.eventAbort.abort();
    disposeBlockSuiteEditorRuntime({
      state: this.state,
      bootstrap: this.bootstrap,
    });
  }

  private scheduleContentChanged(delayMs = saveDebounceMs, refreshMentions = true) {
    this.bootstrap.contentSave.scheduleContentChanged(delayMs, refreshMentions);
  }

  private cancelPendingContentChanged() {
    this.bootstrap.docChanges.cancelQueuedWork();
    this.bootstrap.contentSave.cancelAllScheduledSaves();
  }

  private isLargeDocument() {
    return this.bootstrap.contentSave.isLargeDocument();
  }

  public async createClozeFromSelection(
    cardContextId?: string | null,
    slotId?: string | null,
    displayClozeIndex?: number | null
  ) {
    await this.bootstrap.commands.createClozeFromSelection({
      cardContextId,
      slotId,
      displayClozeIndex,
    });
  }

  public async createLinkedNoteFromSlashMenu(
    viewMode: LinkedNoteViewMode,
    context: AffineSlashMenuContext
  ) {
    await this.bootstrap.commands.createLinkedNoteFromSlashMenu(viewMode, context);
  }

  public async insertImageFromSlashMenu(context: AffineSlashMenuContext) {
    await this.bootstrap.commands.insertImageFromSlashMenu(context);
  }

  private async persistAutomationMutation() {
    await this.bootstrap.runtime.flushHost();
    this.bootstrap.mentions.refreshHighlights();
    this.cancelPendingContentChanged();
    this.state.incrementUserEditGeneration();
    this.options.onDocumentChanged();
    this.options.onContentChanged(await this.exportContentForHost());
  }
}

function createBlockSuiteEditorRuntime(
  options: MarkdownEditorOptions,
  signal: AbortSignal,
  registeredEditor: CogntSlashMenuEditor,
  callbacks: BlockSuiteEditorRuntimeCallbacks
): BlockSuiteEditorRuntime {
  const state = new EditorSessionState({
    onTrackDoc: doc => trackMarkdownEditorDoc(doc, registeredEditor),
    onUntrackDoc: doc => untrackMarkdownEditorDoc(doc),
  });
  const bootstrap = createEditorBootstrap({
    options,
    signal,
    getCurrentDoc: () => state.doc,
    trackCurrentDoc: doc => state.trackDoc(doc),
    getCurrentPath: () => state.path,
    setCurrentPath: path => state.setPath(path),
    getCurrentCollectionRoot: () => state.collectionRoot,
    setCurrentCollectionRoot: collectionRoot => state.setCollectionRoot(collectionRoot),
    getLoadGeneration: () => state.currentLoadGeneration,
    nextLoadGeneration: () => state.nextLoadGeneration(),
    isLoading: () => state.isLoading,
    setLoadingDocument: loading => state.setLoading(loading),
    incrementUserEditGeneration: () => state.incrementUserEditGeneration(),
    isInputLocked: () => callbacks.isInputLocked(),
    showLockedInputStatus: () => callbacks.showLockedInputStatus(),
    scheduleContentChanged: (delayMs, refreshMentions) =>
      callbacks.scheduleContentChanged(delayMs, refreshMentions),
    cancelPendingContentChanged: () => callbacks.cancelPendingContentChanged(),
    isLargeDocument: () => callbacks.isLargeDocument(),
  });

  options.hostElement.append(bootstrap.editor);
  bootstrap.linkedDocs.bindOpenEvents();
  applyCogntEditorTheme(options.hostElement, {});

  return {
    state,
    bootstrap,
  };
}

function disposeBlockSuiteEditorRuntime(runtime: BlockSuiteEditorRuntime) {
  runtime.bootstrap.contentSave.cancelAllScheduledSaves();
  runtime.bootstrap.runtime.dispose();
  runtime.bootstrap.docChanges.dispose();
  runtime.bootstrap.linkedDocs.dispose();
  runtime.bootstrap.mentions.dispose();
  runtime.bootstrap.editor.remove();
  runtime.state.dispose();
}

async function exportBlockSuiteEditorContent(
  bootstrap: EditorBootstrapResult,
  cancelPendingContentChanged: () => void,
  isLargeDocument: () => boolean
) {
  cancelPendingContentChanged();
  const startedAt = performance.now();
  const content = await bootstrap.runtime.exportCurrentContent();
  bootstrap.contentSave.setContentLength(content.length);
  bootstrap.contentSave.updateLargeDocumentState();
  if (isLargeDocument()) {
    logEditorTiming('large document content export on demand', startedAt, `${content.length} chars`);
  }

  return content;
}

function applyBlockSuiteEditorMentionTargets(
  state: EditorSessionState,
  bootstrap: EditorBootstrapResult,
  targets: MentionTarget[],
  scheduleContentChanged: () => void
) {
  reportDiagnostic('info', `Received ${targets.length} linked note target(s).`);
  bootstrap.linkedDocs.mergeMentionTargetTitles(targets, true);
  bootstrap.mentions.setTargets(targets);

  if (state.doc) {
    const changed = bootstrap.linkedDocs.refreshCurrentTitlesFromTargets();
    if (changed) {
      void bootstrap.runtime.flushHost().then(() => {
        if (!state.isLoading && state.path) {
          scheduleContentChanged();
        }
      });
    }
  }
}

function applyBlockSuiteEditorDocumentLinks(
  bootstrap: EditorBootstrapResult,
  payload: DocumentLinksPayload
) {
  bootstrap.documentLinks.setLinks(payload);
}

function handleBlockSuiteEditorMentionPreviewResult(
  bootstrap: EditorBootstrapResult,
  result: MentionPreviewResult
) {
  if (bootstrap.linkedDocs.hasPreviewRequest(result.requestId)) {
    void bootstrap.linkedDocs.handlePreviewResult(result);
  }
}
