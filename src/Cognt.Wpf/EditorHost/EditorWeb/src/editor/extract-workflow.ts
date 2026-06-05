import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  type ExtractSourceAction,
  type PendingExtract,
  runCreateExtractCommand,
} from '../commands';
import { reportDiagnostic } from '../host';
import {
  getExtractTitleFromRelPath,
  getLinkedNoteTitle,
  insertExtractBlocksLinkedPageIcon,
  insertExtractTextRangeLinkedPageIcon,
  normalizeCreatedExtractPath,
  replaceExtractBlocksWithLinkedPage,
  replaceExtractTextRangeWithLinkedPage,
} from '../links/linked-notes';

type ExtractWorkflowOptions = {
  editor: PageEditor;
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  getTextSelection(): TextSelection | null | undefined;
  getBlockSelectionBlockIds(): string[];
  createExtract: Parameters<typeof runCreateExtractCommand>[0]['createExtract'];
  resolveLinkedNoteTitle(relativePath: string): string | undefined;
  trackLinkedDoc(doc: Doc, relativePath: string): void;
  requestLinkedDocPreview(doc: Doc, relativePath: string, generation: number): void;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  schedulePostExtractSourceSave(): void;
  showStatus(message: string): void;
};

export class ExtractWorkflow {
  private pendingExtract: PendingExtract | null = null;
  private suppressNextNativeCut = false;

  public constructor(private readonly options: ExtractWorkflowOptions) {}

  public consumeSuppressedNativeCut() {
    if (!this.suppressNextNativeCut) {
      return false;
    }

    this.suppressNextNativeCut = false;
    return true;
  }

  public async handleShortcut(
    event: KeyboardEvent,
    sourceAction: ExtractSourceAction
  ) {
    const suppressNativeCut = sourceAction === 'copy';
    if (suppressNativeCut) {
      this.suppressNextNativeCut = true;
      window.setTimeout(() => {
        this.suppressNextNativeCut = false;
      }, 250);
    }

    try {
      this.pendingExtract = await runCreateExtractCommand(
        {
          isInputLocked: () => this.options.isInputLocked(),
          getCurrentDoc: () => this.options.getCurrentDoc(),
          getCurrentPath: () => this.options.getCurrentPath(),
          getLoadGeneration: () => this.options.getLoadGeneration(),
          getEditor: () => this.options.editor,
          getTextSelection: () => this.options.getTextSelection(),
          getBlockSelectionBlockIds: () => this.options.getBlockSelectionBlockIds(),
          createExtract: message => this.options.createExtract(message),
          showStatus: message => this.options.showStatus(message),
        },
        event,
        sourceAction
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Extract preparation failed: ${message}`);
      this.options.showStatus(`Extract failed: ${message}`);
      this.pendingExtract = null;
    }

    if (!this.pendingExtract && suppressNativeCut) {
      this.suppressNextNativeCut = false;
    }
  }

  public async handleResult(ok: boolean, createdPath?: string, error?: string) {
    const pending = this.pendingExtract;
    this.pendingExtract = null;

    if (!ok) {
      this.options.showStatus(`Extract failed: ${error ?? 'Unknown error'}`);
      return;
    }

    const relativePath = normalizeCreatedExtractPath(createdPath);
    if (!relativePath) {
      this.options.showStatus('Extract created, but the new note path was not returned.');
      return;
    }

    const currentDoc = this.options.getCurrentDoc();
    if (
      !pending ||
      pending.generation !== this.options.getLoadGeneration() ||
      pending.sourcePath !== this.options.getCurrentPath() ||
      !currentDoc
    ) {
      this.options.showStatus(`Created ${relativePath.split(/[\\/]/).pop() ?? 'extract file'}.`);
      return;
    }

    try {
      const title = getLinkedNoteTitle(
        relativePath,
        this.options.resolveLinkedNoteTitle(relativePath) ?? getExtractTitleFromRelPath(relativePath)
      );
      const linkedDoc = this.applyExtractSourceLink(currentDoc, pending, relativePath, title);
      this.options.trackLinkedDoc(linkedDoc, relativePath);
      this.options.requestLinkedDocPreview(linkedDoc, relativePath, pending.generation);

      this.options.editor.host?.selection.clear(['text']);
      window.getSelection()?.removeAllRanges();
      this.options.cancelPendingContentChanged();
      this.options.recordUserEdit();
      this.options.schedulePostExtractSourceSave();
      this.options.showStatus(
        pending.sourceAction === 'copy'
          ? `Copied extract to ${title}.`
          : `Extracted to ${title}.`
      );
    } catch (replacementError) {
      const message = replacementError instanceof Error
        ? replacementError.message
        : String(replacementError);
      reportDiagnostic('warn', `Extract created but source link replacement failed: ${message}`);
      this.options.showStatus(`Extract created, but source text was not replaced: ${message}`);
    }
  }

  private applyExtractSourceLink(
    currentDoc: Doc,
    pending: PendingExtract,
    relativePath: string,
    title: string
  ) {
    if (pending.sourceAction === 'copy') {
      return pending.replacement.kind === 'blocks'
        ? insertExtractBlocksLinkedPageIcon(
          currentDoc,
          pending.replacement.blockIds,
          relativePath,
          title)
        : insertExtractTextRangeLinkedPageIcon(
          currentDoc,
          pending.replacement,
          relativePath,
          title);
    }

    return pending.replacement.kind === 'blocks'
      ? replaceExtractBlocksWithLinkedPage(
        currentDoc,
        pending.replacement.blockIds,
        relativePath,
        title)
      : replaceExtractTextRangeWithLinkedPage(
        currentDoc,
        pending.replacement,
        relativePath,
        title);
  }
}
