import { reportDiagnostic } from '../host';
import type { LoadMarkdownOptions } from './contracts';
import {
  assignLoadedDocument,
  deferPostLoadWork,
  unlockEditorAfterRender,
} from './document-load-commit';
import { prepareDocumentLoad } from './document-load-prepare';
import {
  type LoadedEditorDocument,
  loadEditorDocumentContent,
} from './document-loader';
import {
  formatElapsed,
} from './timing';
import type {
  DocumentLoadStateGuard,
  DocumentLoadWorkflowOptions,
} from './document-load-types';

type CompletedDocumentLoad = {
  loaded: boolean;
  importKind: LoadedEditorDocument['importKind'];
};

export class DocumentLoadWorkflow {
  public constructor(private readonly options: DocumentLoadWorkflowOptions) {}

  public async load(loadOptions: LoadMarkdownOptions) {
    const generation = this.options.nextLoadGeneration();
    const startedAt = performance.now();
    const guard = this.createStateGuard(generation);

    prepareDocumentLoad(this.options, loadOptions);

    try {
      const content = loadOptions.content ?? '';
      this.options.contentSave.setContentLength(content.length);
      this.options.contentSave.updateLargeDocumentState();
      const loaded = await this.loadCompleteDocument(loadOptions, content, generation, guard);
      if (loaded.loaded) {
        reportDiagnostic(
          'info',
          loaded.importKind === 'blocksuite-json'
            ? `Loaded BlockSuite document JSON (${content.length} characters, ${formatElapsed(startedAt)}).`
            : `Imported Markdown into BlockSuite (${content.length} characters, ${formatElapsed(startedAt)}).`
        );
        this.options.showStatus(`Opened ${loadOptions.title ?? 'document'}.`);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('error', `Could not load document: ${message}`);
      this.options.showStatus(`Could not load document: ${message}`);
      throw error;
    } finally {
      if (!guard.isStaleGeneration(generation)) {
        this.options.setLoadingDocument(false);
      }
    }
  }

  private async loadCompleteDocument(
    loadOptions: LoadMarkdownOptions,
    content: string,
    generation: number,
    guard: DocumentLoadStateGuard
  ): Promise<CompletedDocumentLoad> {
    const loaded = await loadEditorDocumentContent({
      path: loadOptions.path,
      sourcePath: this.options.getCurrentPath(),
      collectionRoot: this.options.getCurrentCollectionRoot(),
      content,
      blobSource: this.options.blobSource,
      onLinkedDoc: linkedDoc => this.options.linkedDocs.register(linkedDoc, generation),
      resolveTitle: relativePath => this.options.linkedDocs.resolveTitle(relativePath),
      readAttachment: this.options.readAttachment,
    });

    if (guard.isStaleGeneration(generation)) {
      return {
        loaded: false,
        importKind: loaded.importKind,
      };
    }

    await assignLoadedDocument(this.options, loaded.doc, loadOptions.title);

    if (guard.isStaleGeneration(generation)) {
      return {
        loaded: false,
        importKind: loaded.importKind,
      };
    }

    const unlocked = await unlockEditorAfterRender(this.options, guard, loaded.operation);
    if (!unlocked) {
      return {
        loaded: false,
        importKind: loaded.importKind,
      };
    }

    deferPostLoadWork(this.options, guard);
    return {
      loaded: true,
      importKind: loaded.importKind,
    };
  }

  private isStaleGeneration(generation: number) {
    return generation !== this.options.getLoadGeneration();
  }

  private createStateGuard(generation: number): DocumentLoadStateGuard {
    return {
      generation,
      isStaleGeneration: currentGeneration => this.isStaleGeneration(currentGeneration),
    };
  }
}
