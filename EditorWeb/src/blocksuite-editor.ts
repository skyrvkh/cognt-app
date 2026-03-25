import { MarkdownAdapter, MarkdownTransformer, PageEditorBlockSpecs } from '@blocksuite/blocks';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { BlockStdScope, type EditorHost } from '@blocksuite/block-std';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { createEmptyDoc, type PageEditor } from '@blocksuite/presets';
import { Job, type Doc, type DraftModel } from '@blocksuite/store';
import { replaceSelectionWithCloze, updateClozeHighlights, type TextSelection } from './cloze';
import { reportDiagnostic } from './host';

const saveDebounceMs = 500;
const statusHideMs = 2600;

blockEffects();
presetEffects();
patchBlockStdRender();

export type CreateExtractMessage = {
  type: 'createExtract';
  sourcePath: string;
  selectedMarkdown: string;
  selectedPlainText: string;
};

export type SaveClozeMessage = {
  type: 'saveCloze';
  sourcePath: string;
  selectedPlainText: string;
  clozeText: string;
  markdown: string;
};

export type MarkdownEditorOptions = {
  hostElement: HTMLElement;
  statusElement: HTMLElement;
  onContentChanged(content: string): void;
  onCreateExtract(message: CreateExtractMessage): void;
  onSaveCloze(message: SaveClozeMessage): void;
};

export type LoadMarkdownOptions = {
  loadId?: number;
  path?: string;
  title?: string;
  content?: string;
};

export class MarkdownBlockSuiteEditor {
  private readonly editor: PageEditor;
  private readonly statusElement: HTMLElement;
  private readonly onContentChanged: (content: string) => void;
  private readonly onCreateExtract: (message: CreateExtractMessage) => void;
  private readonly onSaveCloze: (message: SaveClozeMessage) => void;
  private currentDoc: Doc | null = null;
  private currentPath = '';
  private loadingDocument = false;
  private saveTimer: number | undefined;
  private statusTimer: number | undefined;
  private clozeHighlightFrame: number | undefined;
  private blockUpdatedDisposable: { dispose(): void } | null = null;

  public constructor(options: MarkdownEditorOptions) {
    this.statusElement = options.statusElement;
    this.onContentChanged = options.onContentChanged;
    this.onCreateExtract = options.onCreateExtract;
    this.onSaveCloze = options.onSaveCloze;
    this.editor = document.createElement('page-editor') as PageEditor;
    this.editor.id = 'editor';
    this.editor.specs = PageEditorBlockSpecs;
    this.editor.doc = createEmptyMarkdownDoc();
    this.editor.hasViewport = true;
    options.hostElement.append(this.editor);
    this.bindEditorEvents();
  }

  public async load(options: LoadMarkdownOptions) {
    this.loadingDocument = true;
    window.clearTimeout(this.saveTimer);
    this.cancelScheduledClozeHighlightUpdate();
    this.currentPath = options.path ?? '';

    try {
      const markdown = options.content ?? '';
      const nextDoc = await createDocFromMarkdown(markdown);
      this.currentDoc = nextDoc;
      this.editor.doc = nextDoc;
      this.editor.hasViewport = true;
      this.bindDocChangeEvents(nextDoc);
      await this.flushHost();
      this.refreshClozeHighlights();
      reportDiagnostic('info', `Imported Markdown into BlockSuite (${markdown.length} characters).`);
      this.showStatus(`Opened ${options.title ?? 'document'}.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not load Markdown: ${message}`);
      this.showStatus(`Could not load Markdown: ${message}`);
    } finally {
      this.loadingDocument = false;
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

  private bindEditorEvents() {
    document.addEventListener(
      'keydown',
      event => {
        if (!this.currentDoc || !this.currentPath) {
          return;
        }

        if (isEditorShortcut(event, 'x')) {
          void this.handleCreateExtractShortcut(event);
          return;
        }

        if (isEditorShortcut(event, 'z')) {
          void this.handleClozeShortcut(event);
        }
      },
      true
    );

    document.addEventListener('selectionchange', () => {
      if (this.currentDoc) {
        this.scheduleClozeHighlightUpdate();
      }
    });

    this.editor.addEventListener('input', () => {
      this.scheduleClozeHighlightUpdate();
      this.scheduleContentChanged();
    });
    this.editor.addEventListener('keyup', () => {
      this.scheduleClozeHighlightUpdate();
      this.scheduleContentChanged();
    });
    this.editor.addEventListener('paste', () => this.scheduleContentChanged());
  }

  private bindDocChangeEvents(doc: Doc) {
    this.blockUpdatedDisposable?.dispose();
    this.blockUpdatedDisposable = doc.slots.blockUpdated.on(() => {
      this.scheduleContentChanged();
    });
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
  }

  private scheduleContentChanged() {
    if (this.loadingDocument || !this.currentDoc) {
      return;
    }

    window.clearTimeout(this.saveTimer);
    this.saveTimer = window.setTimeout(async () => {
      this.saveTimer = undefined;
      this.refreshClozeHighlights();
      const content = await exportDocMarkdown(this.currentDoc);
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

    const selection = getCurrentSelection();
    if (!selection) {
      this.showStatus('Ctrl+X needs selected text.');
      return;
    }

    this.onCreateExtract({
      type: 'createExtract',
      sourcePath: this.currentPath,
      selectedMarkdown: selection.markdown,
      selectedPlainText: selection.plainText,
    });
    this.showStatus('Creating Markdown extract...');
  }

  private async handleClozeShortcut(event: KeyboardEvent) {
    event.preventDefault();
    event.stopPropagation();

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
    const markdown = await exportDocMarkdown(this.currentDoc);
    this.onSaveCloze({
      type: 'saveCloze',
      sourcePath: this.currentPath,
      selectedPlainText: selection.plainText,
      clozeText,
      markdown,
    });
    this.showStatus('Selection hidden in place with cloze.');
  }

  private getTextSelection() {
    return this.editor.std?.selection.find('text') as TextSelection | null | undefined;
  }

  private createClozeContext() {
    return {
      doc: this.currentDoc,
      textSelection: this.getTextSelection(),
    };
  }
}

type ShortcutKey = 'x' | 'z';

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

function getCurrentSelection() {
  const selectedText = window.getSelection()?.toString() ?? '';
  const plainText = normalizeSelectionText(selectedText);

  if (plainText.length === 0) {
    return null;
  }

  return {
    plainText,
    markdown: ensureTrailingNewline(selectedText.trim()),
  };
}

function normalizeSelectionText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

function ensureTrailingNewline(text: string) {
  return text.endsWith('\n') ? text : `${text}\n`;
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

async function createDocFromMarkdown(markdown: string) {
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
  }

  doc.resetHistory();
  return doc;
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

  const adapter = new MarkdownAdapter(job);
  const result = await adapter.fromDocSnapshot({
    snapshot,
    assets: job.assetsManager,
  });

  return result.file.trimEnd();
}
