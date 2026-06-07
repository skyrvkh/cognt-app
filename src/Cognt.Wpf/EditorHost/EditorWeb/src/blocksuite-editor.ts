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

  public constructor(options: MarkdownEditorOptions) {
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

/*
 * Superseded monolithic entrypoint retained temporarily while its
 * responsibilities are removed in independently buildable milestones.
 *
  private async attachFiles(
    files: File[],
    onAttached: (file: AttachedFilePayload) => Promise<void | string | null>,
    onImagePreview?: (file: AttachedFilePayload & {
      result: AttachFileResult & { relativePath: string };
    }) => Promise<void | string | null>
  ) {
    for (const file of files) {
      let preview: PendingImageAttachmentPreview | null = null;
      let previewSaveGateActive = false;
      let rescheduleAfterPreviewFailure = false;
      const previewHandler = onImagePreview;
      const attachmentDoc = this.currentDoc;
      const attachmentPath = this.currentPath;

      try {
        const fallbackName = getSafeAttachmentFileName(file);
        const contentBase64 = await readFileAsBase64(file);
        const localImagePayload = previewHandler && attachmentDoc
          ? await createLocalImageAttachmentPayload(file, fallbackName, contentBase64)
          : null;

        if (localImagePayload && previewHandler && attachmentDoc) {
          this.beginAttachmentPreviewSaveGate();
          previewSaveGateActive = true;
          const blockId = await previewHandler(localImagePayload);
          if (typeof blockId === 'string' && blockId.trim().length > 0) {
            preview = {
              doc: attachmentDoc,
              blockId,
              sourceId: localImagePayload.result.relativePath,
              fallbackName,
              sourceFile: file,
              contentBase64,
            };
          } else {
            this.endAttachmentPreviewSaveGate();
            previewSaveGateActive = false;
          }
        }

        const result = await this.onAttachFile({
          type: 'attachFile',
          sourcePath: attachmentPath,
          fileName: fallbackName,
          contentBase64,
        });

        if (!result.ok) {
          throw new Error(result.error ?? 'The file could not be attached.');
        }

        if (!this.isAttachmentContextCurrent(attachmentDoc, attachmentPath)) {
          throw new Error('The note changed while the file was being attached.');
        }

        if (preview && isImageAttachmentResult(result)) {
          await this.finalizeImageAttachmentPreview(preview, result);
          await this.persistAttachmentMutation();
        } else {
          if (preview) {
            this.removeImageAttachmentPreview(preview);
            preview = null;
          }

          await onAttached({ result, fallbackName, sourceFile: file, contentBase64 });
        }

        this.showStatus(`Attached ${result.displayName ?? fallbackName}.`);
      } catch (error) {
        if (preview) {
          this.removeImageAttachmentPreview(preview);
          rescheduleAfterPreviewFailure = true;
        }

        const message = error instanceof Error ? error.message : String(error);
        reportDiagnostic('error', `Attachment failed: ${message}`);
        this.showStatus(`Attach failed: ${message}`);
      } finally {
        if (previewSaveGateActive) {
          this.endAttachmentPreviewSaveGate();
          if (rescheduleAfterPreviewFailure) {
            this.scheduleContentChanged();
          }
        }
      }
    }
  }

  private beginAttachmentPreviewSaveGate() {
    this.pendingAttachmentPreviews++;
    this.cancelPendingContentChanged();
  }

  private endAttachmentPreviewSaveGate() {
    this.pendingAttachmentPreviews = Math.max(0, this.pendingAttachmentPreviews - 1);
  }

  private isAttachmentContextCurrent(doc: Doc | null, path: string) {
    return Boolean(doc)
      && this.currentDoc === doc
      && this.currentPath === path;
  }

  private async finalizeImageAttachmentPreview(
    preview: PendingImageAttachmentPreview,
    result: AttachFileResult & { relativePath: string }
  ) {
    const finalSourceId = await registerImageAttachmentBlob(
      preview.doc,
      result,
      preview.fallbackName,
      preview.sourceFile,
      preview.contentBase64);

    if (finalSourceId === preview.sourceId) {
      return;
    }

    const model = preview.doc.getBlock(preview.blockId)?.model;
    if (!isImageBlockModel(model)) {
      return;
    }

    preview.doc.updateBlock(model, {
      sourceId: finalSourceId,
      size: preview.sourceFile.size,
    } as never);
  }

  private removeImageAttachmentPreview(preview: PendingImageAttachmentPreview) {
    const model = preview.doc.getBlock(preview.blockId)?.model;
    if (model) {
      preview.doc.deleteBlock(model as never, { deleteChildren: true });
    }
  }

  private async insertDroppedAttachmentReference(
    event: DragEvent,
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    if (!this.currentDoc) {
      return null;
    }

    if (isImageAttachmentResult(result)) {
      const model = getEventBlockModel(event);
      let blockId = model
        ? await this.tryInsertImageAttachmentBlockAfterModel(
            model,
            result,
            fallbackName,
            sourceFile,
            contentBase64)
        : null;
      if (!blockId) {
        blockId = await this.appendImageAttachmentBlock(result, fallbackName, sourceFile, contentBase64);
      }

      if (persist) {
        await this.persistAttachmentMutation();
      }

      return blockId;
    }

    const referenceText = createAttachmentReferenceText(result, fallbackName);
    if (!this.tryInsertInlineAttachmentReference(event, referenceText)) {
      await this.appendAttachmentReferenceBlock(referenceText);
    }

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return null;
  }

  private tryInsertInlineAttachmentReference(event: DragEvent, referenceText: string) {
    const model = getEventBlockModel(event);
    if (!model) {
      return false;
    }

    const host = this.editor.host;
    const inlineEditor = host ? getInlineEditorByModel(host, model) : null;
    const range = inlineEditor?.getInlineRange();
    if (inlineEditor && range) {
      inlineEditor.insertText(range, referenceText);
      inlineEditor.setInlineRange({
        index: range.index + referenceText.length,
        length: 0,
      });
      return true;
    }

    const text = (model as {
      text?: {
        length?: number;
        replace(index: number, length: number, content: string): void;
      };
    }).text;
    if (text && typeof text.replace === 'function') {
      text.replace(text.length ?? 0, 0, referenceText);
      return true;
    }

    return false;
  }

  private async insertAttachmentReferenceAtCurrentSelection(
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    if (!this.currentDoc) {
      return null;
    }

    if (isImageAttachmentResult(result)) {
      const model = this.getSelectedBlockModel();
      let blockId = model
        ? await this.tryInsertImageAttachmentBlockAfterModel(
            model,
            result,
            fallbackName,
            sourceFile,
            contentBase64)
        : null;
      if (!blockId) {
        blockId = await this.appendImageAttachmentBlock(result, fallbackName, sourceFile, contentBase64);
      }

      if (persist) {
        await this.persistAttachmentMutation();
      }

      return blockId;
    }

    const referenceText = createAttachmentReferenceText(result, fallbackName);
    if (!this.tryInsertCurrentSelectionAttachmentReference(referenceText)) {
      await this.appendAttachmentReferenceBlock(referenceText);
    }

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return null;
  }

  private async insertImageAttachmentFromSlashContext(
    context: AffineSlashMenuContext,
    result: AttachFileResult,
    fallbackName: string,
    sourceFile: File,
    contentBase64: string,
    persist = true
  ) {
    if (!this.currentDoc) {
      return null;
    }

    if (!isImageAttachmentResult(result)) {
      await this.insertAttachmentReferenceAtCurrentSelection(
        result,
        fallbackName,
        sourceFile,
        contentBase64,
        persist);
      return null;
    }

    const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
    const { model } = context;
    const parent = rootComponent.doc.getParent(model);
    if (!parent) {
      const blockId = await this.appendImageAttachmentBlock(result, fallbackName, sourceFile, contentBase64);
      if (persist) {
        await this.persistAttachmentMutation();
      }

      return blockId;
    }

    const index = parent.children.indexOf(model);
    const blockId = await this.insertImageAttachmentBlock(
      parent,
      Math.max(0, index + 1),
      result,
      fallbackName,
      sourceFile,
      contentBase64);
    if (isEmptyTextBlockModel(model)) {
      rootComponent.doc.deleteBlock(model);
    }

    if (persist) {
      await this.persistAttachmentMutation();
    }

    return blockId;
  }

  private async tryInsertImageAttachmentBlockAfterModel(
    model: BlockModel,
    result: AttachFileResult & { relativePath: string },
    fallbackName: string,
    sourceFile: File,
    contentBase64: string
  ) {
    if (!this.currentDoc) {
      return null;
    }

    const parent = this.currentDoc.getParent(model);
    if (!parent) {
      return null;
    }

    const index = parent.children.indexOf(model);
    return await this.insertImageAttachmentBlock(
      parent,
      Math.max(0, index + 1),
      result,
      fallbackName,
      sourceFile,
      contentBase64);
  }

  private async appendImageAttachmentBlock(
    result: AttachFileResult & { relativePath: string },
    fallbackName: string,
    sourceFile: File,
    contentBase64: string
  ) {
    const note = this.currentDoc?.root?.children.find(child => child.flavour === 'affine:note') ?? null;
    if (!note) {
      return null;
    }

    return await this.insertImageAttachmentBlock(
      note,
      note.children.length,
      result,
      fallbackName,
      sourceFile,
      contentBase64);
  }

  private async insertImageAttachmentBlock(
    parent: BlockModel,
    index: number,
    result: AttachFileResult & { relativePath: string },
    fallbackName: string,
    sourceFile: File,
    contentBase64: string
  ) {
    const doc = this.currentDoc;
    if (!doc) {
      return null;
    }

    const relativePath = await registerImageAttachmentBlob(
      doc,
      result,
      fallbackName,
      sourceFile,
      contentBase64);

    doc.captureSync();
    const blockId = doc.addBlock(
      'affine:image' as never,
      {
        sourceId: relativePath,
        size: sourceFile.size,
      } as never,
      parent,
      index
    );

    if (blockId && this.currentDoc === doc) {
      this.editor.host?.selection.setGroup('note', [
        this.editor.host.selection.create('block', { blockId }),
      ]);
    }

    return blockId;
  }

  private getSelectedBlockModel() {
    const blockSelection = this.editor.host?.selection.find('block') as
      | { blockId?: string | null }
      | null
      | undefined;
    const blockId = typeof blockSelection?.blockId === 'string'
      ? blockSelection.blockId
      : null;

    return blockId && this.currentDoc
      ? this.currentDoc.getBlock(blockId)?.model ?? null
      : null;
  }

  private getSelectedCardContextBlockIds() {
    const textSelection = this.getTextSelection();
    const ids = textSelection && !textSelection.isCollapsed()
      ? this.getTextSelectionBlockIds(textSelection)
      : [];

    return uniqueBlockIds(ids.length > 0 ? ids : this.getBlockSelectionBlockIds());
  }

  private getTextSelectionBlockIds(textSelection: TextSelection) {
    const range = this.editor.std?.range.textSelectionToRange(textSelection as never);
    const components = (range
      ? this.editor.std.range.getSelectedBlockComponentsByRange(range, {
          match: (element: { model?: { id?: string; role?: string } }) =>
            Boolean(element.model?.id) && element.model?.role === 'content',
          mode: 'flat',
        })
      : []) as Array<{ model?: { id?: string } }>;

    const componentIds = components
      .map((component: { model?: { id?: string } }) => component.model?.id)
      .filter((blockId: string | undefined): blockId is string => Boolean(blockId));
    if (componentIds.length > 0) {
      return componentIds;
    }

    return [
      textSelection.start?.blockId,
      textSelection.from?.blockId,
      textSelection.end?.blockId,
      textSelection.to?.blockId,
    ].filter((blockId: string | undefined): blockId is string => Boolean(blockId));
  }

  private getBlockSelectionBlockIds() {
    const selection = this.editor.host?.selection as
      | {
          find(type: string): unknown;
          filter?(type: string): unknown[];
        }
      | undefined;
    if (!selection) {
      return [];
    }

    const blockSelections = typeof selection.filter === 'function'
      ? selection.filter('block')
      : [selection.find('block')];

    return blockSelections.flatMap(getBlockSelectionId);
  }

  private getTextSelectionImageCandidateBlockIds() {
    const textSelection = this.getTextSelection();
    return textSelection ? this.getTextSelectionBlockIds(textSelection) : [];
  }

  private getImageBlocksInDocument() {
    if (!this.currentDoc) {
      return [];
    }

    return this.currentDoc
      .getBlocks()
      .map(block => (block as { model?: unknown }).model ?? block)
      .filter(isImageBlockModel);
  }

  private createSelectedImageBlockFromModel(value: unknown): SelectedImageBlock | null {
    return createSelectedImageBlock(value, this.currentCollectionRoot);
  }

  private resolveOutlineBlockId(blockId: string) {
    const candidate = blockId.trim();
    if (!candidate || !this.currentDoc) {
      return null;
    }

    if (this.currentDoc.getBlock(candidate)) {
      return candidate;
    }

    const markdownMatch = /^markdown-heading-(\d+)$/i.exec(candidate);
    if (!markdownMatch) {
      return null;
    }

    const headingIndex = Number.parseInt(markdownMatch[1], 10) - 1;
    if (!Number.isInteger(headingIndex) || headingIndex < 0) {
      return null;
    }

    return this.getOutlineHeadingBlockIds()[headingIndex] ?? null;
  }

  private getRenderedBlockElement(blockId: string) {
    return (this.editor.std?.view?.getBlock(blockId) as HTMLElement | null | undefined) ?? null;
  }

  private getOutlineHeadingBlockIds() {
    if (this.outlineHeadingBlockIds) {
      return this.outlineHeadingBlockIds;
    }

    this.outlineHeadingBlockIds = this.currentDoc
      ?.getBlocks()
      .map(block => (block as { model?: unknown }).model ?? block)
      .filter(isHeadingBlockModel)
      .map(model => model.id) ?? [];
    return this.outlineHeadingBlockIds;
  }

  private createSelectedCardContextTitle(blockIds: string[]) {
    const selectedText = normalizeSelectionText(window.getSelection()?.toString() ?? '');
    if (selectedText.length > 0) {
      return createTitleFromPlainText(selectedText, 'Card context');
    }

    const blockText = blockIds
      .map(blockId => this.currentDoc?.getBlock(blockId)?.model)
      .map(getBlockPlainText)
      .filter(text => text.length > 0)
      .join(' ');

    return createTitleFromPlainText(blockText, 'Card context');
  }

  private async persistAttachmentMutation() {
    await this.flushHost();
    this.mentions.refreshHighlights();
    this.cancelPendingContentChanged();
    this.userEditGeneration++;
    this.onContentChanged(await this.exportCurrentContent());
  }

  private tryInsertCurrentSelectionAttachmentReference(referenceText: string) {
    const blockSelection = this.editor.host?.selection.find('block') as
      | { blockId?: string | null }
      | null
      | undefined;
    const blockId = typeof blockSelection?.blockId === 'string'
      ? blockSelection.blockId
      : null;
    if (!blockId || !this.currentDoc) {
      return false;
    }

    const model = this.currentDoc.getBlock(blockId)?.model ?? null;
    if (!model || !isTextBlockModel(model)) {
      return false;
    }

    const host = this.editor.host;
    const inlineEditor = host ? getInlineEditorByModel(host, model) : null;
    const range = inlineEditor?.getInlineRange();
    if (inlineEditor && range) {
      inlineEditor.insertText(range, referenceText);
      inlineEditor.setInlineRange({
        index: range.index + referenceText.length,
        length: 0,
      });
      return true;
    }

    const text = (model as {
      text?: {
        length?: number;
        replace(index: number, length: number, content: string): void;
      };
    }).text;
    if (text && typeof text.replace === 'function') {
      text.replace(text.length ?? 0, 0, referenceText);
      return true;
    }

    return false;
  }

  private async appendAttachmentReferenceBlock(referenceText: string) {
    if (!this.currentDoc?.root) {
      return;
    }

    const note = this.currentDoc.root.children.find(child => child.flavour === 'affine:note') ?? null;
    if (!note) {
      return;
    }

    await MarkdownTransformer.importMarkdownToBlock({
      doc: this.currentDoc,
      markdown: referenceText,
      blockId: note.id,
    });
  }

  private bindDocChangeEvents(doc: Doc) {
    this.blockUpdatedDisposable?.dispose();
    this.blockUpdatedDisposable = doc.slots.blockUpdated.on(payload => {
      this.queueDocumentChangeWork(payload);
    });
  }

  private queueDocumentChangeWork(payload?: BlockUpdatePayload) {
    this.pendingDocumentChangeCount++;
    this.scheduleDocumentChangedNotification();
    this.trackPendingDocumentChange(payload);
    this.pendingDocumentChangeShouldRefreshLinks =
      this.pendingDocumentChangeShouldRefreshLinks
      || this.shouldRefreshLinkedDocsForBlockChange(payload);
    this.pendingDocumentChangeShouldRefreshMentions =
      this.pendingDocumentChangeShouldRefreshMentions
      || this.shouldRefreshMentionsForBlockChange(payload);

    if (this.documentChangeWorkTimer !== undefined) {
      return;
    }

    this.pendingDocumentChangeStartedAt = performance.now();
    const delayMs = this.isLargeDocument()
      ? largeDocumentStructureChangeDebounceMs
      : 0;

    this.documentChangeWorkTimer = window.setTimeout(() => {
      const eventCount = this.pendingDocumentChangeCount;
      const shouldRefreshLinks = this.pendingDocumentChangeShouldRefreshLinks;
      const shouldRefreshMentions = this.pendingDocumentChangeShouldRefreshMentions;
      const startedAt = this.pendingDocumentChangeStartedAt;
      const updateSummary = this.formatPendingDocumentChangeSummary();
      const isLargeDocument = this.isLargeDocument();

      this.documentChangeWorkTimer = undefined;
      this.pendingDocumentChangeCount = 0;
      this.pendingDocumentChangeShouldRefreshLinks = false;
      this.pendingDocumentChangeShouldRefreshMentions = false;
      this.pendingDocumentChangeStartedAt = 0;
      this.pendingDocumentChangeKeys.clear();

      this.outlineHeadingBlockIds = null;
      if (!isLargeDocument) {
        this.scheduleContentChanged(saveDebounceMs, shouldRefreshMentions);
      }

      if (shouldRefreshLinks) {
        this.scheduleLinkedDocPreviewRefresh();
      }

      if (isLargeDocument && eventCount >= largeDocumentBlockUpdateLogThreshold) {
        reportDiagnostic(
          'info',
          `Processed large document block update batch: ${eventCount} update(s), ${updateSummary}, linkRefresh=${String(shouldRefreshLinks)}, contentExport=deferred, ${formatElapsed(startedAt)}.`
        );
      }
    }, delayMs);
  }

  private cancelQueuedDocumentChangeWork() {
    window.clearTimeout(this.documentChangeWorkTimer);
    this.documentChangeWorkTimer = undefined;
    this.pendingDocumentChangeCount = 0;
    this.pendingDocumentChangeShouldRefreshLinks = false;
    this.pendingDocumentChangeShouldRefreshMentions = false;
    this.pendingDocumentChangeStartedAt = 0;
    this.pendingDocumentChangeKeys.clear();
  }

  private scheduleDocumentChangedNotification() {
    if (this.loadingDocument || !this.currentDoc) {
      return;
    }

    if (this.dirtyNotifyTimer !== undefined) {
      return;
    }

    const delayMs = this.isLargeDocument()
      ? largeDocumentDirtyNotifyDebounceMs
      : 0;

    this.dirtyNotifyTimer = window.setTimeout(() => {
      this.dirtyNotifyTimer = undefined;
      const notify = () => {
        this.dirtyNotifyIdleId = undefined;
        if (!this.loadingDocument && this.currentDoc) {
          this.onDocumentChanged();
        }
      };

      if (this.isLargeDocument() && window.requestIdleCallback) {
        this.dirtyNotifyIdleId = window.requestIdleCallback(
          notify,
          { timeout: largeDocumentDirtyNotifyIdleTimeoutMs }
        );
        return;
      }

      notify();
    }, delayMs);
  }

  private cancelDocumentChangedNotification() {
    window.clearTimeout(this.dirtyNotifyTimer);
    this.dirtyNotifyTimer = undefined;

    if (this.dirtyNotifyIdleId !== undefined) {
      window.cancelIdleCallback?.(this.dirtyNotifyIdleId);
      this.dirtyNotifyIdleId = undefined;
    }
  }

  private trackPendingDocumentChange(payload?: BlockUpdatePayload) {
    const type = payload?.type?.toLowerCase() ?? 'unknown';
    const flavour = payload?.flavour?.toLowerCase() ?? 'unknown';
    const key = payload?.props?.key?.trim().toLowerCase() ?? '';
    const label = key ? `${type}:${flavour}:${key}` : `${type}:${flavour}`;
    this.pendingDocumentChangeKeys.set(
      label,
      (this.pendingDocumentChangeKeys.get(label) ?? 0) + 1
    );
  }

  private formatPendingDocumentChangeSummary() {
    const entries = [...this.pendingDocumentChangeKeys.entries()]
      .sort((left, right) => right[1] - left[1])
      .slice(0, 6);

    if (entries.length === 0) {
      return 'updates=none';
    }

    return `updates=${entries.map(([label, count]) => `${label}x${count}`).join(',')}`;
  }

  private shouldRefreshLinkedDocsForBlockChange(payload?: BlockUpdatePayload) {
    if (!payload) {
      return true;
    }

    const flavour = payload.flavour?.toLowerCase() ?? '';
    if (flavour === 'affine:embed-linked-doc') {
      return true;
    }

    if (payload.type !== 'update') {
      return false;
    }

    const key = payload.props?.key?.trim().toLowerCase() ?? '';
    if (!key) {
      return true;
    }

    return key === 'text'
      || key === 'pageid'
      || key === 'params'
      || key === 'title'
      || key === 'description'
      || key.includes('link')
      || key.includes('reference');
  }

  private shouldRefreshMentionsForBlockChange(payload?: BlockUpdatePayload) {
    if (!payload) {
      return true;
    }

    if (payload.type !== 'update') {
      return false;
    }

    const key = payload.props?.key?.trim().toLowerCase() ?? '';
    return !key || key === 'text';
  }

  private getSelectedLinkedDocModelForOpenIntent(event: MouseEvent) {
    if (!isOpenThisDocToolbarIntent(event)) {
      return null;
    }

    const toolbarModel = getCogntLinkedDocToolbarModel(event);
    if (toolbarModel) {
      return toolbarModel;
    }

    const blockSelection = this.editor.host?.selection.find('block') as
      | { blockId?: string }
      | null
      | undefined;
    const model = blockSelection?.blockId
      ? this.currentDoc?.getBlock(blockSelection.blockId)?.model
      : null;

    return isCogntLinkedDocModel(model) && getCogntLinkedNotePathFromModel(model)
      ? model
      : null;
  }

  private scheduleLinkedDocPreviewRefresh() {
    window.clearTimeout(this.linkedDocPreviewRefreshTimer);
    this.cancelLinkedDocPreviewRefreshIdle();

    const delayMs = this.isLargeDocument()
      ? largeDocumentLinkedDocPreviewRefreshDebounceMs
      : linkedDocPreviewRefreshDebounceMs;

    this.linkedDocPreviewRefreshTimer = window.setTimeout(() => {
      this.linkedDocPreviewRefreshTimer = undefined;
      const refresh = () => {
        this.linkedDocPreviewRefreshIdleId = undefined;
        this.refreshLinkedDocPreviews();
      };

      if (window.requestIdleCallback) {
        this.linkedDocPreviewRefreshIdleId = window.requestIdleCallback(
          refresh,
          { timeout: linkedDocPreviewIdleTimeoutMs }
        );
        return;
      }

      refresh();
    }, delayMs);
  }

  private cancelLinkedDocPreviewRefresh() {
    window.clearTimeout(this.linkedDocPreviewRefreshTimer);
    this.linkedDocPreviewRefreshTimer = undefined;
    this.cancelLinkedDocPreviewRefreshIdle();
  }

  private cancelLinkedDocPreviewRefreshIdle() {
    if (this.linkedDocPreviewRefreshIdleId === undefined) {
      return;
    }

    window.cancelIdleCallback?.(this.linkedDocPreviewRefreshIdleId);
    this.linkedDocPreviewRefreshIdleId = undefined;
  }

  private refreshLinkedDocPreviews() {
    if (!this.currentDoc || !this.currentPath || this.loadingDocument) {
      return;
    }

    const resolveTitle = (relativePath: string) => this.resolveLinkedNoteTitle(relativePath);
    const embedChanged = registerSerializedCogntEmbedLinkedNotes(
      this.currentDoc,
      linkedDoc => this.registerLinkedDoc(linkedDoc, this.loadGeneration),
      resolveTitle
    );
    const inlineChanged = registerSerializedCogntInlineLinkedNotes(
      this.currentDoc,
      resolveTitle
    );

    if (embedChanged || inlineChanged) {
      if (this.isLargeDocument()) {
        this.scheduleDocumentChangedNotification();
      } else {
        this.scheduleContentChanged();
      }
    }
  }

  private async flushHost() {
    await flushEditorHost(this.editor, {
      patchLinkedDocBlocks: this.hasEmbeddedLinkedDocBlocks(),
    });
  }

  private hasEmbeddedLinkedDocBlocks() {
    return this.currentDoc
      ?.getBlocks()
      .some(block => isCogntLinkedDocModel((block as { model?: unknown }).model ?? block)) ?? false;
  }

  private async exportCurrentContent() {
    this.documentLinks.ensureReferencesBlock();
    return exportDocContent(this.currentDoc, this.currentPath, this.currentCollectionRoot);
  }

  private scheduleContentChanged(delayMs = saveDebounceMs, refreshMentions = true) {
    if (
      this.loadingDocument
      || this.isEditorInputLocked()
      || !this.currentDoc
      || this.pendingAttachmentPreviews > 0
    ) {
      return;
    }

    window.clearTimeout(this.saveTimer);
    this.cancelPendingContentSaveIdle();
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

    this.saveTimer = window.setTimeout(async () => {
      this.saveTimer = undefined;
      const save = async () => {
        this.saveIdleId = undefined;
        const startedAt = performance.now();
        try {
          if (refreshMentions) {
            this.mentions.refreshHighlights();
          }

          const content = await this.exportCurrentContent();
          this.lastKnownContentLength = content.length;
          if (isLargeDocumentSave) {
            logEditorTiming('large document content export', startedAt, `${content.length} chars`);
          }

          this.onContentChanged(content);
        } catch (error) {
          const message = error instanceof Error ? error.message : String(error);
          reportDiagnostic('error', `Could not export editor content after edit: ${message}`);
        }
      };

      if (isLargeDocumentSave && window.requestIdleCallback) {
        this.saveIdleId = window.requestIdleCallback(
          () => void save(),
          { timeout: contentSaveIdleTimeoutMs }
        );
        return;
      }

      await save();
    }, effectiveDelayMs);
  }

  private cancelPendingContentChanged() {
    this.cancelQueuedDocumentChangeWork();
    window.clearTimeout(this.saveTimer);
    this.cancelDocumentChangedNotification();
    window.clearTimeout(this.postExtractSaveTimer);
    this.cancelPendingContentSaveIdle();
    this.cancelPostExtractIdleSave();
    this.saveTimer = undefined;
    this.postExtractSaveTimer = undefined;
  }

  private cancelPendingContentSaveIdle() {
    if (this.saveIdleId === undefined) {
      return;
    }

    window.cancelIdleCallback?.(this.saveIdleId);
    this.saveIdleId = undefined;
  }

  private isLargeDocument() {
    return this.lastKnownContentLength >= largeDocumentCharacterThreshold;
  }

  private updateLargeDocumentState() {
    this.hostElement.dataset.cogntLargeDocument = String(this.isLargeDocument());
  }

  private getContentSaveDebounceMs() {
    return this.isLargeDocument()
      ? largeDocumentSaveDebounceMs
      : saveDebounceMs;
  }

  private schedulePostExtractSourceSave() {
    window.clearTimeout(this.postExtractSaveTimer);
    this.cancelPostExtractIdleSave();
    this.postExtractSaveTimer = window.setTimeout(() => {
      this.postExtractSaveTimer = undefined;
      const queueSave = () => {
        this.postExtractSaveIdleId = undefined;
        if (this.loadingDocument || !this.currentDoc || !this.currentPath) {
          return;
        }

        this.scheduleContentChanged(0, false);
        reportDiagnostic('info', 'Queued source note save after extract replacement.');
      };

      if (window.requestIdleCallback) {
        this.postExtractSaveIdleId = window.requestIdleCallback(queueSave, { timeout: 5000 });
        return;
      }

      this.postExtractSaveTimer = window.setTimeout(queueSave, 0);
    }, postExtractSaveDelayMs);
  }

  private cancelPostExtractIdleSave() {
    if (this.postExtractSaveIdleId === undefined) {
      return;
    }

    window.cancelIdleCallback?.(this.postExtractSaveIdleId);
    this.postExtractSaveIdleId = undefined;
  }

  private async handleCreateExtractShortcut(event: KeyboardEvent) {
    this.suppressNextNativeCut = true;
    try {
      this.pendingExtract = await runCreateExtractCommand(
        {
          isInputLocked: () => this.isEditorInputLocked(),
          getCurrentDoc: () => this.currentDoc,
          getCurrentPath: () => this.currentPath,
          getLoadGeneration: () => this.loadGeneration,
          getEditor: () => this.editor,
          getTextSelection: () => this.getTextSelection(),
          getBlockSelectionBlockIds: () => this.getBlockSelectionBlockIds(),
          createExtract: message => this.onCreateExtract(message),
          showStatus: message => this.showStatus(message),
        },
        event
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Extract preparation failed: ${message}`);
      this.showStatus(`Extract failed: ${message}`);
      this.pendingExtract = null;
    }

    if (!this.pendingExtract) {
      this.suppressNextNativeCut = false;
    }
  }

  private async handleImageOcclusionShortcut(event: KeyboardEvent) {
    if (this.isEditorInputLocked()) {
      this.showLockedInputStatus();
      event.preventDefault();
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    if (!this.currentDoc || !this.currentPath) {
      this.showStatus('Open a note before adding image occlusion.');
      return;
    }

    const selectedImage = this.getSelectedImageBlock();
    if (!selectedImage) {
      this.showStatus('Select an image block before adding image occlusion.');
      return;
    }

    await this.flushHost();
    this.cancelPendingContentChanged();
    this.onOpenImageOcclusion({
      type: 'openImageOcclusion',
      sourcePath: this.currentPath,
      imageBlockId: selectedImage.imageBlockId,
      imageRelPath: selectedImage.imageRelPath,
      content: await this.exportCurrentContent(),
    });
  }

  public async createClozeFromSelection(
    cardContextId?: string | null,
    slotId?: string | null,
    displayClozeIndex?: number | null
  ) {
    await this.handleClozeCommand(undefined, { cardContextId, slotId, displayClozeIndex });
  }

  private async handleClozeCommand(
    event?: KeyboardEvent,
    options: {
      cardContextId?: string | null;
      slotId?: string | null;
      displayClozeIndex?: number | null;
    } = {}
  ) {
    await runClozeCommand(
      {
        isInputLocked: () => this.isEditorInputLocked(),
        getCurrentDoc: () => this.currentDoc,
        getCurrentPath: () => this.currentPath,
        getEditorRoot: () => this.editor,
        getTextSelection: () => this.getTextSelection(),
        clearTextSelection: () => {
          this.editor.host?.selection.clear(['text']);
          window.getSelection()?.removeAllRanges();
        },
        flushHost: () => this.flushHost(),
        cancelPendingContentChanged: () => this.cancelPendingContentChanged(),
        exportContent: () => this.exportCurrentContent(),
        saveCloze: message => this.onSaveCloze(message),
        showStatus: message => this.showStatus(message),
      },
      event,
      options
    );
  }

  private getTextSelection() {
    return this.editor.std?.selection.find('text') as TextSelection | null | undefined;
  }

  public async createLinkedNoteFromSlashMenu(
    viewMode: LinkedNoteViewMode,
    context: AffineSlashMenuContext
  ) {
    await runCreateLinkedNoteCommand(
      {
        isInputLocked: () => this.isEditorInputLocked(),
        getCurrentDoc: () => this.currentDoc,
        getCurrentPath: () => this.currentPath,
        getLoadGeneration: () => this.loadGeneration,
        createLinkedNote: message => this.onCreateLinkedNote(message),
        trackLinkedDoc: (doc, relativePath) => this.trackLinkedDoc(doc, relativePath),
        registerNestedLinkedDoc: (linkedDoc, generation) =>
          this.registerLinkedDoc(linkedDoc, generation),
        markLinkedDocPreviewed: doc => this.markLinkedDocPreviewed(doc),
        requestLinkedDocPreview: (doc, relativePath, generation) =>
          this.requestLinkedDocPreview(doc, relativePath, generation),
        flushHost: () => this.flushHost(),
        refreshMentionHighlights: () => this.mentions.refreshHighlights(),
        cancelPendingContentChanged: () => this.cancelPendingContentChanged(),
        recordUserEdit: () => {
          this.userEditGeneration++;
        },
        notifyContentChanged: async () => this.onContentChanged(await this.exportCurrentContent()),
        showStatus: message => this.showStatus(message),
      },
      viewMode,
      context
    );
  }

  public async insertPropertiesBlockFromSlashMenu(context: AffineSlashMenuContext) {
    await runInsertPropertiesBlockCommand(
      {
        isInputLocked: () => this.isEditorInputLocked(),
        getCurrentDoc: () => this.currentDoc,
        flushHost: () => this.flushHost(),
        cancelPendingContentChanged: () => this.cancelPendingContentChanged(),
        recordUserEdit: () => {
          this.userEditGeneration++;
        },
        notifyContentChanged: async () => this.onContentChanged(await this.exportCurrentContent()),
        showStatus: message => this.showStatus(message),
      },
      context
    );
  }

  public async insertImageFromSlashMenu(context: AffineSlashMenuContext) {
    if (this.isEditorInputLocked()) {
      this.showLockedInputStatus();
      return;
    }

    if (!this.currentDoc || !this.currentPath) {
      this.showStatus('Open a note before adding images.');
      return;
    }

    const files = await pickImageFiles();
    if (files.length === 0) {
      return;
    }

    await this.attachFiles(
      files,
      file =>
        this.insertImageAttachmentFromSlashContext(
          context,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64),
      file =>
        this.insertImageAttachmentFromSlashContext(
          context,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64,
          false));
  }

}

async function hydrateAttachmentImageBlocks(
  doc: Doc,
  sourcePath: string,
  collectionRoot: string,
  readAttachment: (message: ReadAttachmentMessage) => Promise<ReadAttachmentResult>
) {
  if (!sourcePath) {
    return;
  }

  for (const block of doc.getBlocks()) {
    const model = (block as { model?: unknown }).model ?? block;
    if (!isImageBlockModel(model)) {
      continue;
    }

    const sourceId = getImageSourceId(model);
    if (!sourceId) {
      continue;
    }

    if (await doc.blobSync.get(sourceId)) {
      continue;
    }

    const imageSource = normalizeImageSourceForHost(sourceId, collectionRoot) ?? sourceId;
    try {
      const result = await readAttachment({
        type: 'readAttachment',
        sourcePath,
        imageSource,
      });
      if (!result.ok) {
        const message = result.error ?? 'The image attachment could not be loaded.';
        reportDiagnostic('warn', `Image hydration skipped for ${imageSource}: ${message}`);
        continue;
      }

      const blob = createBlobFromReadAttachmentResult(result, imageSource);
      if (!blob) {
        reportDiagnostic('warn', `Image hydration skipped for ${imageSource}: missing image content.`);
        continue;
      }

      const hydratedSourceId = sanitizeAttachmentTarget(result.relativePath ?? imageSource);
      await doc.blobSync.set(hydratedSourceId, blob);
      if (hydratedSourceId !== sourceId) {
        setImageSourceId(model, hydratedSourceId);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Image hydration failed for ${imageSource}: ${message}`);
    }
  }
}

function getEventBlockModel(event: Event) {
  for (const target of event.composedPath()) {
    const candidate = target as { model?: unknown; focusModel?: unknown } | null;
    const model = candidate?.model ?? candidate?.focusModel;
    if (isTextBlockModel(model)) {
      return model;
    }
  }

  return null;
}

function isTextBlockModel(value: unknown): value is BlockModel {
  return typeof value === 'object'
    && value !== null
    && Boolean((value as { text?: unknown }).text);
}

function isHeadingBlockModel(value: unknown): value is BlockModel & { id: string } {
  const candidate = value as {
    id?: unknown;
    flavour?: unknown;
    text?: unknown;
    type?: unknown;
    props?: {
      type?: unknown;
    };
  } | null;
  const headingType = typeof candidate?.type === 'string'
    ? candidate.type
    : typeof candidate?.props?.type === 'string'
      ? candidate.props.type
      : '';

  return typeof candidate?.id === 'string'
    && candidate.flavour === 'affine:paragraph'
    && Boolean(candidate.text)
    && /^h[1-6]$/i.test(headingType);
}

function isEmptyTextBlockModel(value: unknown) {
  if (!isTextBlockModel(value)) {
    return false;
  }

  const candidate = value as {
    text?: { length?: number };
    children?: unknown[];
  };

  return (candidate.text?.length ?? 0) === 0
    && Array.isArray(candidate.children)
    && candidate.children.length === 0;
}

function getBlockSelectionId(value: unknown) {
  const blockId = typeof (value as { blockId?: unknown } | null)?.blockId === 'string'
    ? (value as { blockId: string }).blockId.trim()
    : '';

  return blockId.length > 0 ? [blockId] : [];
}

function uniqueBlockIds(values: string[]) {
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    const blockId = value.trim();
    if (!blockId || seen.has(blockId)) {
      continue;
    }

    seen.add(blockId);
    ids.push(blockId);
  }

  return ids;
}

function getBlockPlainText(value: unknown) {
  const text = (value as { text?: unknown } | null)?.text;
  if (typeof text === 'string') {
    return normalizeSelectionText(text);
  }

  if (text && typeof (text as { toString?: unknown }).toString === 'function') {
    const result = (text as { toString(): string }).toString();
    if (result && result !== '[object Object]') {
      return normalizeSelectionText(result);
    }
  }

  return '';
}

function createSelectedImageBlock(
  value: unknown,
  collectionRoot?: string | null
): SelectedImageBlock | null {
  if (!isImageBlockModel(value)) {
    return null;
  }

  const sourceId = getImageSourceId(value);
  const imageRelPath = sourceId
    ? normalizeImageSourceForHost(sourceId, collectionRoot)
    : null;
  if (!imageRelPath) {
    return null;
  }

  return {
    imageBlockId: value.id,
    imageRelPath,
  };
}

function isImageBlockModel(value: unknown): value is BlockModel & {
  id: string;
  sourceId?: unknown;
  props?: {
    sourceId?: unknown;
  };
} {
  return typeof value === 'object'
    && value !== null
    && (value as { flavour?: unknown }).flavour === 'affine:image'
    && typeof (value as { id?: unknown }).id === 'string';
}

function getImageSourceId(value: {
  sourceId?: unknown;
  props?: {
    sourceId?: unknown;
  };
}) {
  const candidate = typeof value.sourceId === 'string'
    ? value.sourceId
    : typeof value.props?.sourceId === 'string'
      ? value.props.sourceId
      : '';

  return candidate.replace(/\\/g, '/').trim() || null;
}

function normalizeImageSourceForHost(sourceId: string, collectionRoot?: string | null) {
  const normalizedSource = sourceId.replace(/\\/g, '/').trim();
  if (normalizedSource.length === 0) {
    return null;
  }

  const filePath = tryGetFileUriPath(normalizedSource);
  if (filePath && collectionRoot) {
    const relativePath = tryGetCollectionRelativePath(filePath, collectionRoot);
    if (relativePath) {
      return relativePath;
    }
  }

  const localPath = tryGetLocalPath(normalizedSource);
  if (localPath && collectionRoot) {
    const relativePath = tryGetCollectionRelativePath(localPath, collectionRoot);
    if (relativePath) {
      return relativePath;
    }
  }

  return isCollectionImageReference(normalizedSource)
    ? normalizedSource.replace(/^\/+/, '')
    : null;
}

function setImageSourceId(
  value: {
    sourceId?: unknown;
    props?: {
      sourceId?: unknown;
    };
  },
  sourceId: string
) {
  if (typeof value.sourceId === 'string') {
    value.sourceId = sourceId;
    return;
  }

  if (value.props) {
    value.props.sourceId = sourceId;
    return;
  }

  value.sourceId = sourceId;
}

function isImageAttachmentResult(
  result: AttachFileResult
): result is AttachFileResult & { relativePath: string } {
  return result.kind?.toLowerCase() === 'image'
    && typeof result.relativePath === 'string'
    && result.relativePath.trim().length > 0;
}

async function createLocalImageAttachmentPayload(
  sourceFile: File,
  fallbackName: string,
  contentBase64: string
): Promise<(AttachedFilePayload & {
  result: AttachFileResult & { relativePath: string };
}) | null> {
  const bytes = base64ToUint8Array(contentBase64);
  const contentType = sniffImageContentType(bytes)
    ?? normalizeSupportedImageContentType(sourceFile.type)
    ?? inferImageContentType(fallbackName);
  if (!contentType) {
    return null;
  }

  const extension = inferImageFileExtension(fallbackName)
    ?? inferFileExtension(contentType);
  if (!isSupportedImageExtension(extension)) {
    return null;
  }

  const relativePath = await createPreviewImageRelativePath(bytes, extension);
  return {
    result: {
      ok: true,
      displayName: sanitizeAttachmentLabel(fallbackName),
      relativePath,
      kind: 'Image',
    },
    fallbackName,
    sourceFile,
    contentBase64,
  };
}

async function registerImageAttachmentBlob(
  doc: Doc,
  result: AttachFileResult & { relativePath: string },
  fallbackName: string,
  sourceFile: File,
  contentBase64: string
) {
  const relativePath = sanitizeAttachmentTarget(result.relativePath ?? fallbackName);
  const blob = createBlobForImageAttachment(contentBase64, sourceFile, result, fallbackName);
  await doc.blobSync.set(relativePath, blob);
  return relativePath;
}

function tryGetFileUriPath(source: string) {
  try {
    const uri = new URL(source);
    if (uri.protocol !== 'file:') {
      return null;
    }

    return decodeURIComponent(uri.pathname)
      .replace(/^\/([A-Za-z]:\/)/, '$1')
      .replace(/\\/g, '/');
  } catch {
    return null;
  }
}

function tryGetLocalPath(source: string) {
  if (!/^[A-Za-z]:\//.test(source) && !source.startsWith('//')) {
    return null;
  }

  return source.replace(/^\/([A-Za-z]:\/)/, '$1');
}

function tryGetCollectionRelativePath(filePath: string, collectionRoot: string) {
  const root = collectionRoot.replace(/\\/g, '/').replace(/\/+$/, '');
  const fullPath = filePath.replace(/\\/g, '/');
  const rootPrefix = `${root}/`;
  if (root.length === 0 || !fullPath.toLowerCase().startsWith(rootPrefix.toLowerCase())) {
    return null;
  }

  return fullPath.slice(rootPrefix.length).replace(/^\/+/, '');
}

function isCollectionImageReference(source: string) {
  if (/^[a-z][a-z0-9+.-]*:/i.test(source)) {
    return false;
  }

  const path = source.replace(/^\/+/, '').split(/[?#]/, 1)[0];
  if (!inferImageContentType(path)) {
    return false;
  }

  return !/^[A-Za-z]:\//.test(path)
    && !path.startsWith('//')
    && !path.includes('..');
}

function normalizeLocalPath(path: string) {
  return path.replace(/\\/g, '/').trim().replace(/\/+$/, '');
}

function createAttachmentReferenceText(result: AttachFileResult, fallbackName: string) {
  const displayName = sanitizeAttachmentLabel(result.displayName ?? fallbackName);
  const relativePath = sanitizeAttachmentTarget(result.relativePath ?? fallbackName);
  const target = relativePath.includes(' ') || /[()<>]/.test(relativePath)
    ? `<${relativePath.replace(/>/g, '%3E')}>`
    : relativePath;

  return result.kind?.toLowerCase() === 'image'
    ? `![${displayName}](${target})`
    : `[${displayName}](${target})`;
}

function createBlobForImageAttachment(
  contentBase64: string,
  sourceFile: File,
  result: AttachFileResult,
  fallbackName: string
) {
  const bytes = base64ToUint8Array(contentBase64);
  const contentType = sniffImageContentType(bytes)
    ?? normalizeSupportedImageContentType(sourceFile.type)
    ?? inferImageContentType(result.relativePath ?? result.displayName ?? fallbackName)
    ?? inferImageContentType(fallbackName)
    ?? 'application/octet-stream';

  return new File(
    [bytes],
    sanitizeAttachmentLabel(result.displayName ?? fallbackName),
    { type: contentType });
}

function createBlobFromReadAttachmentResult(result: ReadAttachmentResult, fallbackName: string) {
  const contentBase64 = result.contentBase64?.trim();
  if (!contentBase64) {
    return null;
  }

  const bytes = base64ToUint8Array(contentBase64);
  const contentType = normalizeImageContentType(result.contentType ?? '')
    ?? inferImageContentType(result.relativePath ?? result.fileName ?? fallbackName)
    ?? 'application/octet-stream';

  return new File(
    [bytes],
    sanitizeAttachmentLabel(result.fileName ?? fallbackName),
    { type: contentType });
}

function base64ToUint8Array(value: string) {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

function normalizeImageContentType(value: string) {
  const contentType = value.trim().toLowerCase();
  return contentType.startsWith('image/') ? contentType : null;
}

function normalizeSupportedImageContentType(value: string) {
  const contentType = normalizeImageContentType(value);
  return contentType && isSupportedImageExtension(inferFileExtension(contentType))
    ? contentType
    : null;
}

function inferImageContentType(value: string) {
  const extension = value
    .split(/[?#]/, 1)[0]
    .split('.')
    .pop()
    ?.trim()
    .toLowerCase();

  return extension === 'png'
    ? 'image/png'
    : extension === 'jpg' || extension === 'jpeg'
      ? 'image/jpeg'
      : extension === 'webp'
        ? 'image/webp'
        : extension === 'gif'
          ? 'image/gif'
          : null;
}

function inferImageFileExtension(value: string) {
  const extension = value
    .split(/[?#]/, 1)[0]
    .split('.')
    .pop()
    ?.trim()
    .toLowerCase();

  if (!extension) {
    return null;
  }

  const normalizedExtension = `.${extension}`;
  return isSupportedImageExtension(normalizedExtension)
    ? normalizedExtension
    : null;
}

function isSupportedImageExtension(value: string | null | undefined) {
  return value === '.png'
    || value === '.jpg'
    || value === '.jpeg'
    || value === '.webp'
    || value === '.gif';
}

async function createPreviewImageRelativePath(bytes: Uint8Array, extension: string) {
  const sha256 = await computeSha256Hex(bytes);
  const fileName = sha256
    ? `${sha256}${extension}`
    : `pending-${createPreviewToken()}${extension}`;

  return `attachments/images/${fileName}`;
}

async function computeSha256Hex(bytes: Uint8Array) {
  if (!globalThis.crypto?.subtle) {
    return null;
  }

  const copy = new Uint8Array(bytes.byteLength);
  copy.set(bytes);
  const digest = await globalThis.crypto.subtle.digest('SHA-256', copy.buffer);
  return Array.from(new Uint8Array(digest))
    .map(byte => byte.toString(16).padStart(2, '0'))
    .join('');
}

function createPreviewToken() {
  return globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function sniffImageContentType(bytes: Uint8Array) {
  if (bytes.length >= 8
    && bytes[0] === 0x89
    && bytes[1] === 0x50
    && bytes[2] === 0x4e
    && bytes[3] === 0x47
    && bytes[4] === 0x0d
    && bytes[5] === 0x0a
    && bytes[6] === 0x1a
    && bytes[7] === 0x0a) {
    return 'image/png';
  }

  if (bytes.length >= 3
    && bytes[0] === 0xff
    && bytes[1] === 0xd8
    && bytes[2] === 0xff) {
    return 'image/jpeg';
  }

  if (bytes.length >= 6
    && bytes[0] === 0x47
    && bytes[1] === 0x49
    && bytes[2] === 0x46
    && bytes[3] === 0x38
    && (bytes[4] === 0x37 || bytes[4] === 0x39)
    && bytes[5] === 0x61) {
    return 'image/gif';
  }

  if (bytes.length >= 12
    && bytes[0] === 0x52
    && bytes[1] === 0x49
    && bytes[2] === 0x46
    && bytes[3] === 0x46
    && bytes[8] === 0x57
    && bytes[9] === 0x45
    && bytes[10] === 0x42
    && bytes[11] === 0x50) {
    return 'image/webp';
  }

  return null;
}

function sanitizeAttachmentLabel(value: string) {
  return value
    .replace(/[\]\[\r\n]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim() || 'Attachment';
}

function sanitizeAttachmentTarget(value: string) {
  return value
    .replace(/\\/g, '/')
    .replace(/[\r\n]+/g, '')
    .trim() || 'Attachment';
}

function readFileAsBase64(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('The dropped file could not be read.'));
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : '';
      const separatorIndex = result.indexOf(',');
      resolve(separatorIndex >= 0 ? result.slice(separatorIndex + 1) : result);
    };
    reader.readAsDataURL(file);
  });
}

function pickImageFiles() {
  return new Promise<File[]>(resolve => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg,image/webp,image/gif';
    input.multiple = true;
    input.style.display = 'none';

    const complete = () => {
      const files = Array.from(input.files ?? []).filter(file => file.size > 0);
      input.remove();
      resolve(files);
    };

    input.addEventListener('change', complete, { once: true });
    input.addEventListener('cancel', complete, { once: true });
    document.body.append(input);
    input.click();
  });
}

function getClipboardFiles(clipboardData: DataTransfer | null): File[] {
  if (!clipboardData) {
    return [];
  }

  const directFiles = Array.from(clipboardData.files ?? []).filter(file => file.size > 0);
  if (directFiles.length > 0) {
    return directFiles;
  }

  return Array.from(clipboardData.items ?? [])
    .filter(item => item.kind === 'file')
    .map(item => item.getAsFile())
    .filter((file): file is File => Boolean(file && file.size > 0));
}

function getSafeAttachmentFileName(file: File) {
  const trimmedName = file.name.trim();
  const inferredExtension = inferFileExtension(file.type);
  if (trimmedName.length > 0) {
    return hasFileExtension(trimmedName) || inferredExtension === '.bin'
      ? trimmedName
      : `${trimmedName}${inferredExtension}`;
  }

  return `clipboard-image${inferredExtension}`;
}

function hasFileExtension(value: string) {
  return /(?:^|[/\\])[^/\\]+\.[^./\\]+$/.test(value);
}

function inferFileExtension(contentType: string) {
  const normalized = contentType.trim().toLowerCase();
  return normalized === 'image/png'
    ? '.png'
    : normalized === 'image/jpeg'
      ? '.jpg'
      : normalized === 'image/webp'
        ? '.webp'
        : normalized === 'image/gif'
          ? '.gif'
          : '.bin';
}
*/
