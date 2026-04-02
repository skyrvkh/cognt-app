import '@toeverything/theme/fonts.css';
import '@toeverything/theme/style.css';
import './styles.css';
import { MarkdownBlockSuiteEditor } from './blocksuite-editor';
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
        message.selectedMarkdown,
        message.selectedPlainText
      );
    },
    onSaveCloze: message => {
      void dotNet.invokeMethodAsync(
        'HandleSaveCloze',
        message.sourcePath,
        message.selectedPlainText,
        message.clozeText,
        message.markdown
      );
    },
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
        message.markdown
      );
    },
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

export async function loadCard(instanceId: number, markdown: string) {
  const instance = getInstance(instanceId);
  await instance.editor.loadCard(markdown);
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
  instance.editor.showStatus(
    ok
      ? `Created ${createdPath?.split(/[\\/]/).pop() ?? 'extract file'}.`
      : `Extract failed: ${error ?? 'Unknown error'}`
  );
}

export function setMentionTargets(
  instanceId: number,
  targets: Array<{ relativePath: string; fileName: string; recentRank: number }>
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
  markdownExcerpt?: string,
  error?: string
) {
  const instance = getInstance(instanceId);
  instance.editor.handleMentionPreviewResult({
    requestId,
    relativePath,
    mode: mode === 'full' ? 'full' : 'preview',
    ok,
    title,
    markdownExcerpt,
    error,
  });
}

export function handleMentionOpenResult(instanceId: number, ok: boolean, error?: string) {
  const instance = getInstance(instanceId);
  instance.editor.handleMentionOpenResult(ok, error);
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
