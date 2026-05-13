import { MarkdownTransformer, type AffineSlashMenuContext } from '@blocksuite/blocks';
import {
  getInlineEditorByModel,
  RefNodeSlotsProvider,
} from '@blocksuite/affine-components/rich-text';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { type PageEditor } from '@blocksuite/presets';
import { type BlockModel, type Doc } from '@blocksuite/store';
import {
  defineCogntPropertiesBlockElement,
} from './blocks/properties';
import {
  defineCogntReferencesBlockElement,
} from './blocks/references';
import { type TextSelection } from './cloze';
import {
  type CreateExtractCommandMessage,
  type PendingExtract,
  runClozeCommand,
  runCreateExtractCommand,
  runCreateLinkedNoteCommand,
  runInsertPropertiesBlockCommand,
  type SaveClozeCommandMessage,
} from './commands';
import {
  createTitleFromPlainText,
  isBlockSuiteJsonPath,
  normalizeSelectionText,
} from './documents/block-suite-document';
import {
  createDocFromBlockSuiteJson,
  createDocFromMarkdown,
  createEmptyMarkdownDoc,
  exportDocContent,
  importBlockSuiteJsonIntoExistingDoc,
} from './documents/io';
import {
  configureCogntSlashMenu,
  type LinkedNoteViewMode,
} from './editor/slash-menu';
import {
  findEditableElement,
  getInteractivityDiagnostic,
  waitForNextAnimationFrame,
} from './editor/dom';
import {
  flushEditorHost,
  focusFirstTextBlock,
  forceEditableRichText,
  forceReadonlyRichText,
  setDocReadonly,
} from './editor/host-operations';
import {
  isEditorShortcut,
  preventReadOnlyKeyboardInput,
} from './editor/keyboard';
import {
  formatElapsed,
  logEditorTiming,
} from './editor/timing';
import { reportDiagnostic } from './host';
import {
  MentionController,
  type OpenMentionMessage,
  type RequestMentionPreviewMessage,
  type SearchMentionTargetsMessage,
} from './mention-controller';
import {
  DocumentLinksController,
  type DocumentLinksPayload,
} from './document-links';
import {
  createEditorSpecs,
  patchBlockStdRender,
} from './editor/specs';
import {
  attachCogntPathToInlineReference,
  getCogntLinkedDocOpenIntent,
  getCogntLinkedDocToolbarModel,
  getCogntLinkedNotePathFromModel,
  getExtractTitleFromRelPath,
  getInlineLinkedPageReferenceFromEvent,
  getLinkedDocReferenceFromModel,
  getLinkedNotePathFromResult,
  getLinkedNoteTitle,
  getLinkedNoteTitleFromResult,
  getLinkedPageReferencePath,
  getLinkedPageReferenceTitle,
  isCogntLinkedDocModel,
  isOpenThisDocToolbarIntent,
  normalizeCreatedExtractPath,
  normalizeLinkedNotePathKey,
  registerSerializedCogntEmbedLinkedNotes,
  registerSerializedCogntInlineLinkedNotes,
  replaceExtractBlocksWithLinkedPage,
  replaceExtractTextRangeWithLinkedPage,
  type LinkedPageReference,
} from './links/linked-notes';
import { type MentionPreviewResult, type MentionTarget } from './mentions';
import type { CogntSlashMenuRootComponent } from './blocksuite-compat';

const saveDebounceMs = 500;
const statusHideMs = 2600;
const linkedDocPreviewMaxCharacters = 4000;
const editorsByDoc = new WeakMap<Doc, MarkdownBlockSuiteEditor>();

defineCogntPropertiesBlockElement();
defineCogntReferencesBlockElement();
configureCogntSlashMenu({
  getEditor: doc => editorsByDoc.get(doc),
});
blockEffects();
presetEffects();
patchBlockStdRender();

export type {
  CreateExtractCommandMessage as CreateExtractMessage,
  SaveClozeCommandMessage as SaveClozeMessage,
} from './commands';

export type { LinkedNoteViewMode } from './editor/slash-menu';

export type CreateLinkedNoteMessage = {
  type: 'createLinkedNote';
  sourcePath: string;
  viewMode: LinkedNoteViewMode;
};

export type CreateLinkedNoteResult = {
  ok: boolean;
  relativePath?: string;
  title?: string;
  documentContent?: string;
  error?: string;
};

export type AttachFileMessage = {
  type: 'attachFile';
  sourcePath: string;
  fileName: string;
  contentBase64: string;
};

export type AttachFileResult = {
  ok: boolean;
  displayName?: string;
  relativePath?: string;
  kind?: string;
  error?: string;
};

export type ReadAttachmentMessage = {
  type: 'readAttachment';
  sourcePath: string;
  imageSource: string;
};

export type ReadAttachmentResult = {
  ok: boolean;
  fileName?: string;
  relativePath?: string;
  contentType?: string;
  contentBase64?: string;
  error?: string;
};

export type OpenImageOcclusionMessage = {
  type: 'openImageOcclusion';
  sourcePath: string;
  imageBlockId: string;
  imageRelPath: string;
  content: string;
};

export type SelectedImageBlock = {
  imageBlockId: string;
  imageRelPath: string;
};

export type SelectedCardContextBlocks = {
  blockIds: string[];
  title?: string;
};

type AttachedFilePayload = {
  result: AttachFileResult;
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

type PendingImageAttachmentPreview = {
  doc: Doc;
  blockId: string;
  sourceId: string;
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

export type MarkdownEditorOptions = {
  hostElement: HTMLElement;
  documentLinksElement: HTMLElement;
  statusElement: HTMLElement;
  onContentChanged(content: string): void;
  onCreateExtract(message: CreateExtractCommandMessage): void;
  onSaveCloze(message: SaveClozeCommandMessage): void;
  onSearchMentionTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  onRequestMentionPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
  onCreateLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  onAttachFile(message: AttachFileMessage): Promise<AttachFileResult>;
  onReadAttachment(message: ReadAttachmentMessage): Promise<ReadAttachmentResult>;
  onOpenImageOcclusion(message: OpenImageOcclusionMessage): void;
};

export type LoadMarkdownOptions = {
  loadId?: number;
  path?: string;
  title?: string;
  content?: string;
  collectionRoot?: string;
};

export class MarkdownBlockSuiteEditor {
  private readonly eventAbort = new AbortController();
  private readonly editor: PageEditor;
  private readonly statusElement: HTMLElement;
  private readonly mentions: MentionController;
  private readonly documentLinks: DocumentLinksController;
  private readonly onContentChanged: (content: string) => void;
  private readonly onCreateExtract: (message: CreateExtractCommandMessage) => void;
  private readonly onSaveCloze: (message: SaveClozeCommandMessage) => void;
  private readonly onRequestMentionPreview: (message: RequestMentionPreviewMessage) => void;
  private readonly onOpenMention: (message: OpenMentionMessage) => void;
  private readonly onCreateLinkedNote: (
    message: CreateLinkedNoteMessage
  ) => Promise<CreateLinkedNoteResult>;
  private readonly onAttachFile: (message: AttachFileMessage) => Promise<AttachFileResult>;
  private readonly onReadAttachment: (message: ReadAttachmentMessage) => Promise<ReadAttachmentResult>;
  private readonly onOpenImageOcclusion: (message: OpenImageOcclusionMessage) => void;
  private currentDoc: Doc | null = null;
  private currentPath = '';
  private currentCollectionRoot = '';
  private loadingDocument = false;
  private readOnlyMode = false;
  private saveTimer: number | undefined;
  private statusTimer: number | undefined;
  private blockUpdatedDisposable: { dispose(): void } | null = null;
  private linkedDocOpenDisposable: { dispose(): void } | null = null;
  private readonly linkedDocPathsByPageId = new Map<string, string>();
  private readonly linkedNoteTitlesByPath = new Map<string, string>();
  private readonly linkedDocPreviewRequests = new Map<
    string,
    {
      doc: Doc;
      generation: number;
      relativePath: string;
    }
  >();
  private linkedDocPreviewRequestCounter = 0;
  private linkedDocPreviewedPageIds = new Set<string>();
  private linkedDocPreviewRefreshTimer: number | undefined;
  private linkedDocOpenRetryTimer: number | undefined;
  private pendingExtract: PendingExtract | null = null;
  private suppressNextNativeCut = false;
  private loadGeneration = 0;
  private userEditGeneration = 0;
  private lockedInputStatusShownAt = 0;
  private pendingAttachmentPreviews = 0;

  public constructor(options: MarkdownEditorOptions) {
    this.statusElement = options.statusElement;
    this.onContentChanged = options.onContentChanged;
    this.onCreateExtract = options.onCreateExtract;
    this.onSaveCloze = options.onSaveCloze;
    this.onRequestMentionPreview = options.onRequestMentionPreview;
    this.onOpenMention = options.onOpenMention;
    this.onCreateLinkedNote = options.onCreateLinkedNote;
    this.onAttachFile = options.onAttachFile;
    this.onReadAttachment = options.onReadAttachment;
    this.onOpenImageOcclusion = options.onOpenImageOcclusion;
    this.editor = document.createElement('page-editor') as PageEditor;
    this.editor.id = 'editor';
    this.editor.doc = createEmptyMarkdownDoc();
    this.trackCurrentDoc(this.editor.doc);
    this.editor.hasViewport = true;
    this.mentions = new MentionController({
      editor: this.editor,
      getCurrentPath: () => this.currentPath,
      flushHost: () => this.flushHost(),
      scheduleContentChanged: () => this.scheduleContentChanged(),
      showStatus: message => this.showStatus(message),
      onSearchTargets: async message => {
        const targets = await options.onSearchMentionTargets(message);
        this.mergeMentionTargetTitles(targets, false);
        return targets;
      },
    });
    this.documentLinks = new DocumentLinksController({
      container: options.documentLinksElement,
      getDoc: () => this.currentDoc,
      openDocument: relativePath => this.openDocumentLink(relativePath),
    });
    this.editor.specs = createEditorSpecs((query, abort, inlineEditor, abortSignal) =>
      this.mentions.getNativeMentionMenus(query, abort, inlineEditor, abortSignal)
    );
    options.hostElement.append(this.editor);
    this.bindLinkedDocOpenEvents();
    this.bindEditorEvents();
  }

  public async load(options: LoadMarkdownOptions) {
    const generation = ++this.loadGeneration;
    const startedAt = performance.now();

    this.loadingDocument = true;
    this.enableEditorMode();
    window.clearTimeout(this.saveTimer);
    this.mentions.cancelScheduledHighlightUpdate();
    this.currentPath = options.path ?? '';
    this.currentCollectionRoot = normalizeLocalPath(options.collectionRoot ?? '');
    this.clearLinkedDocState();

    try {
      const content = options.content ?? '';
      const isBlockSuiteJson = isBlockSuiteJsonPath(options.path);
      const loaded = isBlockSuiteJson
        ? await this.loadCompleteBlockSuiteJson(options, content, generation)
        : await this.loadCompleteMarkdown(options, content, generation);
      if (loaded) {
        reportDiagnostic(
          'info',
          isBlockSuiteJson
            ? `Loaded BlockSuite document JSON (${content.length} characters, ${formatElapsed(startedAt)}).`
            : `Imported Markdown into BlockSuite (${content.length} characters, ${formatElapsed(startedAt)}).`
        );
        this.showStatus(`Opened ${options.title ?? 'document'}.`);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not load document: ${message}`);
      this.showStatus(`Could not load document: ${message}`);
      throw error;
    } finally {
      if (generation === this.loadGeneration) {
        this.loadingDocument = false;
      }
    }
  }

  private async loadCompleteMarkdown(
    options: LoadMarkdownOptions,
    markdown: string,
    generation: number
  ) {
    const importStartedAt = performance.now();
    const nextDoc = await createDocFromMarkdown(
      markdown,
      linkedDoc => this.registerLinkedDoc(linkedDoc, generation),
      relativePath => this.resolveLinkedNoteTitle(relativePath)
    );
    await hydrateAttachmentImageBlocks(
      nextDoc,
      this.currentPath,
      this.currentCollectionRoot,
      this.onReadAttachment);
    logEditorTiming('full Markdown import', importStartedAt, `${markdown.length} chars`);

    if (generation !== this.loadGeneration) {
      return false;
    }

    const assignStartedAt = performance.now();
    this.trackCurrentDoc(nextDoc);
    this.editor.doc = nextDoc;
    this.enableEditorMode();
    this.editor.hasViewport = true;
    this.bindDocChangeEvents(nextDoc);
    this.documentLinks.setLinks({
      incoming: [],
      outgoing: [],
    });
    nextDoc.resetHistory();
    logEditorTiming('editor.doc assignment', assignStartedAt, options.title);

    const flushStartedAt = performance.now();
    await this.flushHost();
    logEditorTiming('host flush/render', flushStartedAt, options.title);

    if (generation !== this.loadGeneration) {
      return false;
    }

    const unlocked = await this.unlockEditorAfterRender(generation, 'full Markdown import');
    if (!unlocked) {
      return false;
    }

    this.deferPostLoadWork(generation);
    return true;
  }

  private async loadCompleteBlockSuiteJson(
    options: LoadMarkdownOptions,
    content: string,
    generation: number
  ) {
    const importStartedAt = performance.now();
    const nextDoc = await createDocFromBlockSuiteJson(
      content,
      linkedDoc => this.registerLinkedDoc(linkedDoc, generation),
      relativePath => this.resolveLinkedNoteTitle(relativePath)
    );
    await hydrateAttachmentImageBlocks(
      nextDoc,
      this.currentPath,
      this.currentCollectionRoot,
      this.onReadAttachment);
    logEditorTiming('BlockSuite JSON import', importStartedAt, `${content.length} chars`);

    if (generation !== this.loadGeneration) {
      return false;
    }

    const assignStartedAt = performance.now();
    this.trackCurrentDoc(nextDoc);
    this.editor.doc = nextDoc;
    this.enableEditorMode();
    this.editor.hasViewport = true;
    this.bindDocChangeEvents(nextDoc);
    this.documentLinks.setLinks({
      incoming: [],
      outgoing: [],
    });
    nextDoc.resetHistory();
    logEditorTiming('editor.doc assignment', assignStartedAt, options.title);

    const flushStartedAt = performance.now();
    await this.flushHost();
    logEditorTiming('host flush/render', flushStartedAt, options.title);

    if (generation !== this.loadGeneration) {
      return false;
    }

    const unlocked = await this.unlockEditorAfterRender(generation, 'BlockSuite JSON import');
    if (!unlocked) {
      return false;
    }

    this.deferPostLoadWork(generation);
    return true;
  }

  private async unlockEditorAfterRender(generation: number, operation: string) {
    if (generation !== this.loadGeneration || !this.currentDoc) {
      return false;
    }

    const startedAt = performance.now();
    this.enableEditorMode();
    setDocReadonly(this.currentDoc, false);

    await waitForNextAnimationFrame();

    if (generation !== this.loadGeneration || !this.currentDoc) {
      return false;
    }

    await forceEditableRichText(this.editor);
    logEditorTiming(`${operation} editable unlock`, startedAt);
    reportDiagnostic(
      'info',
      `Editor unlocked after ${operation} (${getInteractivityDiagnostic(this.currentDoc, document)}).`
    );
    return true;
  }

  private deferPostLoadWork(generation: number) {
    const run = () => {
      if (generation !== this.loadGeneration || !this.currentDoc) {
        return;
      }

      const focusStartedAt = performance.now();
      focusFirstTextBlock(this.editor, this.currentDoc);
      logEditorTiming('focus first text block', focusStartedAt);

      const mentionStartedAt = performance.now();
      this.mentions.refreshHighlights();
      logEditorTiming('mention highlight refresh', mentionStartedAt);
    };

    const idleCallback = window.requestIdleCallback;
    if (idleCallback) {
      idleCallback(run, { timeout: 1200 });
      return;
    }

    window.setTimeout(run, 80);
  }

  public async focusEditor() {
    await this.activateEditorMode();

    const target =
      findEditableElement(this.editor) ??
      findEditableElement(this.editor.shadowRoot) ??
      findEditableElement(document);

    this.editor.focus();
    target?.focus({ preventScroll: true });
  }

  public getSelectedImageBlock(): SelectedImageBlock | null {
    const selectedBlockIds = uniqueBlockIds([
      ...this.getBlockSelectionBlockIds(),
      ...this.getTextSelectionImageCandidateBlockIds(),
    ]);

    for (const blockId of selectedBlockIds) {
      const model = this.currentDoc?.getBlock(blockId)?.model;
      const selectedImage = this.createSelectedImageBlockFromModel(model);
      if (selectedImage) {
        return selectedImage;
      }
    }

    const persistableImages = this.getImageBlocksInDocument()
      .map(image => this.createSelectedImageBlockFromModel(image))
      .filter((image): image is SelectedImageBlock => image !== null);
    return persistableImages.length === 1
      ? persistableImages[0]
      : null;
  }

  public getSelectedCardContextBlocks(): SelectedCardContextBlocks | null {
    const blockIds = this.getSelectedCardContextBlockIds();
    if (blockIds.length === 0) {
      return null;
    }

    return {
      blockIds,
      title: this.createSelectedCardContextTitle(blockIds),
    };
  }

  public async activateEditorMode() {
    this.enableEditorMode();
    await this.flushHost();
    await waitForNextAnimationFrame();
    await forceEditableRichText(this.editor);
    focusFirstTextBlock(this.editor, this.currentDoc);
    reportDiagnostic(
      'info',
      `Editor mode active (${getInteractivityDiagnostic(this.currentDoc, document)}).`
    );
  }

  public async setReadOnlyMode(readOnly: boolean) {
    if (!readOnly) {
      await this.activateEditorMode();
      return;
    }

    this.readOnlyMode = true;
    document.body.dataset.mode = 'readonly';
    setDocReadonly(this.currentDoc, true);
    await this.flushHost();
    await waitForNextAnimationFrame();
    await forceReadonlyRichText(this.editor);
    this.showStatus('Read-only.');
    reportDiagnostic(
      'info',
      `Read-only mode active (${getInteractivityDiagnostic(this.currentDoc, document)}).`
    );
  }

  private enableEditorMode() {
    this.readOnlyMode = false;
    document.body.dataset.mode = 'editor';
    setDocReadonly(this.currentDoc, false);
  }

  private isEditorInputLocked() {
    return this.readOnlyMode;
  }

  private showLockedInputStatus() {
    const now = performance.now();
    if (now - this.lockedInputStatusShownAt < 1200) {
      return;
    }

    this.lockedInputStatusShownAt = now;
    this.showStatus('This view is read-only.');
  }

  public showStatus(message: string) {
    this.statusElement.textContent = message;
    this.statusElement.dataset.visible = 'true';
    window.clearTimeout(this.statusTimer);
    this.statusTimer = window.setTimeout(() => {
      this.statusElement.dataset.visible = 'false';
    }, statusHideMs);
  }

  public async handleExtractResult(ok: boolean, createdPath?: string, error?: string) {
    const pending = this.pendingExtract;
    this.pendingExtract = null;

    if (!ok) {
      this.showStatus(`Extract failed: ${error ?? 'Unknown error'}`);
      return;
    }

    const relativePath = normalizeCreatedExtractPath(createdPath);
    if (!relativePath) {
      this.showStatus('Extract created, but the new note path was not returned.');
      return;
    }

    if (
      !pending ||
      pending.generation !== this.loadGeneration ||
      pending.sourcePath !== this.currentPath ||
      !this.currentDoc
    ) {
      this.showStatus(`Created ${relativePath.split(/[\\/]/).pop() ?? 'extract file'}.`);
      return;
    }

    try {
      const title = getLinkedNoteTitle(
        relativePath,
        this.resolveLinkedNoteTitle(relativePath) ?? getExtractTitleFromRelPath(relativePath)
      );
      const linkedDoc = pending.replacement.kind === 'blocks'
        ? replaceExtractBlocksWithLinkedPage(this.currentDoc, pending.replacement.blockIds, relativePath, title)
        : replaceExtractTextRangeWithLinkedPage(this.currentDoc, pending.replacement, relativePath, title);
      this.trackLinkedDoc(linkedDoc, relativePath);
      this.requestLinkedDocPreview(linkedDoc, relativePath, pending.generation);

      this.editor.host?.selection.clear(['text']);
      window.getSelection()?.removeAllRanges();
      await this.flushHost();
      this.mentions.refreshHighlights();
      this.cancelPendingContentChanged();
      this.userEditGeneration++;
      this.onContentChanged(await this.exportCurrentContent());
      this.showStatus(`Extracted to ${title}.`);
    } catch (replacementError) {
      const message = replacementError instanceof Error
        ? replacementError.message
        : String(replacementError);
      reportDiagnostic('warn', `Extract created but source link replacement failed: ${message}`);
      this.showStatus(`Extract created, but source text was not replaced: ${message}`);
    }
  }

  public setMentionTargets(targets: MentionTarget[]) {
    reportDiagnostic('info', `Received ${targets.length} linked note target(s).`);
    this.mergeMentionTargetTitles(targets, true);
    this.mentions.setTargets(targets);

    if (this.currentDoc) {
      const changed = this.refreshCurrentLinkedDocTitlesFromTargets();
      if (changed) {
        void this.flushHost().then(() => {
          if (!this.loadingDocument && this.currentPath) {
            this.scheduleContentChanged();
          }
        });
      }
    }
  }

  public setDocumentLinks(payload: DocumentLinksPayload) {
    this.documentLinks.setLinks(payload);
  }

  private refreshCurrentLinkedDocTitlesFromTargets() {
    if (!this.currentDoc) {
      return false;
    }

    const resolveTitle = (relativePath: string) => this.resolveLinkedNoteTitle(relativePath);
    const embedChanged = registerSerializedCogntEmbedLinkedNotes(
      this.currentDoc,
      linkedDoc => this.trackLinkedDoc(linkedDoc.doc, linkedDoc.relativePath),
      resolveTitle
    );
    const inlineChanged = registerSerializedCogntInlineLinkedNotes(
      this.currentDoc,
      resolveTitle
    );

    return embedChanged || inlineChanged;
  }

  private mergeMentionTargetTitles(targets: MentionTarget[], replace: boolean) {
    if (replace) {
      this.linkedNoteTitlesByPath.clear();
    }

    for (const target of targets) {
      const title = target.title.trim();
      if (title.length > 0) {
        this.linkedNoteTitlesByPath.set(normalizeLinkedNotePathKey(target.relativePath), title);
      }
    }
  }

  public handleMentionPreviewResult(result: MentionPreviewResult) {
    if (this.linkedDocPreviewRequests.has(result.requestId)) {
      void this.handleLinkedDocPreviewResult(result);
    }
  }

  public handleMentionOpenResult(_ok: boolean, _error?: string) {}

  public dispose() {
    window.clearTimeout(this.saveTimer);
    window.clearTimeout(this.statusTimer);
    this.blockUpdatedDisposable?.dispose();
    this.blockUpdatedDisposable = null;
    this.linkedDocOpenDisposable?.dispose();
    this.linkedDocOpenDisposable = null;
    window.clearTimeout(this.linkedDocOpenRetryTimer);
    this.clearLinkedDocState();
    this.mentions.dispose();
    this.eventAbort.abort();
    this.editor.remove();
    this.untrackCurrentDoc();
    this.currentPath = '';
  }

  private trackCurrentDoc(doc: Doc) {
    this.untrackCurrentDoc();
    this.currentDoc = doc;
    editorsByDoc.set(doc, this);
  }

  private untrackCurrentDoc() {
    if (this.currentDoc) {
      editorsByDoc.delete(this.currentDoc);
    }

    this.currentDoc = null;
  }

  private bindLinkedDocOpenEvents(retryCount = 0) {
    if (this.linkedDocOpenDisposable) {
      return;
    }

    const refNodeSlotsProvider = this.editor.std.getOptional(RefNodeSlotsProvider);
    if (!refNodeSlotsProvider) {
      if (retryCount < 60 && !this.eventAbort.signal.aborted) {
        window.clearTimeout(this.linkedDocOpenRetryTimer);
        this.linkedDocOpenRetryTimer = window.setTimeout(
          () => this.bindLinkedDocOpenEvents(retryCount + 1),
          50
        );
      }
      return;
    }

    window.clearTimeout(this.linkedDocOpenRetryTimer);
    this.linkedDocOpenRetryTimer = undefined;
    this.linkedDocOpenDisposable =
      refNodeSlotsProvider.docLinkClicked.on((reference: LinkedPageReference) => {
        void this.openLinkedDocReference(reference);
      });
  }

  private async openLinkedDocReference(reference: LinkedPageReference) {
    const pageId = reference.pageId;
    if (!pageId || !this.currentPath) {
      return;
    }

    let relativePath: string | null | undefined = this.getLinkedPageReferencePath(reference);

    if (!relativePath) {
      try {
        relativePath = await this.materializeNativeLinkedDocReference(reference);
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        reportDiagnostic('error', `Could not create New Doc: ${message}`);
        this.showStatus(`New Doc failed: ${message}`);
        return;
      }
    }

    if (!relativePath) {
      return;
    }

    this.onOpenMention({
      type: 'openMention',
      sourcePath: this.currentPath,
      relativePath,
      content: await this.exportCurrentContent(),
    });
    this.showStatus(`Opening ${getLinkedPageReferenceTitle(reference) ?? relativePath}...`);
  }

  private async materializeNativeLinkedDocReference(reference: LinkedPageReference) {
    const pageId = reference.pageId;
    if (!pageId || !this.currentDoc || !this.currentPath) {
      return null;
    }

    const result = await this.onCreateLinkedNote({
      type: 'createLinkedNote',
      sourcePath: this.currentPath,
      viewMode: 'native',
    });

    if (!result.ok) {
      throw new Error(result.error ?? 'The new document could not be created.');
    }

    const relativePath = getLinkedNotePathFromResult(result);
    if (!relativePath) {
      throw new Error('The new document path was not returned.');
    }

    const title = getLinkedNoteTitleFromResult(result);
    this.linkedDocPathsByPageId.set(pageId, relativePath);
    const attached = attachCogntPathToInlineReference(this.currentDoc, pageId, relativePath, title);
    if (!attached) {
      reportDiagnostic(
        'warn',
        `Created New Doc ${relativePath}, but could not persist its inline reference path.`
      );
    }

    this.cancelPendingContentChanged();
    this.userEditGeneration++;
    this.onContentChanged(await this.exportCurrentContent());
    this.showStatus(`Created ${relativePath}.`);

    return relativePath;
  }

  private getLinkedPageReferencePath(reference: LinkedPageReference) {
    const pageId = reference.pageId;
    return (pageId ? this.linkedDocPathsByPageId.get(pageId) : undefined)
      ?? getLinkedPageReferencePath(reference);
  }

  private resolveLinkedNoteTitle(relativePath: string) {
    return this.linkedNoteTitlesByPath.get(normalizeLinkedNotePathKey(relativePath)) ?? null;
  }

  private registerLinkedDoc(
    linkedDoc: { doc: Doc; relativePath: string },
    generation: number
  ) {
    this.trackLinkedDoc(linkedDoc.doc, linkedDoc.relativePath);
    if (this.linkedDocPreviewedPageIds.has(linkedDoc.doc.id)) {
      return;
    }

    this.markLinkedDocPreviewed(linkedDoc.doc);
    this.requestLinkedDocPreview(linkedDoc.doc, linkedDoc.relativePath, generation);
  }

  private trackLinkedDoc(doc: Doc, relativePath: string) {
    this.linkedDocPathsByPageId.set(doc.id, relativePath);
  }

  private markLinkedDocPreviewed(doc: Doc) {
    this.linkedDocPreviewedPageIds.add(doc.id);
  }

  private async openLinkedDocFromModel(model: unknown) {
    const reference = getLinkedDocReferenceFromModel(model);
    if (!reference) {
      return;
    }

    if (reference.relativePath) {
      this.linkedDocPathsByPageId.set(reference.pageId, reference.relativePath);
    }

    await this.openLinkedDocReference(reference);
  }

  private async openDocumentLink(relativePath: string) {
    if (!this.currentPath || !relativePath) {
      return;
    }

    this.onOpenMention({
      type: 'openMention',
      sourcePath: this.currentPath,
      relativePath,
      content: await this.exportCurrentContent(),
    });
    this.showStatus(`Opening ${relativePath}...`);
  }

  private requestLinkedDocPreview(doc: Doc, relativePath: string, generation: number) {
    if (!this.currentPath) {
      return;
    }

    this.markLinkedDocPreviewed(doc);
    const requestId = `linked-doc-${Date.now().toString(36)}-${(
      ++this.linkedDocPreviewRequestCounter
    ).toString(36)}`;
    this.linkedDocPreviewRequests.set(requestId, { doc, generation, relativePath });
    this.onRequestMentionPreview({
      type: 'requestMentionPreview',
      requestId,
      sourcePath: this.currentPath,
      relativePath,
      mode: 'preview',
      maxCharacters: linkedDocPreviewMaxCharacters,
    });
  }

  private async handleLinkedDocPreviewResult(result: MentionPreviewResult) {
    const pending = this.linkedDocPreviewRequests.get(result.requestId);
    this.linkedDocPreviewRequests.delete(result.requestId);

    if (!pending || pending.generation !== this.loadGeneration) {
      return;
    }

    if (!result.ok) {
      reportDiagnostic(
        'warn',
        `Could not preview linked doc ${pending.relativePath}: ${result.error ?? 'Preview unavailable.'}`
      );
      return;
    }

    try {
      await importBlockSuiteJsonIntoExistingDoc(
        pending.doc,
        result.documentContent ?? '',
        linkedDoc => this.registerLinkedDoc(linkedDoc, pending.generation)
      );
      if (pending.generation === this.loadGeneration && this.currentDoc && !this.loadingDocument) {
        await this.flushHost();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Could not render linked doc preview ${pending.relativePath}: ${message}`);
    }
  }

  private clearLinkedDocState() {
    this.linkedDocPathsByPageId.clear();
    this.linkedDocPreviewRequests.clear();
    this.linkedDocPreviewedPageIds.clear();
    if (this.linkedDocPreviewRefreshTimer !== undefined) {
      window.cancelAnimationFrame(this.linkedDocPreviewRefreshTimer);
      this.linkedDocPreviewRefreshTimer = undefined;
    }
  }

  private bindEditorEvents() {
    document.addEventListener(
      'keydown',
      event => {
        if (this.isEditorInputLocked()) {
          preventReadOnlyKeyboardInput(event);
          this.showLockedInputStatus();
          return;
        }

        if (!this.currentDoc || !this.currentPath) {
          return;
        }

        if (isEditorShortcut(event, 'x')) {
          void this.handleCreateExtractShortcut(event);
          return;
        }

        if (isEditorShortcut(event, 'c', { shift: true })) {
          void this.handleClozeCommand(event);
          return;
        }

        if (isEditorShortcut(event, 'i', { shift: true })) {
          void this.handleImageOcclusionShortcut(event);
          return;
        }

      },
      { capture: true, signal: this.eventAbort.signal }
    );

    document.addEventListener(
      'click',
      event => {
        const reference = getInlineLinkedPageReferenceFromEvent(event);
        if (reference) {
          event.preventDefault();
          event.stopPropagation();
          void this.openLinkedDocReference(reference);
          return;
        }

        const model =
          getCogntLinkedDocOpenIntent(event) ??
          this.getSelectedLinkedDocModelForOpenIntent(event);
        if (!model) {
          return;
        }

        event.preventDefault();
        event.stopPropagation();
        void this.openLinkedDocFromModel(model);
      },
      { capture: true, signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'paste',
      event => {
        if (!this.isEditorEvent(event) || !this.hasClipboardFiles(event)) {
          return;
        }

        this.cancelNativeFileEvent(event);
        void this.handlePastedFiles(event);
      },
      { capture: true, signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'dragover',
      event => this.handleFileDragOver(event),
      { capture: true, signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'drop',
      event => {
        if (!this.isEditorEvent(event) || !this.hasDroppedFiles(event)) {
          return;
        }

        this.cancelNativeFileEvent(event);
        void this.handleDroppedFiles(event);
      },
      { capture: true, signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'dblclick',
      event => {
        const reference = getInlineLinkedPageReferenceFromEvent(event);
        if (reference) {
          event.preventDefault();
          event.stopPropagation();
          void this.openLinkedDocReference(reference);
          return;
        }

        const model =
          getCogntLinkedDocOpenIntent(event) ??
          this.getSelectedLinkedDocModelForOpenIntent(event);
        if (!model) {
          return;
        }

        event.preventDefault();
        event.stopPropagation();
        void this.openLinkedDocFromModel(model);
      },
      { capture: true, signal: this.eventAbort.signal }
    );

    this.editor.addEventListener(
      'beforeinput',
      event => {
        if (this.suppressNextNativeCut && event.inputType === 'deleteByCut') {
          event.preventDefault();
          this.suppressNextNativeCut = false;
          return;
        }

        if (this.isEditorInputLocked()) {
          event.preventDefault();
          this.showLockedInputStatus();
        }
      },
      { capture: true, signal: this.eventAbort.signal }
    );

    document.addEventListener('selectionchange', () => {
      if (this.currentDoc) {
        this.mentions.scheduleHighlightUpdate();
      }
    }, { signal: this.eventAbort.signal });

    this.editor.addEventListener('input', () => {
      if (this.isEditorInputLocked()) {
        return;
      }

      this.userEditGeneration++;
      this.mentions.scheduleHighlightUpdate();
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('keyup', () => {
      if (this.isEditorInputLocked()) {
        return;
      }

      this.mentions.scheduleHighlightUpdate();
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('paste', event => {
      if (this.isEditorInputLocked()) {
        event.preventDefault();
        this.showLockedInputStatus();
        return;
      }

      if (this.hasClipboardFiles(event)) {
        this.cancelNativeFileEvent(event);
        void this.handlePastedFiles(event);
        return;
      }

      this.userEditGeneration++;
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('cut', event => {
      if (this.suppressNextNativeCut) {
        event.preventDefault();
        this.suppressNextNativeCut = false;
        return;
      }

      if (this.isEditorInputLocked()) {
        event.preventDefault();
        this.showLockedInputStatus();
      }
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('drop', event => {
      if (this.isEditorInputLocked() || this.hasDroppedFiles(event)) {
        this.cancelNativeFileEvent(event);
        if (this.isEditorInputLocked()) {
          this.showLockedInputStatus();
        }
      }
    }, { signal: this.eventAbort.signal });
  }

  private hasDroppedFiles(event: DragEvent) {
    const dataTransfer = event.dataTransfer;
    if (!dataTransfer) {
      return false;
    }

    return Array.from(dataTransfer.types ?? []).includes('Files')
      || dataTransfer.files.length > 0;
  }

  private hasClipboardFiles(event: ClipboardEvent) {
    const clipboardData = event.clipboardData;
    if (!clipboardData) {
      return false;
    }

    if (clipboardData.files.length > 0) {
      return true;
    }

    return Array.from(clipboardData.items ?? []).some(item => item.kind === 'file');
  }

  private handleFileDragOver(event: DragEvent) {
    if (!this.hasDroppedFiles(event)) {
      return;
    }

    this.cancelNativeFileEvent(event);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = this.currentPath && !this.isEditorInputLocked()
        ? 'copy'
        : 'none';
    }
  }

  private async handleDroppedFiles(event: DragEvent) {
    this.cancelNativeFileEvent(event);

    if (this.isEditorInputLocked()) {
      this.showLockedInputStatus();
      return;
    }

    if (!this.currentDoc || !this.currentPath) {
      this.showStatus('Open a note before attaching files.');
      return;
    }

    const files = Array.from(event.dataTransfer?.files ?? [])
      .filter(file => file.size > 0);
    if (files.length === 0) {
      return;
    }

    await this.attachFiles(
      files,
      file =>
        this.insertDroppedAttachmentReference(
          event,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64),
      file =>
        this.insertDroppedAttachmentReference(
          event,
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64,
          false));
  }

  private async handlePastedFiles(event: ClipboardEvent) {
    this.cancelNativeFileEvent(event);

    if (this.isEditorInputLocked()) {
      this.showLockedInputStatus();
      return;
    }

    if (!this.currentDoc || !this.currentPath) {
      this.showStatus('Open a note before attaching files.');
      return;
    }

    const files = getClipboardFiles(event.clipboardData);
    if (files.length === 0) {
      return;
    }

    await this.attachFiles(
      files,
      file =>
        this.insertAttachmentReferenceAtCurrentSelection(
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64),
      file =>
        this.insertAttachmentReferenceAtCurrentSelection(
          file.result,
          file.fallbackName,
          file.sourceFile,
          file.contentBase64,
          false));
  }

  private isEditorEvent(event: Event) {
    return event.composedPath().includes(this.editor);
  }

  private cancelNativeFileEvent(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    if (typeof event.stopImmediatePropagation === 'function') {
      event.stopImmediatePropagation();
    }
  }

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
    this.blockUpdatedDisposable = doc.slots.blockUpdated.on(() => {
      this.scheduleContentChanged();
      this.scheduleLinkedDocPreviewRefresh();
    });
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
    if (this.linkedDocPreviewRefreshTimer !== undefined) {
      return;
    }

    this.linkedDocPreviewRefreshTimer = window.requestAnimationFrame(() => {
      this.linkedDocPreviewRefreshTimer = undefined;
      this.refreshLinkedDocPreviews();
    });
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
      this.scheduleContentChanged();
    }
  }

  private async flushHost() {
    await flushEditorHost(this.editor);
  }

  private async exportCurrentContent() {
    this.documentLinks.ensureReferencesBlock();
    return exportDocContent(this.currentDoc, this.currentPath, this.currentCollectionRoot);
  }

  private scheduleContentChanged() {
    if (
      this.loadingDocument
      || this.isEditorInputLocked()
      || !this.currentDoc
      || this.pendingAttachmentPreviews > 0
    ) {
      return;
    }

    window.clearTimeout(this.saveTimer);
    this.saveTimer = window.setTimeout(async () => {
      this.saveTimer = undefined;
      this.mentions.refreshHighlights();
      const content = await this.exportCurrentContent();
      this.onContentChanged(content);
    }, saveDebounceMs);
  }

  private cancelPendingContentChanged() {
    window.clearTimeout(this.saveTimer);
    this.saveTimer = undefined;
  }

  private async handleCreateExtractShortcut(event: KeyboardEvent) {
    this.suppressNextNativeCut = true;
    this.pendingExtract = runCreateExtractCommand(
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
