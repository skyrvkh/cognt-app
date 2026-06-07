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
