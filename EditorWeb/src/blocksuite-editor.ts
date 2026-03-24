import { MarkdownAdapter, MarkdownTransformer, PageEditorBlockSpecs } from '@blocksuite/blocks';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { BlockStdScope, type EditorHost } from '@blocksuite/block-std';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { createEmptyDoc, type PageEditor } from '@blocksuite/presets';
import { Job, type Doc, type DraftModel } from '@blocksuite/store';
import { reportDiagnostic } from './host';

const saveDebounceMs = 500;
const statusHideMs = 2600;

blockEffects();
presetEffects();
patchBlockStdRender();

export type MarkdownEditorOptions = {
  hostElement: HTMLElement;
  statusElement: HTMLElement;
  onContentChanged(content: string): void;
};

export type LoadMarkdownOptions = {
  title?: string;
  content?: string;
};

export class MarkdownBlockSuiteEditor {
  private readonly editor: PageEditor;
  private readonly statusElement: HTMLElement;
  private readonly onContentChanged: (content: string) => void;
  private currentDoc: Doc | null = null;
  private loadingDocument = false;
  private saveTimer: number | undefined;
  private statusTimer: number | undefined;
  private blockUpdatedDisposable: { dispose(): void } | null = null;

  public constructor(options: MarkdownEditorOptions) {
    this.statusElement = options.statusElement;
    this.onContentChanged = options.onContentChanged;
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

    try {
      const markdown = options.content ?? '';
      const nextDoc = await createDocFromMarkdown(markdown);
      this.currentDoc = nextDoc;
      this.editor.doc = nextDoc;
      this.editor.hasViewport = true;
      this.bindDocChangeEvents(nextDoc);
      await this.flushHost();
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

  private bindEditorEvents() {
    this.editor.addEventListener('input', () => this.scheduleContentChanged());
    this.editor.addEventListener('keyup', () => this.scheduleContentChanged());
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
      const content = await exportDocMarkdown(this.currentDoc);
      this.onContentChanged(content);
    }, saveDebounceMs);
  }

  private showStatus(message: string) {
    this.statusElement.textContent = message;
    this.statusElement.dataset.visible = 'true';
    window.clearTimeout(this.statusTimer);
    this.statusTimer = window.setTimeout(() => {
      this.statusElement.dataset.visible = 'false';
    }, statusHideMs);
  }
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
