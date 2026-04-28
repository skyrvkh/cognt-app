import '@toeverything/theme/fonts.css';
import '@toeverything/theme/style.css';
import './styles.css';
import {
  MarkdownBlockSuiteEditor,
  type AttachFileResult,
  type CreateLinkedNoteResult,
} from './blocksuite-editor';
import type { DocumentLinksPayload } from './document-links';
import { installHostDiagnostics, waitForAnimationFrames } from './host';

installHostDiagnostics();

type DotNetReference = {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
};

type BlockSuiteInstance = {
  editor: MarkdownBlockSuiteEditor;
};

const instances = new Map<number, BlockSuiteInstance>();
let nextInstanceId = 1;

export function mountBlockSuite(
  hostElement: HTMLElement,
  statusElement: HTMLElement,
  mentionPreviewElement: HTMLElement,
  dotNet: DotNetReference
) {
  const instanceId = nextInstanceId++;

  window.__cogntPostHost = message => {
    void dotNet.invokeMethodAsync('HandleHostMessage', message);
  };

  const editor = new MarkdownBlockSuiteEditor({
    hostElement,
    statusElement,
    mentionPreviewElement,
    onContentChanged: content => {
      void dotNet.invokeMethodAsync('HandleContentChanged', content);
    },
    onCreateExtract: message => {
      void dotNet.invokeMethodAsync(
        'HandleCreateExtract',
        message.sourcePath,
        message.selectedContent,
        message.selectedPlainText
      );
    },
    onSaveCloze: message => {
      void dotNet.invokeMethodAsync(
        'HandleSaveCloze',
        message.sourcePath,
        message.selectedPlainText,
        message.clozeText,
        message.content
      );
    },
    onSearchMentionTargets: async message =>
      normalizeMentionTargets(
        await dotNet.invokeMethodAsync(
          'HandleSearchMentionTargets',
          message.sourcePath,
          message.query,
          message.limit
        )
      ),
    onRequestMentionPreview: message => {
      void dotNet.invokeMethodAsync(
        'HandleRequestMentionPreview',
        message.requestId,
        message.sourcePath,
        message.relativePath,
        message.mode,
        message.maxCharacters
      );
    },
    onOpenMention: message => {
      void dotNet.invokeMethodAsync(
        'HandleOpenMention',
        message.sourcePath,
        message.relativePath,
        message.content
      );
    },
    onCreateLinkedNote: async message =>
      normalizeCreateLinkedNoteResult(
        await dotNet.invokeMethodAsync(
          'HandleCreateLinkedNote',
          message.sourcePath,
          message.viewMode
        )
      ),
    onAttachFile: async message =>
      normalizeAttachFileResult(
        await dotNet.invokeMethodAsync(
          'HandleAttachFile',
          message.sourcePath,
          message.fileName,
          message.contentBase64
        )
      ),
  });

  instances.set(instanceId, { editor });
  return instanceId;
}

export async function loadNote(
  instanceId: number,
  path: string | null,
  title: string,
  content: string
) {
  const instance = getInstance(instanceId);
  await instance.editor.load({ path: path ?? undefined, title, content });
  await waitForAnimationFrames(2);
}

export async function loadCard(instanceId: number, content: string) {
  const instance = getInstance(instanceId);
  await instance.editor.loadCard(content);
  await waitForAnimationFrames(2);
}

export async function focusEditor(instanceId: number) {
  const instance = getInstance(instanceId);
  await instance.editor.focusEditor();
  await waitForAnimationFrames(1);
}

export function beginCardRender(instanceId: number) {
  const instance = getInstance(instanceId);
  instance.editor.beginCardRender();
}

export async function activateEditor(instanceId: number) {
  const instance = getInstance(instanceId);
  await instance.editor.activateEditorMode();
  await waitForAnimationFrames(1);
}

export function showSaveResult(instanceId: number, ok: boolean, error?: string) {
  const instance = getInstance(instanceId);
  instance.editor.showStatus(ok ? 'Saved.' : `Save failed: ${error ?? 'Unknown error'}`);
}

export function showExtractResult(
  instanceId: number,
  ok: boolean,
  createdPath?: string,
  error?: string
) {
  const instance = getInstance(instanceId);
  void instance.editor.handleExtractResult(ok, createdPath, error);
}

export function setMentionTargets(
  instanceId: number,
  targets: Array<{ relativePath: string; title: string; fileName: string; recentRank: number }>
) {
  const instance = getInstance(instanceId);
  instance.editor.setMentionTargets(targets);
}

export function handleMentionPreviewResult(
  instanceId: number,
  requestId: string,
  relativePath: string,
  mode: string,
  ok: boolean,
  title?: string,
  documentContent?: string,
  error?: string
) {
  const instance = getInstance(instanceId);
  instance.editor.handleMentionPreviewResult({
    requestId,
    relativePath,
    mode: mode === 'full' ? 'full' : 'preview',
    ok,
    title,
    documentContent,
    error,
  });
}

export function handleMentionOpenResult(instanceId: number, ok: boolean, error?: string) {
  const instance = getInstance(instanceId);
  instance.editor.handleMentionOpenResult(ok, error);
}

export function setDocumentLinks(instanceId: number, payload: DocumentLinksPayload) {
  const instance = getInstance(instanceId);
  instance.editor.setDocumentLinks(payload);
}

export function disposeBlockSuite(instanceId: number) {
  const instance = instances.get(instanceId);
  instance?.editor.dispose();
  instances.delete(instanceId);

  if (instances.size === 0) {
    window.__cogntPostHost = undefined;
  }
}

function getInstance(instanceId: number) {
  const instance = instances.get(instanceId);
  if (!instance) {
    throw new Error(`BlockSuite instance ${instanceId} is not mounted.`);
  }

  return instance;
}

function normalizeCreateLinkedNoteResult(value: unknown): CreateLinkedNoteResult {
  const result = value as {
    ok?: boolean;
    Ok?: boolean;
    relativePath?: string;
    RelativePath?: string;
    title?: string;
    Title?: string;
    documentContent?: string;
    DocumentContent?: string;
    error?: string;
    Error?: string;
  } | null;

  return {
    ok: Boolean(result?.ok ?? result?.Ok),
    relativePath: result?.relativePath ?? result?.RelativePath,
    title: result?.title ?? result?.Title,
    documentContent: result?.documentContent ?? result?.DocumentContent,
    error: result?.error ?? result?.Error,
  };
}

function normalizeMentionTargets(value: unknown) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap(item => {
    const target = item as {
      relativePath?: string;
      RelativePath?: string;
      title?: string;
      Title?: string;
      fileName?: string;
      FileName?: string;
      recentRank?: number | null;
      RecentRank?: number | null;
    } | null;

    const relativePath = target?.relativePath ?? target?.RelativePath;
    if (!relativePath) {
      return [];
    }

    return [{
      relativePath,
      title: target?.title ?? target?.Title ?? '',
      fileName: target?.fileName ?? target?.FileName ?? '',
      recentRank: target?.recentRank ?? target?.RecentRank ?? null,
    }];
  });
}

function normalizeAttachFileResult(value: unknown): AttachFileResult {
  const result = value as {
    ok?: boolean;
    Ok?: boolean;
    displayName?: string;
    DisplayName?: string;
    relativePath?: string;
    RelativePath?: string;
    kind?: string;
    Kind?: string;
    error?: string;
    Error?: string;
  } | null;

  return {
    ok: Boolean(result?.ok ?? result?.Ok),
    displayName: result?.displayName ?? result?.DisplayName,
    relativePath: result?.relativePath ?? result?.RelativePath,
    kind: result?.kind ?? result?.Kind,
    error: result?.error ?? result?.Error,
  };
}
