import {
  type AffineSlashMenuContext,
} from '@blocksuite/blocks';
import {
  REFERENCE_NODE,
  RefNodeSlotsProvider,
} from '@blocksuite/affine-components/rich-text';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { type PageEditor } from '@blocksuite/presets';
import {
  type BlockSnapshot,
  type Doc,
  type DocSnapshot,
} from '@blocksuite/store';
import { defineCogntPropertiesBlockElement } from './blocks/properties';
import { updateClozeHighlights, type TextSelection } from './cloze';
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
  createBlockId,
  getSnapshotTextDelta,
  getSnapshotTextValue,
  isBlockSuiteJsonPath,
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
  setDocReadonly,
} from './editor/host-operations';
import {
  isEditorShortcut,
  preventReadOnlyKeyboardInput,
} from './editor/keyboard';
import {
  createEditorSpecs,
  patchBlockStdRender,
} from './editor/specs';
import {
  formatElapsed,
  logEditorTiming,
} from './editor/timing';
import { reportDiagnostic } from './host';
import {
  attachCogntPathToInlineReference,
  getCogntLinkedDocOpenIntent,
  getCogntLinkedDocToolbarModel,
  getCogntLinkedNotePathFromModel,
  getInlineLinkedPageReference,
  getInlineLinkedPageReferenceElementFromEvent,
  getInlineLinkedPageReferenceFromEvent,
  getLinkedDocReferenceFromModel,
  getExtractTitleFromRelPath,
  getLinkedNoteTitle,
  getLinkedNotePathFromResult,
  getLinkedNoteTitleFromResult,
  getLinkedPageReferencePath,
  getLinkedPageReferenceTitle,
  isCogntLinkedDocModel,
  isOpenThisDocToolbarIntent,
  normalizeCreatedExtractPath,
  normalizeLinkedNotePathKey,
  registerSerializedCogntEmbedLinkedNotes,
  registerSerializedCogntInlineLinkedNotes,
  replaceExtractSelectionWithLinkedPage,
  type LinkedPageReference,
} from './links/linked-notes';
import {
  MentionController,
  type OpenMentionMessage,
  type RequestMentionPreviewMessage,
  type SearchMentionTargetsMessage,
} from './mention-controller';
import { parseNoteMentionTarget, type MentionPreviewResult, type MentionTarget } from './mentions';

const saveDebounceMs = 500;
const statusHideMs = 2600;
const linkedDocPreviewMaxCharacters = 4000;
const cogntLinkedNotePathProp = 'cogntLinkedNotePath';
const editorsByDoc = new WeakMap<Doc, MarkdownBlockSuiteEditor>();

defineCogntPropertiesBlockElement();
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

export type MarkdownEditorOptions = {
  hostElement: HTMLElement;
  statusElement: HTMLElement;
  mentionPreviewElement: HTMLElement;
  onContentChanged(content: string): void;
  onCreateExtract(message: CreateExtractCommandMessage): void;
  onSaveCloze(message: SaveClozeCommandMessage): void;
  onSearchMentionTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  onRequestMentionPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
  onCreateLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
};

export type LoadMarkdownOptions = {
  loadId?: number;
  path?: string;
  title?: string;
  content?: string;
};

export class MarkdownBlockSuiteEditor {
  private readonly eventAbort = new AbortController();
  private readonly editor: PageEditor;
  private readonly statusElement: HTMLElement;
  private readonly mentions: MentionController;
  private readonly onContentChanged: (content: string) => void;
  private readonly onCreateExtract: (message: CreateExtractCommandMessage) => void;
  private readonly onSaveCloze: (message: SaveClozeCommandMessage) => void;
  private readonly onRequestMentionPreview: (message: RequestMentionPreviewMessage) => void;
  private readonly onOpenMention: (message: OpenMentionMessage) => void;
  private readonly onCreateLinkedNote: (
    message: CreateLinkedNoteMessage
  ) => Promise<CreateLinkedNoteResult>;
  private currentDoc: Doc | null = null;
  private currentPath = '';
  private loadingDocument = false;
  private readOnlyMode = false;
  private saveTimer: number | undefined;
  private statusTimer: number | undefined;
  private clozeHighlightFrame: number | undefined;
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
  private loadGeneration = 0;
  private userEditGeneration = 0;
  private lockedInputStatusShownAt = 0;

  public constructor(options: MarkdownEditorOptions) {
    this.statusElement = options.statusElement;
    this.onContentChanged = options.onContentChanged;
    this.onCreateExtract = options.onCreateExtract;
    this.onSaveCloze = options.onSaveCloze;
    this.onRequestMentionPreview = options.onRequestMentionPreview;
    this.onOpenMention = options.onOpenMention;
    this.onCreateLinkedNote = options.onCreateLinkedNote;
    this.editor = document.createElement('page-editor') as PageEditor;
    this.editor.id = 'editor';
    this.editor.doc = createEmptyMarkdownDoc();
    this.trackCurrentDoc(this.editor.doc);
    this.editor.hasViewport = true;
    this.mentions = new MentionController({
      editor: this.editor,
      previewElement: options.mentionPreviewElement,
      getCurrentPath: () => this.currentPath,
      exportContent: () => this.exportCurrentContent(),
      flushHost: () => this.flushHost(),
      scheduleContentChanged: () => this.scheduleContentChanged(),
      showStatus: message => this.showStatus(message),
      onSearchTargets: async message => {
        const targets = await options.onSearchMentionTargets(message);
        this.mergeMentionTargetTitles(targets, false);
        return targets;
      },
      onRequestPreview: options.onRequestMentionPreview,
      onOpenMention: options.onOpenMention,
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
    this.cancelScheduledClozeHighlightUpdate();
    this.mentions.cancelScheduledHighlightUpdate();
    this.mentions.hidePreview();
    this.currentPath = options.path ?? '';
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

      const clozeStartedAt = performance.now();
      this.refreshClozeHighlights();
      logEditorTiming('cloze highlight refresh', clozeStartedAt);

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


  public async loadCard(content: string) {
    ++this.loadGeneration;
    this.loadingDocument = true;
    this.beginCardRender();

    try {
      const nextDoc = await createDocFromBlockSuiteJson(content);
      this.trackCurrentDoc(nextDoc);
      this.editor.doc = nextDoc;
      this.editor.hasViewport = true;
      this.bindDocChangeEvents(nextDoc);
      await this.flushHost();
      this.refreshClozeHighlights();
      reportDiagnostic('info', `Rendered review card in shared editor (${content.length} characters).`);
      this.editor.classList.remove('card-render-pending');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not render review card: ${message}`);
      throw error;
    } finally {
      this.loadingDocument = false;
    }
  }

  public beginCardRender() {
    ++this.loadGeneration;
    this.readOnlyMode = true;
    document.body.dataset.mode = 'review';
    this.editor.classList.add('review-renderer', 'card-render-pending');
    window.clearTimeout(this.saveTimer);
    this.cancelScheduledClozeHighlightUpdate();
    this.mentions.cancelScheduledHighlightUpdate();
    this.mentions.hidePreview();
    this.currentPath = '';
    this.clearLinkedDocState();
  }

  private enableEditorMode() {
    this.readOnlyMode = false;
    document.body.dataset.mode = 'editor';
    this.editor.classList.remove('review-renderer', 'card-render-pending');
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
      const linkedDoc = replaceExtractSelectionWithLinkedPage(
        this.currentDoc,
        pending.selection,
        relativePath,
        title
      );
      this.trackLinkedDoc(linkedDoc, relativePath);

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
      return;
    }

    this.mentions.handlePreviewResult(result);
  }

  public handleMentionOpenResult(ok: boolean, error?: string) {
    this.mentions.handleOpenResult(ok, error);
  }

  public dispose() {
    window.clearTimeout(this.saveTimer);
    window.clearTimeout(this.statusTimer);
    this.cancelScheduledClozeHighlightUpdate();
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
      refNodeSlotsProvider.docLinkClicked.on(reference => {
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

    this.mentions.hidePreview();
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

        if (isEditorShortcut(event, 'z')) {
          void this.handleClozeShortcut(event);
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

    this.editor.addEventListener(
      'beforeinput',
      event => {
        if (this.isEditorInputLocked()) {
          event.preventDefault();
          this.showLockedInputStatus();
        }
      },
      { capture: true, signal: this.eventAbort.signal }
    );

    document.addEventListener('selectionchange', () => {
      if (this.currentDoc) {
        this.scheduleClozeHighlightUpdate();
        this.mentions.scheduleHighlightUpdate();
      }
    }, { signal: this.eventAbort.signal });

    this.editor.addEventListener('input', () => {
      if (this.isEditorInputLocked()) {
        return;
      }

      this.userEditGeneration++;
      this.scheduleClozeHighlightUpdate();
      this.mentions.scheduleHighlightUpdate();
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('keyup', () => {
      if (this.isEditorInputLocked()) {
        return;
      }

      this.scheduleClozeHighlightUpdate();
      this.mentions.scheduleHighlightUpdate();
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('click', event => {
      void this.mentions.handleClick(event);
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener(
      'mousemove',
      event => {
        const referenceElement = getInlineLinkedPageReferenceElementFromEvent(event);
        const reference = referenceElement
          ? getInlineLinkedPageReference(referenceElement)
          : null;
        const relativePath = reference ? this.getLinkedPageReferencePath(reference) : null;
        if (relativePath && referenceElement) {
          this.mentions.handleLinkedPageHover(relativePath, referenceElement);
          return;
        }

        this.mentions.handleHoverMiss();
      },
      { signal: this.eventAbort.signal }
    );
    this.editor.addEventListener('mouseleave', event => {
      const target = event.relatedTarget as Node | null;
      if (target && this.mentions.containsFloatingUi(target)) {
        return;
      }

      if (this.mentions.isShowingPreview()) {
        this.mentions.schedulePreviewHide();
      }
    }, { signal: this.eventAbort.signal });
    this.editor.addEventListener('paste', event => {
      if (this.isEditorInputLocked()) {
        event.preventDefault();
        this.showLockedInputStatus();
        return;
      }

      this.userEditGeneration++;
      this.scheduleContentChanged();
    }, { signal: this.eventAbort.signal });

    document.addEventListener('mousedown', event => {
      const target = event.target as Node | null;
      if (target && this.mentions.containsFloatingUi(target)) {
        return;
      }

      if (this.mentions.isShowingPreview()) {
        this.mentions.hidePreview();
      }
    }, { signal: this.eventAbort.signal });
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

    const changed = registerSerializedCogntEmbedLinkedNotes(
      this.currentDoc,
      linkedDoc => this.registerLinkedDoc(linkedDoc, this.loadGeneration),
      relativePath => this.resolveLinkedNoteTitle(relativePath)
    );

    if (changed) {
      this.scheduleContentChanged();
    }
  }

  private async flushHost() {
    await flushEditorHost(this.editor);
  }

  private async exportCurrentContent() {
    return exportDocContent(this.currentDoc, this.currentPath);
  }

  private scheduleContentChanged() {
    if (this.loadingDocument || this.isEditorInputLocked() || !this.currentDoc) {
      return;
    }

    window.clearTimeout(this.saveTimer);
    this.saveTimer = window.setTimeout(async () => {
      this.saveTimer = undefined;
      this.refreshClozeHighlights();
      this.mentions.refreshHighlights();
      const content = await this.exportCurrentContent();
      this.onContentChanged(content);
    }, saveDebounceMs);
  }

  private cancelPendingContentChanged() {
    window.clearTimeout(this.saveTimer);
    this.saveTimer = undefined;
  }

  private scheduleClozeHighlightUpdate() {
    if (this.clozeHighlightFrame !== undefined) {
      return;
    }

    this.clozeHighlightFrame = window.requestAnimationFrame(() => {
      this.clozeHighlightFrame = undefined;
      if (this.currentDoc) {
        updateClozeHighlights(this.editor);
      }
    });
  }

  private refreshClozeHighlights() {
    this.cancelScheduledClozeHighlightUpdate();
    updateClozeHighlights(this.editor);
  }

  private cancelScheduledClozeHighlightUpdate() {
    if (this.clozeHighlightFrame === undefined) {
      return;
    }

    window.cancelAnimationFrame(this.clozeHighlightFrame);
    this.clozeHighlightFrame = undefined;
  }

  private async handleCreateExtractShortcut(event: KeyboardEvent) {
    this.pendingExtract = runCreateExtractCommand(
      {
        isInputLocked: () => this.isEditorInputLocked(),
        getCurrentDoc: () => this.currentDoc,
        getCurrentPath: () => this.currentPath,
        getLoadGeneration: () => this.loadGeneration,
        getEditor: () => this.editor,
        getTextSelection: () => this.getTextSelection(),
        createExtract: message => this.onCreateExtract(message),
        showStatus: message => this.showStatus(message),
      },
      event
    );
  }

  private async handleClozeShortcut(event: KeyboardEvent) {
    await runClozeCommand(
      {
        isInputLocked: () => this.isEditorInputLocked(),
        getCurrentDoc: () => this.currentDoc,
        getCurrentPath: () => this.currentPath,
        getTextSelection: () => this.getTextSelection(),
        clearTextSelection: () => {
          this.editor.host?.selection.clear(['text']);
          window.getSelection()?.removeAllRanges();
        },
        flushHost: () => this.flushHost(),
        refreshClozeHighlights: () => this.refreshClozeHighlights(),
        cancelPendingContentChanged: () => this.cancelPendingContentChanged(),
        exportContent: () => this.exportCurrentContent(),
        saveCloze: message => this.onSaveCloze(message),
        showStatus: message => this.showStatus(message),
      },
      event
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

}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function upgradeSerializedRawCogntInlineMentionSnapshots(snapshot: DocSnapshot) {
  upgradeBlockSnapshotRawCogntInlineMentions(snapshot.blocks);
}

function upgradeBlockSnapshotRawCogntInlineMentions(snapshot: BlockSnapshot) {
  const textValue = getSnapshotTextValue(snapshot);
  const delta = textValue ? getSnapshotTextDelta(textValue) : [];
  const upgradedDelta = upgradeRawCogntInlineMentionDelta(delta);

  if (upgradedDelta !== delta && textValue) {
    snapshot.props = {
      ...snapshot.props,
      text: {
        ...textValue,
        delta: upgradedDelta,
      },
    };
  }

  snapshot.children.forEach(upgradeBlockSnapshotRawCogntInlineMentions);
}

function upgradeRawCogntInlineMentionDelta(delta: unknown[]) {
  let changed = false;
  const upgraded: unknown[] = [];

  for (const entry of delta) {
    if (!isRecord(entry) || typeof entry.insert !== 'string' || !entry.insert.includes('[[')) {
      upgraded.push(entry);
      continue;
    }

    const segments = splitRawCogntInlineMentionEntry(entry as Record<string, unknown>);
    if (segments === null) {
      upgraded.push(entry);
      continue;
    }

    changed = true;
    upgraded.push(...segments);
  }

  return changed ? upgraded : delta;
}

function splitRawCogntInlineMentionEntry(entry: Record<string, unknown>) {
  const insert = entry.insert;
  if (typeof insert !== 'string') {
    return null;
  }

  const expression = /\[\[([^\]\[\n]+)\]\]/g;
  const segments: unknown[] = [];
  let changed = false;
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = expression.exec(insert)) !== null) {
    const target = parseNoteMentionTarget(match[1]);
    if (!target || !isCogntNoteRelativePath(target.relativePath)) {
      continue;
    }

    if (match.index > lastIndex) {
      segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex, match.index)));
    }

    segments.push({
      insert: REFERENCE_NODE,
      attributes: {
        reference: createLinkedPageReference(
          createBlockId(),
          target.relativePath,
          getLinkedNoteTitle(target.relativePath)
        ),
      },
    });

    changed = true;
    lastIndex = match.index + match[0].length;
  }

  if (!changed) {
    return null;
  }

  if (lastIndex < insert.length) {
    segments.push(createTextDeltaSegment(entry, insert.slice(lastIndex)));
  }

  return segments;
}

function createTextDeltaSegment(source: Record<string, unknown>, insert: string) {
  const segment: Record<string, unknown> = { insert };
  if (isRecord(source.attributes)) {
    segment.attributes = source.attributes;
  }

  return segment;
}

function createLinkedPageReference(pageId: string, relativePath: string, title: string) {
  return {
    type: 'LinkedPage',
    pageId,
    params: createCogntLinkedNoteParams(relativePath),
    title,
    description: relativePath,
    [cogntLinkedNotePathProp]: relativePath,
  };
}

function isCogntNoteRelativePath(relativePath: string) {
  return /\.(json|md|markdown)$/i.test(relativePath);
}

function getCogntLinkedNotePath(props: Record<string, unknown>) {
  const value = props[cogntLinkedNotePathProp];
  const directPath = typeof value === 'string' && value.trim().length > 0
    ? value.trim()
    : null;
  if (directPath) {
    return directPath;
  }

  const params = getRecordProperty(props, 'params');
  const paramPath = params
    ? getTrimmedString(params[cogntLinkedNotePathProp])
      ?? getTrimmedString(params.relativePath)
    : null;
  if (paramPath) {
    return paramPath;
  }

  const legacyPath =
    getCogntLinkedNoteLegacyPath(props.relativePath)
    ?? getCogntLinkedNoteLegacyPath(props.description);

  return legacyPath;
}

function getTrimmedString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

function getCogntLinkedNoteLegacyPath(value: unknown) {
  const path = getTrimmedString(value);
  return path && isCogntNoteRelativePath(path) ? path : null;
}

function createCogntLinkedNoteParams(relativePath: string) {
  return {
    [cogntLinkedNotePathProp]: relativePath,
  };
}

function mergeCogntLinkedNoteParams(params: unknown, relativePath: string) {
  return {
    ...(isRecord(params) ? params : {}),
    [cogntLinkedNotePathProp]: relativePath,
  };
}

function hasCogntLinkedNotePath(params: unknown, relativePath: string) {
  return isRecord(params)
    && getTrimmedString(params[cogntLinkedNotePathProp]) === relativePath;
}

function getRecordProperty(record: Record<string, unknown>, key: string) {
  const value = record[key];
  return isRecord(value) ? value : null;
}

