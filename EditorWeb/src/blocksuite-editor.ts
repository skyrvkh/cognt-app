import {
  createDefaultDoc,
  type AffineSlashMenuContext,
} from '@blocksuite/blocks';
import {
  getInlineEditorByModel,
  REFERENCE_NODE,
  RefNodeSlotsProvider,
} from '@blocksuite/affine-components/rich-text';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { type PageEditor } from '@blocksuite/presets';
import {
  type BlockModel,
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
  getTextSelectionSnapshotRange,
  type TextSelectionSnapshot,
} from './documents/selection-export';
import {
  configureCogntSlashMenu,
  type LinkedNoteViewMode,
} from './editor/slash-menu';
import {
  findEditableElement,
  findElementsDeep,
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
const embedLinkedDocTagName = 'affine-embed-linked-doc-block';
const cogntLinkedDocWrapStyleId = 'cognt-linked-doc-wrap-style';
const defaultCogntEmbedStyle = 'horizontal';
const cogntEmbedStyleAliases = new Map([
  ['horizontal', 'horizontal'],
  ['horizontalthin', 'horizontalThin'],
  ['list', 'list'],
  ['vertical', 'vertical'],
  ['cube', 'cube'],
]);
const editorsByDoc = new WeakMap<Doc, MarkdownBlockSuiteEditor>();

type CogntLinkedDoc = {
  doc: Doc;
  relativePath: string;
};

type LinkedNoteTitleResolver = (relativePath: string) => string | null | undefined;

type LinkedPageReference = {
  pageId?: string;
  relativePath?: string;
  description?: string;
  cogntLinkedNotePath?: string;
  params?: Record<string, unknown>;
  title?: string;
  type?: string;
};

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
    if (this.isEditorInputLocked()) {
      this.showStatus('Wait for the full note to finish loading before creating linked notes.');
      return;
    }

    if (!this.currentDoc || !this.currentPath) {
      this.showStatus('Open a note before creating a linked note.');
      return;
    }

    try {
      const result = await this.onCreateLinkedNote({
        type: 'createLinkedNote',
        sourcePath: this.currentPath,
        viewMode,
      });

      if (!result.ok) {
        throw new Error(result.error ?? 'The linked note could not be created.');
      }

      const linkedNotePath = getLinkedNotePathFromResult(result);
      if (!linkedNotePath) {
        throw new Error('The linked note path was not returned.');
      }

      if (viewMode === 'embed') {
        const linkedDoc = insertCogntLinkedNoteEmbed(
          context,
          linkedNotePath,
          getLinkedNoteTitleFromResult(result)
        );
        this.trackLinkedDoc(linkedDoc, linkedNotePath);

        if (typeof result.documentContent === 'string') {
          await importBlockSuiteJsonIntoExistingDoc(
            linkedDoc,
            result.documentContent,
            nestedLinkedDoc => this.registerLinkedDoc(nestedLinkedDoc, this.loadGeneration)
          );
          this.markLinkedDocPreviewed(linkedDoc);
        } else {
          this.requestLinkedDocPreview(linkedDoc, linkedNotePath, this.loadGeneration);
        }
      } else {
        const linkedDoc = insertCogntLinkedNoteInline(
          context,
          linkedNotePath,
          getLinkedNoteTitleFromResult(result)
        );
        this.trackLinkedDoc(linkedDoc, linkedNotePath);
      }

      await this.flushHost();
      this.mentions.refreshHighlights();
      this.cancelPendingContentChanged();
      this.userEditGeneration++;
      this.onContentChanged(await this.exportCurrentContent());
      this.showStatus(`Created ${result.relativePath ?? 'linked note'}.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not create linked note: ${message}`);
      this.showStatus(`Create note failed: ${message}`);
    }
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

function getInlineLinkedPageReferenceFromEvent(event: MouseEvent): LinkedPageReference | null {
  const element = getInlineLinkedPageReferenceElementFromEvent(event);
  return element ? getInlineLinkedPageReference(element) : null;
}

function getInlineLinkedPageReferenceElementFromEvent(
  event: MouseEvent
): (HTMLElement & {
  referenceInfo?: unknown;
  delta?: { attributes?: { reference?: unknown } };
}) | null {
  return event.composedPath().find(isInlineLinkedPageReferenceElement) ?? null;
}

function getInlineLinkedPageReference(
  element: HTMLElement & {
    referenceInfo?: unknown;
    delta?: { attributes?: { reference?: unknown } };
  }
): LinkedPageReference | null {
  const reference = element?.referenceInfo ?? element?.delta?.attributes?.reference;

  return isLinkedPageReference(reference) ? reference : null;
}

function isInlineLinkedPageReferenceElement(
  value: EventTarget
): value is HTMLElement & {
  referenceInfo?: unknown;
  delta?: { attributes?: { reference?: unknown } };
} {
  return value instanceof HTMLElement && value.localName === 'affine-reference';
}

function isLinkedPageReference(value: unknown): value is LinkedPageReference {
  return typeof value === 'object'
    && value !== null
    && (value as { pageId?: unknown }).pageId !== undefined
    && typeof (value as { pageId?: unknown }).pageId === 'string';
}

function getCogntLinkedDocOpenIntent(event: MouseEvent) {
  const path = event.composedPath();
  const block = path.find(isEmbedLinkedDocBlockElement) as
    | (HTMLElement & { model?: unknown })
    | undefined;

  if (!block || !path.some(isLinkedDocCardOpenTarget)) {
    return null;
  }

  return isCogntLinkedDocModel(block.model)
    && getCogntLinkedNotePathFromModel(block.model)
    ? block.model
    : null;
}

function isOpenThisDocToolbarIntent(event: MouseEvent) {
  return event.composedPath().some(isOpenThisDocActionElement);
}

function getCogntLinkedDocToolbarModel(event: MouseEvent) {
  for (const target of event.composedPath()) {
    if (typeof target !== 'object' || target === null) {
      continue;
    }

    const candidate = target as { focusModel?: unknown; model?: unknown };
    const model = candidate.focusModel ?? candidate.model;
    if (isCogntLinkedDocModel(model) && getCogntLinkedNotePathFromModel(model)) {
      return model;
    }
  }

  return null;
}

function isEmbedLinkedDocBlockElement(value: EventTarget) {
  return value instanceof HTMLElement && value.localName === embedLinkedDocTagName;
}

function isLinkedDocCardOpenTarget(value: EventTarget) {
  return value instanceof HTMLElement
    && (value.classList.contains('affine-embed-linked-doc-content-title-icon')
      || value.classList.contains('affine-embed-linked-doc-content-title-text'));
}

function isOpenThisDocActionElement(value: EventTarget) {
  if (!(value instanceof HTMLElement)) {
    return false;
  }

  const ariaLabel = value.getAttribute('aria-label')?.trim().toLowerCase();
  const actionType = value.getAttribute('type')?.trim().toLowerCase();

  return ariaLabel === 'open this doc'
    || actionType === 'open-this-doc'
    || actionType === 'open';
}

function getLinkedDocReferenceFromModel(model: unknown) {
  if (!isCogntLinkedDocModel(model)) {
    return null;
  }

  const linkedDocModel = model as Record<string, unknown> & { pageId: string };

  return {
    pageId: linkedDocModel.pageId,
    relativePath: getCogntLinkedNotePathFromModel(linkedDocModel) ?? undefined,
  };
}

function isCogntLinkedDocModel(model: unknown) {
  return typeof model === 'object'
    && model !== null
    && (model as { flavour?: unknown }).flavour === 'affine:embed-linked-doc'
    && typeof (model as { pageId?: unknown }).pageId === 'string';
}

function getCogntLinkedNotePathFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  return getCogntLinkedNotePath(record) ?? getCogntLinkedNotePath(props);
}

function getLinkedNoteTitleFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  return getCogntLinkedNoteTitle(record) ?? getCogntLinkedNoteTitle(props);
}

function patchRenderedEmbedLinkedDocBlocks(root: ParentNode | ShadowRoot) {
  for (const block of findElementsDeep(root, embedLinkedDocTagName)) {
    injectShadowStyle(block, linkedDocCardWrapCss());

    for (const child of findElementsDeep(
      block,
      'affine-paragraph, affine-list, rich-text, .inline-editor'
    )) {
      injectShadowStyle(child, linkedDocNestedWrapCss());
    }
  }
}

function injectShadowStyle(element: HTMLElement, cssText: string) {
  const root = element.shadowRoot;
  if (!root || root.getElementById(cogntLinkedDocWrapStyleId)) {
    return;
  }

  const style = document.createElement('style');
  style.id = cogntLinkedDocWrapStyleId;
  style.textContent = cssText;
  root.prepend(style);
}

function linkedDocCardWrapCss() {
  return `
    .affine-embed-linked-doc-content,
    .affine-embed-linked-doc-content-note,
    .affine-embed-doc-content-note-blocks,
    .affine-embed-doc-content-note-blocks * {
      min-width: 0 !important;
      max-width: 100% !important;
      overflow-wrap: anywhere !important;
      word-break: break-word !important;
      white-space: normal !important;
    }

    .affine-embed-linked-doc-content-note.render {
      overflow: hidden !important;
    }
  `;
}

function linkedDocNestedWrapCss() {
  return `
    :host,
    *,
    .inline-editor,
    rich-text {
      min-width: 0 !important;
      max-width: 100% !important;
      overflow-wrap: anywhere !important;
      word-break: break-word !important;
      white-space: normal !important;
    }
  `;
}

function getLinkedNotePathFromResult(result: CreateLinkedNoteResult) {
  if (result.relativePath?.trim()) {
    return result.relativePath.trim();
  }

  return null;
}

function getLinkedNoteTitleFromResult(result: CreateLinkedNoteResult) {
  const title = normalizeLinkedNoteTitle(result.title);
  return title;
}

function normalizeCreatedExtractPath(path: string | null | undefined) {
  const value = path?.trim().replace(/\\/g, '/');
  if (!value) {
    return null;
  }

  const filesIndex = value.toLowerCase().lastIndexOf('/files/');
  if (filesIndex >= 0) {
    return value.slice(filesIndex + 1);
  }

  return value;
}

function getExtractTitleFromRelPath(relativePath: string) {
  const fileName = relativePath.split(/[\\/]/).filter(Boolean).pop() ?? relativePath;
  const stem = fileName.replace(/\.(json|md|markdown)$/i, '').trim();
  const separatorIndex = stem.lastIndexOf('.');

  return separatorIndex >= 0 && separatorIndex < stem.length - 1
    ? stem.slice(separatorIndex + 1)
    : stem;
}

function extractCogntEmbedTarget(markdown: string | undefined) {
  const match = markdown?.trim().match(/^!\[\[([^\]\n]+)\]\]$/);
  return parseCogntLinkTarget(match?.[1]);
}

function parseCogntLinkTarget(value: string | undefined) {
  if (!value) {
    return null;
  }

  const [relativePath, ...parts] = value.split('|');
  const normalizedPath = relativePath.trim();
  if (!normalizedPath) {
    return null;
  }

  const titleParts: string[] = [];
  let style: string | null = null;

  for (const part of parts) {
    const optionMatch = part.trim().match(/^style\s*=\s*(.+)$/i);
    if (optionMatch) {
      style = normalizeCogntEmbedStyle(optionMatch[1]) ?? style;
      continue;
    }

    titleParts.push(part);
  }

  return {
    relativePath: normalizedPath,
    title: normalizeLinkedNoteTitle(titleParts.join('|')),
    style,
  };
}

function getLinkedNoteTitle(relativePath: string, title?: string | null) {
  const normalizedTitle = normalizeLinkedNoteTitle(title);
  if (normalizedTitle
    && normalizedTitle !== relativePath
    && !isCogntNoteRelativePath(normalizedTitle)) {
    return normalizedTitle;
  }

  const fileName = relativePath.split(/[\\/]/).filter(Boolean).pop() ?? relativePath;
  const fallbackTitle = fileName.replace(/\.(json|md|markdown)$/i, '').trim();
  return fallbackTitle || 'Untitled';
}

function normalizeLinkedNotePathKey(relativePath: string) {
  return relativePath.replace(/\\/g, '/').trim().toLowerCase();
}

function normalizeLinkedNoteTitle(title: string | null | undefined) {
  return title?.replace(/[|\]\r\n]+/g, ' ').replace(/\s+/g, ' ').trim() || null;
}

function normalizeCogntEmbedStyle(style: unknown) {
  if (typeof style !== 'string') {
    return null;
  }

  return cogntEmbedStyleAliases.get(style.trim().toLowerCase()) ?? null;
}

function insertCogntLinkedNoteEmbed(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  const { rootComponent, model } = context;
  const parent = rootComponent.doc.getParent(model);

  if (!parent) {
    throw new Error('Could not find the insertion block.');
  }

  const index = parent.children.indexOf(model);
  const title = getLinkedNoteTitle(relativePath, requestedTitle);
  const linkedDoc = createDefaultDoc(rootComponent.doc.collection, { title });
  const blockId = rootComponent.doc.addBlock(
    'affine:embed-linked-doc' as never,
    {
      pageId: linkedDoc.id,
      params: createCogntLinkedNoteParams(relativePath),
      style: defaultCogntEmbedStyle,
      caption: null,
      title,
      description: relativePath,
      [cogntLinkedNotePathProp]: relativePath,
    } as never,
    parent,
    index + 1
  );

  if (model.text?.length === 0 && model.children.length === 0) {
    rootComponent.doc.deleteBlock(model);
  }

  rootComponent.host.selection.setGroup('note', [
    rootComponent.host.selection.create('block', { blockId }),
  ]);

  return linkedDoc;
}

function insertCogntLinkedNoteInline(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  const { rootComponent, model } = context;
  const inlineEditor = getInlineEditorByModel(rootComponent.host, model);

  if (!inlineEditor) {
    throw new Error('Could not find the inline editor for the insertion block.');
  }

  const inlineRange = inlineEditor.getInlineRange();
  if (!inlineRange) {
    throw new Error('Could not find the inline insertion point.');
  }

  const title = getLinkedNoteTitle(relativePath, requestedTitle);
  const linkedDoc = createDefaultDoc(rootComponent.doc.collection, { title });
  const reference = {
    type: 'LinkedPage',
    pageId: linkedDoc.id,
    params: createCogntLinkedNoteParams(relativePath),
    title,
    description: relativePath,
    [cogntLinkedNotePathProp]: relativePath,
  };
  inlineEditor.insertText(inlineRange, REFERENCE_NODE, {
    reference,
  } as never);
  inlineEditor.setInlineRange({
    index: inlineRange.index + REFERENCE_NODE.length,
    length: 0,
  });

  return linkedDoc;
}

function attachCogntPathToInlineReference(
  doc: Doc,
  pageId: string,
  relativePath: string,
  title?: string | null
) {
  for (const model of walkBlockModels(doc.root)) {
    const text = getBlockText(model);
    if (!text) {
      continue;
    }

    let index = 0;
    for (const delta of text.toDelta()) {
      const inserted = typeof delta.insert === 'string' ? delta.insert : '';
      const length = inserted.length;
      const reference = getDeltaReference(delta);

      if (reference?.pageId === pageId) {
        text.format(index, Math.max(length, REFERENCE_NODE.length), {
          reference: {
            ...reference,
            type: 'LinkedPage',
            pageId,
            params: mergeCogntLinkedNoteParams(reference.params, relativePath),
            title: title ?? reference.title,
            description: relativePath,
            [cogntLinkedNotePathProp]: relativePath,
          },
        });
        return true;
      }

      index += length;
    }
  }

  return false;
}

function* walkBlockModels(model: unknown): Generator<unknown> {
  if (!model) {
    return;
  }

  yield model;

  const children = (model as { children?: unknown }).children;
  if (!Array.isArray(children)) {
    return;
  }

  for (const child of children) {
    yield* walkBlockModels(child);
  }
}

function getBlockText(model: unknown) {
  const text = (model as { text?: unknown } | null)?.text;
  if (
    typeof text === 'object'
    && text !== null
    && typeof (text as { toDelta?: unknown }).toDelta === 'function'
    && typeof (text as { format?: unknown }).format === 'function'
  ) {
    return text as {
      toDelta(): Array<{
        insert?: unknown;
        attributes?: Record<string, unknown>;
      }>;
      format(index: number, length: number, format: Record<string, unknown>): void;
      replace(
        index: number,
        length: number,
        content: string,
        attributes?: Record<string, unknown>
      ): void;
    };
  }

  return null;
}

function getDeltaReference(delta: {
  attributes?: Record<string, unknown>;
}) {
  const reference = delta.attributes?.reference;
  if (typeof reference === 'object' && reference !== null) {
    return reference as {
      pageId?: string;
      title?: string;
      description?: string;
      relativePath?: string;
      cogntLinkedNotePath?: string;
      params?: Record<string, unknown>;
      type?: string;
    };
  }

  return null;
}

function replaceExtractSelectionWithLinkedPage(
  doc: Doc,
  selection: TextSelectionSnapshot,
  relativePath: string,
  title: string
) {
  const range = getTextSelectionSnapshotRange(selection);
  if (!range) {
    throw new Error('source replacement supports text selected inside one block');
  }

  const model = doc.getBlockById(range.blockId) as
    | {
        text?: {
          replace(
            index: number,
            length: number,
            content: string,
            attributes?: Record<string, unknown>
          ): void;
        };
      }
    | null;
  if (!model?.text) {
    throw new Error('selected block no longer has editable text');
  }

  const linkedDoc = createDefaultDoc(doc.collection, { title });
  const reference = createLinkedPageReference(linkedDoc.id, relativePath, title);

  doc.transact(() => {
    model.text!.replace(range.index, range.length, REFERENCE_NODE, {
      reference,
    });
  });

  return linkedDoc;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function hydrateCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    return;
  }

  for (const child of [...note.children]) {
    if (child.flavour !== 'affine:paragraph') {
      continue;
    }

    const target = extractCogntEmbedTarget(child.text?.toString());
    if (!target) {
      continue;
    }

    const title = getLinkedNoteTitle(
      target.relativePath,
      target.title ?? resolveTitle?.(target.relativePath)
    );
    const linkedDoc = createDefaultDoc(doc.collection, { title });
    onLinkedDoc?.({ doc: linkedDoc, relativePath: target.relativePath });
    const parent = doc.getParent(child);
    if (!parent) {
      continue;
    }

    const index = parent.children.indexOf(child);
    doc.addBlock(
      'affine:embed-linked-doc' as never,
      {
        pageId: linkedDoc.id,
        params: createCogntLinkedNoteParams(target.relativePath),
        style: target.style ?? defaultCogntEmbedStyle,
        caption: null,
        title,
        description: target.relativePath,
        [cogntLinkedNotePathProp]: target.relativePath,
      } as never,
      parent,
      index
    );
    doc.deleteBlock(child, { deleteChildren: true });
  }
}

function registerSerializedCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  let changed = false;

  for (const model of doc.getBlocks()) {
    if (!isCogntLinkedDocModel(model)) {
      continue;
    }

    const relativePath = getCogntLinkedNotePathFromModel(model);
    if (!relativePath) {
      continue;
    }

    const title = getLinkedNoteTitle(
      relativePath,
      resolveTitle?.(relativePath) ?? getLinkedNoteTitleFromModel(model)
    );
    const modelRecord = model as unknown as Record<string, unknown> & { pageId?: string };
    const existingPageId = getTrimmedString(modelRecord.pageId);
    const linkedDoc = ensureLinkedDocReferenceDoc(doc, existingPageId, title);
    const mergedParams = mergeCogntLinkedNoteParams(
      getRecordProperty(modelRecord, 'params'),
      relativePath
    );
    const needsUpdate =
      modelRecord.pageId !== linkedDoc.id
      || getLinkedNoteTitleFromModel(model) !== title
      || getCogntLinkedNotePathFromModel(model) !== relativePath
      || getTrimmedString(modelRecord.description) !== relativePath
      || !hasCogntLinkedNotePath(modelRecord.params, relativePath);

    if (needsUpdate) {
      doc.updateBlock(model as BlockModel, {
        pageId: linkedDoc.id,
        params: mergedParams,
        title,
        description: relativePath,
        [cogntLinkedNotePathProp]: relativePath,
      });
      changed = true;
    }
    onLinkedDoc?.({ doc: linkedDoc, relativePath });
  }

  return changed;
}

function registerSerializedCogntInlineLinkedNotes(
  doc: Doc,
  resolveTitle?: LinkedNoteTitleResolver
) {
  let changed = false;

  for (const model of walkBlockModels(doc.root)) {
    const text = getBlockText(model);
    if (!text) {
      continue;
    }

    let index = 0;
    for (const delta of text.toDelta()) {
      const inserted = typeof delta.insert === 'string' ? delta.insert : '';
      const length = inserted.length;
      const reference = getDeltaReference(delta);
      const relativePath = reference ? getLinkedPageReferencePath(reference) : null;

      if (reference?.type === 'LinkedPage' && reference.pageId && relativePath) {
        const linkedPage = ensureLinkedPageReferenceDoc(doc, reference, relativePath, resolveTitle);
        const needsUpdate =
          reference.pageId !== linkedPage.pageId
          || getTrimmedString(reference.title) !== linkedPage.title
          || getLinkedPageReferencePath(reference) !== relativePath
          || getTrimmedString(reference.description) !== relativePath
          || !hasCogntLinkedNotePath(reference.params, relativePath);

        if (needsUpdate) {
          text.format(index, Math.max(length, REFERENCE_NODE.length), {
            reference: {
              ...reference,
              type: 'LinkedPage',
              pageId: linkedPage.pageId,
              params: mergeCogntLinkedNoteParams(reference.params, relativePath),
              title: linkedPage.title,
              description: relativePath,
              [cogntLinkedNotePathProp]: relativePath,
            },
          });
          changed = true;
        }
      }

      index += length;
    }
  }

  return changed;
}

function upgradeRuntimeRawCogntInlineMentions(doc: Doc, resolveTitle?: LinkedNoteTitleResolver) {
  for (const model of walkBlockModels(doc.root)) {
    const text = getBlockText(model);
    if (!text) {
      continue;
    }

    const replacements: Array<{
      index: number;
      length: number;
      relativePath: string;
    }> = [];
    let index = 0;

    for (const delta of text.toDelta()) {
      const inserted = typeof delta.insert === 'string' ? delta.insert : '';
      const length = inserted.length;

      if (inserted.includes('[[')) {
        const expression = /\[\[([^\]\[\n]+)\]\]/g;
        let match: RegExpExecArray | null;

        while ((match = expression.exec(inserted)) !== null) {
          const target = parseNoteMentionTarget(match[1]);
          if (!target || !isCogntNoteRelativePath(target.relativePath)) {
            continue;
          }

          replacements.push({
            index: index + match.index,
            length: match[0].length,
            relativePath: target.relativePath,
          });
        }
      }

      index += length;
    }

    for (const replacement of replacements.reverse()) {
      text.replace(
        replacement.index,
        replacement.length,
        REFERENCE_NODE,
        {
          reference: createLinkedPageReference(
            createBlockId(),
            replacement.relativePath,
            getLinkedNoteTitle(replacement.relativePath, resolveTitle?.(replacement.relativePath))
          ),
        }
      );
    }
  }
}

function ensureLinkedPageReferenceDoc(
  doc: Doc,
  reference: Record<string, unknown>,
  relativePath: string,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const title = getLinkedNoteTitle(
    relativePath,
    resolveTitle?.(relativePath) ?? getTrimmedString(reference.title)
  );
  const pageId = getTrimmedString(reference.pageId) ?? createBlockId();

  if (!doc.collection.getDoc(pageId)) {
    createDefaultDoc(doc.collection, { id: pageId, title });
  } else {
    doc.collection.setDocMeta(pageId, { title });
  }

  return { pageId, title };
}

function ensureLinkedDocReferenceDoc(doc: Doc, pageId: string | null, title: string) {
  const existingDoc = pageId ? doc.collection.getDoc(pageId) : null;
  const linkedDoc = existingDoc ?? createDefaultDoc(
    doc.collection,
    pageId ? { id: pageId, title } : { title }
  );

  doc.collection.setDocMeta(linkedDoc.id, { title });
  return linkedDoc;
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

function getLinkedPageReferencePath(reference: LinkedPageReference | Record<string, unknown>) {
  const record = reference as Record<string, unknown>;
  return getCogntLinkedNotePath(record)
    ?? getTrimmedString(record.relativePath)
    ?? getTrimmedString(record.description);
}

function getLinkedPageReferenceTitle(reference: LinkedPageReference | Record<string, unknown>) {
  return getTrimmedString((reference as Record<string, unknown>).title);
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

function getCogntLinkedNoteTitle(props: Record<string, unknown>) {
  const value = props.title;
  return typeof value === 'string' ? normalizeLinkedNoteTitle(value) : null;
}

function getCogntLinkedNoteStyle(props: Record<string, unknown>) {
  return normalizeCogntEmbedStyle(props.style);
}

