import { MarkdownTransformer, PageEditorBlockSpecs } from '@blocksuite/blocks';
import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { BlockStdScope, type EditorHost } from '@blocksuite/block-std';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import { createEmptyDoc, type PageEditor } from '@blocksuite/presets';
import type { Doc, DraftModel } from '@blocksuite/store';
import { updateClozeHighlights } from './cloze';
import { reportDiagnostic } from './host';

blockEffects();
presetEffects();
patchBlockStdRender();

export class MarkdownBlockSuiteRenderer {
  private readonly editor: PageEditor;
  private readonly doc: Doc;

  public constructor(hostElement: HTMLElement) {
    this.doc = createEmptyMarkdownDoc();
    this.editor = document.createElement('page-editor') as PageEditor;
    this.editor.id = 'review-renderer';
    this.editor.doc = this.doc;
    this.editor.hasViewport = true;
    this.editor.specs = PageEditorBlockSpecs;
    hostElement.append(this.editor);
    this.preventEditing();
  }

  public async load(markdown: string) {
    try {
      await loadMarkdownIntoDoc(this.doc, markdown);
      this.editor.doc = this.doc;
      this.editor.hasViewport = true;
      await this.flushHost();
      updateClozeHighlights(this.editor);
      reportDiagnostic('info', `Rendered review card (${markdown.length} characters).`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not render review card: ${message}`);
      throw error;
    }
  }

  private preventEditing() {
    this.editor.tabIndex = -1;
    this.editor.addEventListener('beforeinput', event => event.preventDefault(), true);
    this.editor.addEventListener('paste', event => event.preventDefault(), true);
    this.editor.addEventListener(
      'keydown',
      event => {
        if (event.ctrlKey || event.metaKey || event.altKey) {
          return;
        }

        if (event.key.length === 1 || event.key === 'Backspace' || event.key === 'Delete') {
          event.preventDefault();
        }
      },
      true
    );
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

async function loadMarkdownIntoDoc(doc: Doc, markdown: string) {
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
}
