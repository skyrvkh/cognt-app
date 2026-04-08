import {
  AffineSlashMenuWidget,
  createDefaultDoc,
  defaultBlockMarkdownAdapterMatchers,
  MarkdownAdapter,
  MarkdownTransformer,
  PageEditorBlockSpecs,
  type AffineSlashMenuActionItem,
  type AffineSlashMenuGroupDivider,
  type AffineSlashMenuItem,
  type AffineSlashMenuItemGenerator,
  type AffineSlashMenuContext,
  type AffineSlashSubMenu,
} from '@blocksuite/blocks';
import {
  focusTextModel,
  getInlineEditorByModel,
  REFERENCE_NODE,
  RefNodeSlotsProvider,
} from '@blocksuite/affine-components/rich-text';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { BlockStdScope, ConfigExtension, type EditorHost } from '@blocksuite/block-std';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { createEmptyDoc, type PageEditor } from '@blocksuite/presets';
import {
  Job,
  type BlockModel,
  type BlockSnapshot,
  type Doc,
  type DocSnapshot,
  type DraftModel,
} from '@blocksuite/store';
import { replaceSelectionWithCloze, updateClozeHighlights, type TextSelection } from './cloze';
import { reportDiagnostic } from './host';
import {
  MentionController,
  type NativeInlineEditor,
  type NativeLinkedMenuGroup,
  type OpenMentionMessage,
  type RequestMentionPreviewMessage,
  type SearchMentionTargetsMessage,
} from './mention-controller';
import { parseNoteMentionTarget, type MentionPreviewResult, type MentionTarget } from './mentions';

const saveDebounceMs = 500;
const statusHideMs = 2600;
const linkedDocPreviewMaxCharacters = 4000;
const hiddenSlashMenuSections = new Set(['Style', 'Date', 'Actions']);
const hiddenSlashMenuItems = new Set(['Figma', 'GitHub', 'Github', 'Loom', 'Linked Doc', 'Embed Link']);
const cogntLinkedNotePathProp = 'cogntLinkedNotePath';
const embedLinkedDocTagName = 'affine-embed-linked-doc-block';
const cogntLinkedDocWrapStyleId = 'cognt-linked-doc-wrap-style';
const defaultCogntEmbedStyle = 'horizontal';
const cogntBlockSuiteDocumentKind = 'cognt-blocksuite-doc';
const cogntBlockSuiteDocumentVersion = 1;
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

type TextSelectionPointSnapshot = {
  blockId: string;
  index: number;
  length: number;
};

type TextSelectionSnapshot = {
  from: TextSelectionPointSnapshot;
  to: TextSelectionPointSnapshot | null;
  start?: TextSelectionPointSnapshot;
  end?: TextSelectionPointSnapshot;
};

type PendingExtract = {
  generation: number;
  sourcePath: string;
  selection: TextSelectionSnapshot;
};

type CogntBlockSuiteDocument = {
  kind?: string;
  version?: number;
  snapshot?: DocSnapshot | null;
};

configureSlashMenu();
blockEffects();
presetEffects();
patchBlockStdRender();

export type CreateExtractMessage = {
  type: 'createExtract';
  sourcePath: string;
  selectedContent: string;
  selectedPlainText: string;
};

export type SaveClozeMessage = {
  type: 'saveCloze';
  sourcePath: string;
  selectedPlainText: string;
  clozeText: string;
  content: string;
};

export type LinkedNoteViewMode = 'inline' | 'embed' | 'native';

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
  onCreateExtract(message: CreateExtractMessage): void;
  onSaveCloze(message: SaveClozeMessage): void;
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
  private readonly onCreateExtract: (message: CreateExtractMessage) => void;
  private readonly onSaveCloze: (message: SaveClozeMessage) => void;
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
    this.setCurrentDocReadonly(false);

    await waitForNextAnimationFrame();

    if (generation !== this.loadGeneration || !this.currentDoc) {
      return false;
    }

    await this.forceEditableRichText();
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
      this.focusFirstTextBlock();
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
    await this.forceEditableRichText();
    this.focusFirstTextBlock();
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
    this.setCurrentDocReadonly(false);
  }

  private setCurrentDocReadonly(readonly: boolean) {
    this.currentDoc?.awarenessStore.setReadonly(this.currentDoc.blockCollection, readonly);
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

  private focusFirstTextBlock() {
    if (!this.currentDoc || this.currentDoc.readonly) {
      return;
    }

    const textBlock = this.currentDoc
      .getBlocks()
      .find(block => block.role === 'content' && block.text);

    if (textBlock) {
      focusTextModel(this.editor.std, textBlock.id, textBlock.text?.length ?? 0);
    }
  }

  private async forceEditableRichText() {
    for (const richText of findElementsDeep(this.editor, 'rich-text')) {
      const editable = richText as HTMLElement & {
        readonly?: boolean;
        inlineEditor?: { setReadonly?: (readonly: boolean) => void };
        requestUpdate?: (name?: string, oldValue?: unknown) => void;
        updateComplete?: Promise<unknown>;
      };

      editable.readonly = false;
      editable.inlineEditor?.setReadonly?.(false);
      editable.requestUpdate?.('readonly', true);
      await editable.updateComplete;

      const inlineEditor = findEditableElement(editable.shadowRoot) ??
        editable.shadowRoot?.querySelector<HTMLElement>('.inline-editor');

      inlineEditor?.classList.remove('readonly');
      inlineEditor?.setAttribute('contenteditable', 'true');
    }
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
    await this.editor.updateComplete;

    const host = this.editor.host as (EditorHost & {
      requestUpdate?: () => void;
      updateComplete?: Promise<unknown>;
    }) | null;

    if (!host || !this.editor.std || !this.editor.doc) {
      return;
    }

    host.std = this.editor.std;
    host.doc = this.editor.doc;
    host.requestUpdate?.();
    await host.updateComplete;
    patchRenderedEmbedLinkedDocBlocks(this.editor);
    window.requestAnimationFrame(() => patchRenderedEmbedLinkedDocBlocks(this.editor));
    window.setTimeout(() => patchRenderedEmbedLinkedDocBlocks(this.editor), 80);
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
    event.preventDefault();
    event.stopPropagation();

    if (this.isEditorInputLocked()) {
      this.showStatus('Wait for the full note to finish loading before extracting text.');
      return;
    }

    const selection = getCurrentSelection();
    if (!selection) {
      this.showStatus('Ctrl+X needs selected text.');
      return;
    }

    const textSelection = this.getTextSelection();
    const selectionSnapshot = textSelection && !textSelection.isCollapsed()
      ? cloneTextSelection(textSelection)
      : null;
    if (!selectionSnapshot) {
      this.showStatus('Ctrl+X needs a BlockSuite text selection.');
      return;
    }

    const selectedContent = this.currentDoc
      ? createBlockSuiteDocumentJsonFromSelection(this.currentDoc, this.editor, selection.plainText)
      : null;
    this.pendingExtract = {
      generation: this.loadGeneration,
      sourcePath: this.currentPath,
      selection: selectionSnapshot,
    };

    this.onCreateExtract({
      type: 'createExtract',
      sourcePath: this.currentPath,
      selectedContent: selectedContent ?? createBlockSuiteDocumentJsonFromPlainText(
        selection.plainText,
        createTitleFromPlainText(selection.plainText, 'Extract')
      ),
      selectedPlainText: selection.plainText,
    });
    this.showStatus('Creating extract...');
  }

  private async handleClozeShortcut(event: KeyboardEvent) {
    event.preventDefault();
    event.stopPropagation();

    if (this.isEditorInputLocked()) {
      this.showStatus('Wait for the full note to finish loading before creating cloze text.');
      return;
    }

    const selection = getCurrentSelection();
    if (!selection) {
      this.showStatus('Ctrl+Z is disabled unless text is selected.');
      return;
    }

    let clozeText: string;
    try {
      clozeText = replaceSelectionWithCloze(this.createClozeContext(), selection.plainText);
    } catch (error) {
      this.showStatus(
        error instanceof Error ? error.message : 'Could not hide the selected text in place.'
      );
      return;
    }

    this.editor.host?.selection.clear(['text']);
    window.getSelection()?.removeAllRanges();
    await this.flushHost();
    await new Promise(resolve => window.requestAnimationFrame(resolve));
    this.refreshClozeHighlights();
    this.cancelPendingContentChanged();
    const content = await this.exportCurrentContent();
    this.onSaveCloze({
      type: 'saveCloze',
      sourcePath: this.currentPath,
      selectedPlainText: selection.plainText,
      clozeText,
      content,
    });
    this.showStatus('Selection hidden in place with cloze.');
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

  private createClozeContext() {
    return {
      doc: this.currentDoc,
      textSelection: this.getTextSelection(),
    };
  }
}

type ShortcutKey = 'x' | 'z';
type SlashMenuStaticItem =
  | AffineSlashMenuActionItem
  | AffineSlashMenuGroupDivider
  | AffineSlashSubMenu;

function configureSlashMenu() {
  const config = AffineSlashMenuWidget.DEFAULT_CONFIG;

  AffineSlashMenuWidget.DEFAULT_CONFIG = {
    ...config,
    items: [
      { groupName: 'Cognt' },
      {
        name: 'Create Note',
        description: 'Create a note link.',
        alias: ['note', 'linked note', 'new note'],
        subMenu: [
          { groupName: 'Create Note' },
          {
            name: 'Inline Link',
            description: 'Insert a link to a new note.',
            alias: ['inline note', 'link note'],
            action: context => {
              void runCreateLinkedNoteAction('inline', context);
            },
          },
          {
            name: 'Embed Link',
            description: 'Insert an embedded reference to a new note.',
            alias: ['embed note', 'note embed', 'embed view'],
            action: context => {
              void runCreateLinkedNoteAction('embed', context);
            },
          },
        ],
      },
      ...filterSlashMenuItems(config.items),
    ],
  };
}

async function runCreateLinkedNoteAction(
  viewMode: LinkedNoteViewMode,
  context: AffineSlashMenuContext
) {
  const editor = editorsByDoc.get(context.rootComponent.doc);

  if (!editor) {
    reportDiagnostic('error', 'Could not create linked note: no editor is registered for this document.');
    return;
  }

  await editor.createLinkedNoteFromSlashMenu(viewMode, context);
}

function filterSlashMenuItems(items: AffineSlashMenuItem[]) {
  const filtered: AffineSlashMenuItem[] = [];
  let isSkippingSection = false;

  for (const item of items) {
    if (isSlashMenuGenerator(item)) {
      if (!isSkippingSection) {
        filtered.push(context => filterSlashMenuStaticItems(item(context)));
      }

      continue;
    }

    if (isSlashMenuGroupDivider(item)) {
      isSkippingSection = hiddenSlashMenuSections.has(item.groupName);

      if (!isSkippingSection) {
        filtered.push(item);
      }

      continue;
    }

    if (isSkippingSection) {
      continue;
    }

    const nextItem = filterSlashMenuStaticItem(item);
    if (nextItem) {
      filtered.push(nextItem);
    }
  }

  return filtered;
}

function filterSlashMenuStaticItems(items: SlashMenuStaticItem[]) {
  const filtered: SlashMenuStaticItem[] = [];
  let isSkippingSection = false;

  for (const item of items) {
    if (isSlashMenuGroupDivider(item)) {
      isSkippingSection = hiddenSlashMenuSections.has(item.groupName);

      if (!isSkippingSection) {
        filtered.push(item);
      }

      continue;
    }

    if (isSkippingSection) {
      continue;
    }

    const nextItem = filterSlashMenuStaticItem(item);
    if (nextItem) {
      filtered.push(nextItem);
    }
  }

  return filtered;
}

function filterSlashMenuStaticItem(item: SlashMenuStaticItem): SlashMenuStaticItem | null {
  if (isNamedSlashMenuItem(item) && hiddenSlashMenuItems.has(item.name)) {
    return null;
  }

  if (isNamedSlashMenuItem(item) && item.name === 'New Doc' && !isSlashSubMenu(item)) {
    return {
      ...item,
      description: 'Start a new note.',
      action: context => {
        void runCreateLinkedNoteAction('inline', context);
      },
    } as AffineSlashMenuActionItem;
  }

  if (isSlashSubMenu(item)) {
    const subMenu = filterSlashMenuStaticItems(item.subMenu);

    if (subMenu.length === 0) {
      return null;
    }

    return {
      ...item,
      subMenu,
    };
  }

  return item;
}

function isSlashMenuGenerator(item: AffineSlashMenuItem): item is AffineSlashMenuItemGenerator {
  return typeof item === 'function';
}

function isSlashMenuGroupDivider(item: SlashMenuStaticItem): item is AffineSlashMenuGroupDivider {
  return 'groupName' in item;
}

function isSlashSubMenu(item: SlashMenuStaticItem): item is AffineSlashSubMenu {
  return 'subMenu' in item;
}

function isNamedSlashMenuItem(
  item: SlashMenuStaticItem
): item is AffineSlashMenuActionItem | AffineSlashSubMenu {
  return 'name' in item;
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

function createEditorSpecs(
  getMenus: (
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor,
    abortSignal: AbortSignal
  ) => Promise<NativeLinkedMenuGroup[]> | NativeLinkedMenuGroup[]
) {
  return [
    ...PageEditorBlockSpecs,
    ConfigExtension('affine:page', {
      linkedWidget: {
        triggerKeys: ['@'],
        convertTriggerKey: false,
        getMenus: (
          query: string,
          abort: () => void,
          _editorHost: EditorHost,
          inlineEditor: NativeInlineEditor,
          abortSignal: AbortSignal
        ) => getMenus(query, abort, inlineEditor, abortSignal),
      },
    }),
  ];
}

function isEditorShortcut(
  event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
  key: ShortcutKey
) {
  return (
    (event.ctrlKey || event.metaKey) &&
    !event.shiftKey &&
    !event.altKey &&
    event.key.toLowerCase() === key
  );
}

function preventReadOnlyKeyboardInput(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) {
    return;
  }

  if (event.key.length === 1 || event.key === 'Backspace' || event.key === 'Delete') {
    event.preventDefault();
  }
}

function findEditableElement(root: ParentNode | ShadowRoot | null): HTMLElement | null {
  if (!root) {
    return null;
  }

  const editable = findElementDeep(root, '[contenteditable="true"], [contenteditable=""], textarea, input');
  if (editable) {
    return editable;
  }

  return findElementDeep(root, '.inline-editor, rich-text');
}

function getInteractivityDiagnostic(doc: Doc | null, root: ParentNode | ShadowRoot) {
  const editable = findEditableElement(root);
  const readOnlyRichTexts = findElementsDeep(root, 'rich-text')
    .filter(element => (element as HTMLElement & { readonly?: boolean }).readonly);
  const falseContentEditableCount = findElementsDeep(root, '[contenteditable="false"]').length;

  return [
    `docReadonly=${String(doc?.readonly ?? false)}`,
    `editable=${String(!!editable)}`,
    `editableTag=${editable?.tagName.toLowerCase() ?? 'none'}`,
    `editableContentEditable=${editable?.getAttribute('contenteditable') ?? 'none'}`,
    `readOnlyRichTexts=${readOnlyRichTexts.length}`,
    `falseContentEditable=${falseContentEditableCount}`,
  ].join(', ');
}

function findElementDeep(root: ParentNode | ShadowRoot, selector: string): HTMLElement | null {
  const direct = root.querySelector<HTMLElement>(selector);

  if (direct) {
    return direct;
  }

  for (const element of root.querySelectorAll<HTMLElement>('*')) {
    const found = element.shadowRoot ? findElementDeep(element.shadowRoot, selector) : null;
    if (found) {
      return found;
    }
  }

  return null;
}

function findElementsDeep(root: ParentNode | ShadowRoot, selector: string): HTMLElement[] {
  const elements = Array.from(root.querySelectorAll<HTMLElement>(selector));

  if (root instanceof HTMLElement && root.shadowRoot) {
    elements.push(...findElementsDeep(root.shadowRoot, selector));
  }

  for (const element of root.querySelectorAll<HTMLElement>('*')) {
    if (element.shadowRoot) {
      elements.push(...findElementsDeep(element.shadowRoot, selector));
    }
  }

  return elements;
}

function waitForNextAnimationFrame() {
  return new Promise<void>(resolve => {
    window.requestAnimationFrame(() => resolve());
  });
}


function getCurrentSelection() {
  const selectedText = window.getSelection()?.toString() ?? '';
  const plainText = normalizeSelectionText(selectedText);

  if (plainText.length === 0) {
    return null;
  }

  return {
    plainText,
  };
}

function createBlockSuiteDocumentJsonFromSelection(
  doc: Doc,
  editor: PageEditor,
  plainText: string
) {
  const textSelection = editor.std?.selection.find('text') as TextSelection | null | undefined;
  if (!textSelection || textSelection.isCollapsed()) {
    return null;
  }

  const models = getSelectedTextBlockModels(editor, doc, textSelection);
  if (models.length === 0) {
    return null;
  }

  const job = new Job({ collection: doc.collection });
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  const snapshots = models
    .map(model => job.blockToSnapshot(model as DraftModel))
    .filter(isBlockSnapshot)
    .map(cloneBlockSnapshot)
    .map(snapshot => trimSnapshotForTextSelection(snapshot, start, end));

  if (snapshots.length === 0 || !snapshots.some(hasSnapshotBodyContent)) {
    return null;
  }

  return createBlockSuiteDocumentJsonFromBlockSnapshots(
    snapshots,
    createTitleFromPlainText(plainText, 'Extract')
  );
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

function cloneTextSelection(selection: TextSelection): TextSelectionSnapshot {
  return {
    from: cloneTextSelectionPoint(selection.from),
    to: selection.to ? cloneTextSelectionPoint(selection.to) : null,
    start: selection.start ? cloneTextSelectionPoint(selection.start) : undefined,
    end: selection.end ? cloneTextSelectionPoint(selection.end) : undefined,
  };
}

function cloneTextSelectionPoint(point: TextSelectionPointSnapshot): TextSelectionPointSnapshot {
  return {
    blockId: point.blockId,
    index: point.index,
    length: point.length,
  };
}

function getTextSelectionSnapshotRange(selection: TextSelectionSnapshot) {
  const start = selection.start ?? selection.from;
  const end = selection.end ?? selection.to ?? selection.from;

  if (start.blockId !== end.blockId) {
    return null;
  }

  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    start.index + Math.max(0, start.length),
    end.index + Math.max(0, end.length)
  );
  const length = selectionEnd - index;

  return length > 0
    ? {
        blockId: start.blockId,
        index,
        length,
      }
    : null;
}

function getSelectedTextBlockModels(
  editor: PageEditor,
  doc: Doc,
  textSelection: TextSelection
) {
  const range = editor.std?.range.textSelectionToRange(textSelection as never);
  const components = range
    ? editor.std.range.getSelectedBlockComponentsByRange(range, {
        match: (element: { model?: { role?: string } }) => element.model?.role === 'content',
        mode: 'flat',
      })
    : [];

  const models = components
    .map((component: { model?: BlockModel }) => component.model)
    .filter((model): model is BlockModel => Boolean(model));

  if (models.length > 0) {
    return models;
  }

  const fallback = doc.getBlock(textSelection.from.blockId)?.model;
  return fallback ? [fallback as BlockModel] : [];
}

function getTextSelectionStart(textSelection: TextSelection) {
  return textSelection.start
    ?? (isReverseTextSelection(textSelection) ? (textSelection.to ?? textSelection.from) : textSelection.from);
}

function getTextSelectionEnd(textSelection: TextSelection) {
  return textSelection.end
    ?? (isReverseTextSelection(textSelection) ? textSelection.from : (textSelection.to ?? textSelection.from));
}

function isReverseTextSelection(textSelection: TextSelection) {
  return Boolean((textSelection as TextSelection & { reverse?: boolean }).reverse);
}

function cloneBlockSnapshot(snapshot: BlockSnapshot) {
  return JSON.parse(JSON.stringify(snapshot)) as BlockSnapshot;
}

function trimSnapshotForTextSelection(
  snapshot: BlockSnapshot,
  start: { blockId: string; index: number; length: number },
  end: { blockId: string; index: number; length: number }
) {
  if (start.blockId === end.blockId) {
    if (snapshot.id === start.blockId) {
      const range = getSameBlockSelectionRange(start, end);
      trimSnapshotText(snapshot, range.index, range.length);
    }
  } else if (snapshot.id === start.blockId) {
    trimSnapshotText(snapshot, start.index, getSnapshotTextLength(snapshot) - start.index);
  } else if (snapshot.id === end.blockId) {
    trimSnapshotText(snapshot, 0, getSelectionPointEndOffset(end));
  }

  snapshot.children = snapshot.children.map(child => trimSnapshotForTextSelection(child, start, end));
  return snapshot;
}

function getSameBlockSelectionRange(
  start: { index: number; length: number },
  end: { index: number; length: number }
) {
  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    getSelectionPointEndOffset(start),
    getSelectionPointEndOffset(end)
  );

  return {
    index,
    length: Math.max(0, selectionEnd - index),
  };
}

function getSelectionPointEndOffset(point: { index: number; length: number }) {
  return point.index + Math.max(0, point.length);
}

function getSnapshotTextLength(snapshot: BlockSnapshot) {
  return getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .reduce((length, entry) => (
      isRecord(entry) && typeof entry.insert === 'string'
        ? length + entry.insert.length
        : length
    ), 0);
}

function hasSnapshotBodyContent(snapshot: BlockSnapshot): boolean {
  const hasText = getSnapshotTextDelta(getSnapshotTextValue(snapshot) ?? {})
    .some(entry => isRecord(entry) && typeof entry.insert === 'string' && entry.insert.trim().length > 0);

  return hasText || snapshot.children.some(hasSnapshotBodyContent);
}

function trimSnapshotText(snapshot: BlockSnapshot, index: number, length: number) {
  const textValue = getSnapshotTextValue(snapshot);
  if (!textValue) {
    return;
  }

  snapshot.props = {
    ...snapshot.props,
    text: {
      ...textValue,
      delta: sliceTextDelta(getSnapshotTextDelta(textValue), index, length),
    },
  };
}

function getSnapshotTextValue(snapshot: BlockSnapshot) {
  const text = snapshot.props.text;
  return isRecord(text) ? text : null;
}

function getSnapshotTextDelta(textValue: Record<string, unknown>) {
  return Array.isArray(textValue.delta) ? textValue.delta : [];
}

function sliceTextDelta(delta: unknown[], index: number, length: number) {
  const start = Math.max(0, index);
  const end = Math.max(start, start + Math.max(0, length));
  const result: Array<Record<string, unknown>> = [];
  let offset = 0;

  for (const entry of delta) {
    if (!isRecord(entry) || typeof entry.insert !== 'string') {
      continue;
    }

    const insert = entry.insert;
    const entryStart = offset;
    const entryEnd = offset + insert.length;
    const overlapStart = Math.max(start, entryStart);
    const overlapEnd = Math.min(end, entryEnd);

    if (overlapEnd > overlapStart) {
      result.push({
        ...entry,
        insert: insert.slice(overlapStart - entryStart, overlapEnd - entryStart),
      });
    }

    offset = entryEnd;
  }

  return result;
}

function createBlockSuiteDocumentJsonFromBlockSnapshots(
  children: BlockSnapshot[],
  title: string
) {
  const pageId = createBlockId();
  const surfaceId = createBlockId();
  const noteId = createBlockId();

  return `${JSON.stringify(
    {
      kind: cogntBlockSuiteDocumentKind,
      version: cogntBlockSuiteDocumentVersion,
      snapshot: {
        type: 'page',
        meta: {
          id: pageId,
          title,
          createDate: Date.now(),
          tags: [],
        },
        blocks: {
          type: 'block',
          id: pageId,
          flavour: 'affine:page',
          version: 2,
          props: {
            title: createBlockSuiteTextValue(title),
          },
          children: [
            {
              type: 'block',
              id: surfaceId,
              flavour: 'affine:surface',
              version: 5,
              props: {},
              children: [],
            },
            {
              type: 'block',
              id: noteId,
              flavour: 'affine:note',
              version: 1,
              props: {
                xywh: '[0,0,800,95]',
                background: '--affine-note-background-blue',
                index: 'a0',
                hidden: false,
                displayMode: 'both',
                edgeless: {
                  style: {
                    borderRadius: 8,
                    borderSize: 4,
                    borderStyle: 'none',
                    shadowType: '--affine-note-shadow-box',
                  },
                },
              },
              children,
            },
          ],
        },
      },
    },
    null,
    2
  )}\n`;
}

function createBlockSuiteDocumentJsonFromPlainText(text: string, title: string) {
  const pageId = createBlockId();
  const surfaceId = createBlockId();
  const noteId = createBlockId();
  const paragraphId = createBlockId();

  return `${JSON.stringify(
    {
      kind: cogntBlockSuiteDocumentKind,
      version: cogntBlockSuiteDocumentVersion,
      snapshot: {
        type: 'page',
        meta: {
          id: pageId,
          title,
          createDate: Date.now(),
          tags: [],
        },
        blocks: {
          type: 'block',
          id: pageId,
          flavour: 'affine:page',
          version: 2,
          props: {
            title: createBlockSuiteTextValue(title),
          },
          children: [
            {
              type: 'block',
              id: surfaceId,
              flavour: 'affine:surface',
              version: 5,
              props: {},
              children: [],
            },
            {
              type: 'block',
              id: noteId,
              flavour: 'affine:note',
              version: 1,
              props: {
                xywh: '[0,0,800,95]',
                background: '--affine-note-background-blue',
                index: 'a0',
                hidden: false,
                displayMode: 'both',
                edgeless: {
                  style: {
                    borderRadius: 8,
                    borderSize: 4,
                    borderStyle: 'none',
                    shadowType: '--affine-note-shadow-box',
                  },
                },
              },
              children: [
                {
                  type: 'block',
                  id: paragraphId,
                  flavour: 'affine:paragraph',
                  version: 1,
                  props: {
                    type: 'text',
                    text: createBlockSuiteTextValue(text),
                    collapsed: false,
                  },
                  children: [],
                },
              ],
            },
          ],
        },
      },
    },
    null,
    2
  )}\n`;
}

function createBlockSuiteTextValue(value: string) {
  return {
    '$blocksuite:internal:text$': true,
    delta: value.length > 0 ? [{ insert: value }] : [],
  };
}

function createBlockId() {
  return `cognt-${Math.random().toString(36).slice(2, 14)}`;
}

function createTitleFromPlainText(text: string, fallback: string) {
  const title = text.replace(/\s+/g, ' ').trim();
  if (title.length === 0) {
    return fallback;
  }

  return title.length <= 60 ? title : title.slice(0, 60).trimEnd();
}

function normalizeSelectionText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

function patchBlockStdRender() {
  const prototype = BlockStdScope.prototype as unknown as {
    __cogntRenderPatched?: boolean;
    render: () => EditorHost;
  };

  if (prototype.__cogntRenderPatched) {
    return;
  }

  prototype.render = function renderWithCreateElement(this: BlockStdScope) {
    const scope = this as unknown as {
      doc: Doc;
      _getHost: () => EditorHost;
      _lifeCycleWatchers: Array<{ rendered: { call(watcher: unknown): void } }>;
    };
    const element = document.createElement('editor-host') as EditorHost;
    element.std = this;
    element.doc = scope.doc;
    scope._getHost = () => element;
    scope._lifeCycleWatchers.forEach(watcher => {
      watcher.rendered.call(watcher);
    });
    return element;
  };
  prototype.__cogntRenderPatched = true;
}

function createEmptyMarkdownDoc() {
  return createEmptyDoc().init();
}

function isBlockSuiteJsonPath(path: string | null | undefined) {
  return typeof path === 'string' && /\.json$/i.test(path);
}

function clearNoteChildren(doc: Doc) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    throw new Error('BlockSuite note block was not created.');
  }

  for (const child of [...note.children]) {
    doc.deleteBlock(child as DraftModel, { deleteChildren: true });
  }

  return note;
}

async function createDocFromBlockSuiteJson(
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const snapshot = parseBlockSuiteDocumentSnapshot(content);
  if (!snapshot) {
    const doc = createEmptyMarkdownDoc();
    doc.resetHistory();
    return doc;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  const seedDoc = createEmptyMarkdownDoc();
  const job = new Job({ collection: seedDoc.collection });
  const doc = await job.snapshotToDoc(snapshot);
  if (!doc) {
    throw new Error('BlockSuite could not load the saved document snapshot.');
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
  registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
  doc.resetHistory();
  return doc;
}

async function importBlockSuiteJsonIntoExistingDoc(
  doc: Doc,
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void
) {
  const note = clearNoteChildren(doc);
  const snapshot = parseBlockSuiteDocumentSnapshot(content);

  if (!snapshot) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    doc.resetHistory();
    return;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  const sourceNote = findFirstBlockSnapshot(snapshot.blocks, 'affine:note');
  const children = sourceNote?.children?.filter(isBlockSnapshot) ?? [];

  if (children.length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    const job = new Job({ collection: doc.collection });
    for (let index = 0; index < children.length; index++) {
      await job.snapshotToBlock(children[index], doc, note.id, index);
    }
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc);
  registerSerializedCogntInlineLinkedNotes(doc);
  doc.resetHistory();
}

async function createDocFromMarkdown(
  markdown: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const doc = createEmptyMarkdownDoc();
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
    upgradeRuntimeRawCogntInlineMentions(doc, resolveTitle);
    registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
  }

  doc.resetHistory();
  return doc;
}

async function importMarkdownIntoExistingDoc(doc: Doc, markdown: string) {
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc);
    upgradeRuntimeRawCogntInlineMentions(doc);
    registerSerializedCogntInlineLinkedNotes(doc);
  }

  doc.resetHistory();
}

function parseBlockSuiteDocumentSnapshot(content: string): DocSnapshot | null {
  const trimmed = content.trim();
  if (trimmed.length === 0) {
    return null;
  }

  const parsed = JSON.parse(trimmed) as unknown;
  if (isDocSnapshot(parsed)) {
    return parsed;
  }

  if (!isRecord(parsed)) {
    throw new Error('The note file is not a BlockSuite JSON document.');
  }

  const document = parsed as CogntBlockSuiteDocument;
  if (typeof document.kind === 'string' && document.kind !== cogntBlockSuiteDocumentKind) {
    throw new Error('The note file was saved by an unsupported document format.');
  }

  if (
    typeof document.version === 'number' &&
    document.version > cogntBlockSuiteDocumentVersion
  ) {
    throw new Error('The note file was saved by a newer BlockSuite document format.');
  }

  if (document.snapshot == null) {
    return null;
  }

  if (!isDocSnapshot(document.snapshot)) {
    throw new Error('The BlockSuite document snapshot is invalid.');
  }

  return document.snapshot;
}

function isDocSnapshot(value: unknown): value is DocSnapshot {
  return isRecord(value)
    && value.type === 'page'
    && isRecord(value.meta)
    && isBlockSnapshot(value.blocks);
}

function isBlockSnapshot(value: unknown): value is BlockSnapshot {
  return isRecord(value)
    && value.type === 'block'
    && typeof value.id === 'string'
    && typeof value.flavour === 'string'
    && isRecord(value.props)
    && Array.isArray(value.children);
}

function findFirstBlockSnapshot(block: BlockSnapshot, flavour: string): BlockSnapshot | null {
  if (block.flavour === flavour) {
    return block;
  }

  for (const child of block.children) {
    if (!isBlockSnapshot(child)) {
      continue;
    }

    const result = findFirstBlockSnapshot(child, flavour);
    if (result) {
      return result;
    }
  }

  return null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function formatElapsed(startedAt: number) {
  return `${Math.round(performance.now() - startedAt)}ms`;
}

function logEditorTiming(operation: string, startedAt: number, detail?: string) {
  const suffix = detail ? ` [${detail}]` : '';
  reportDiagnostic('info', `BlockSuite editor ${operation}: ${formatElapsed(startedAt)}${suffix}`);
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

async function exportDocMarkdown(doc: Doc | null) {
  if (!doc) {
    return '';
  }

  const job = new Job({ collection: doc.collection });
  const snapshot = job.docToSnapshot(doc);

  if (!snapshot) {
    return '';
  }

  const adapter = new MarkdownAdapter(job, defaultBlockMarkdownAdapterMatchers);
  const result = await adapter.fromDocSnapshot({
    snapshot,
    assets: job.assetsManager,
  });

  return result.file.trimEnd();
}

async function exportDocContent(doc: Doc | null, path: string | null | undefined) {
  return isBlockSuiteJsonPath(path)
    ? exportDocBlockSuiteJson(doc)
    : exportDocMarkdown(doc);
}

async function exportDocBlockSuiteJson(doc: Doc | null) {
  if (!doc) {
    return `${JSON.stringify(
      {
        kind: cogntBlockSuiteDocumentKind,
        version: cogntBlockSuiteDocumentVersion,
        snapshot: null,
      },
      null,
      2
    )}\n`;
  }

  const job = new Job({ collection: doc.collection });
  const snapshot = job.docToSnapshot(doc);
  const payload: CogntBlockSuiteDocument = {
    kind: cogntBlockSuiteDocumentKind,
    version: cogntBlockSuiteDocumentVersion,
    snapshot: snapshot ?? null,
  };

  return `${JSON.stringify(payload, null, 2)}\n`;
}
