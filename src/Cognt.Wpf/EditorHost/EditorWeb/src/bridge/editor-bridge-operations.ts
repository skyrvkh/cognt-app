import type { CogntEditorTheme } from '../editor-theme';
import { waitForAnimationFrames } from '../host';
import type { DocumentLinksPayload } from '../document-links';
import { normalizeCaretState, runEditorOperation, runEditorOperationSync } from './editor-bridge-runtime';

async function runEditorAction<TResult>(
  instanceId: number,
  operation: string,
  action: (editor: import('../blocksuite-editor').MarkdownBlockSuiteEditor) => Promise<TResult> | TResult,
  animationFrames = 0
) {
  return await runEditorOperation(instanceId, operation, async instance => {
    const result = await action(instance.editor);
    if (animationFrames > 0) {
      await waitForAnimationFrames(animationFrames);
    }

    return result;
  });
}

function runEditorActionSync<TResult>(
  instanceId: number,
  operation: string,
  action: (editor: import('../blocksuite-editor').MarkdownBlockSuiteEditor) => TResult
) {
  return runEditorOperationSync(instanceId, operation, instance => action(instance.editor));
}

export async function loadNote(
  instanceId: number,
  path: string | null,
  title: string,
  content: string,
  collectionRoot?: string | null
) {
  await runEditorAction(
    instanceId,
    `load note (path=${path ?? ''}, title=${title}, length=${content.length})`,
    editor =>
      editor.load({
        path: path ?? undefined,
        title,
        content,
        collectionRoot: collectionRoot ?? undefined,
      }),
    2
  );
}

export async function focusEditor(instanceId: number) {
  await runEditorAction(instanceId, 'focus editor', editor => editor.focusEditor(), 1);
}

export async function exportContent(instanceId: number) {
  return await runEditorAction(instanceId, 'export content', editor =>
    editor.exportContentForHost()
  );
}

export async function focusBlock(instanceId: number, blockId: string) {
  await runEditorAction(
    instanceId,
    `focus block (block=${blockId})`,
    editor => editor.focusBlock(blockId)
  );
}

export function getCaretState(instanceId: number) {
  return runEditorActionSync(instanceId, 'get caret state', editor =>
    editor.getCaretState()
  );
}

export async function restoreCaretState(
  instanceId: number,
  state?: { blockId?: string; index?: number; length?: number } | null
) {
  await runEditorAction(
    instanceId,
    'restore caret state',
    editor => editor.restoreCaretState(normalizeCaretState(state)),
    1
  );
}

export function getSelectedImageBlock(instanceId: number) {
  return runEditorActionSync(instanceId, 'get selected image block', editor =>
    editor.getSelectedImageBlock()
  );
}

export function getSelectedCardContextBlocks(instanceId: number) {
  return runEditorActionSync(instanceId, 'get selected card context blocks', editor =>
    editor.getSelectedCardContextBlocks()
  );
}

export async function createClozeFromSelection(
  instanceId: number,
  cardContextId?: string | null,
  slotId?: string | null,
  displayClozeIndex?: number | null
) {
  await runEditorAction(
    instanceId,
    `create cloze from selection (context=${cardContextId ?? ''}, slot=${slotId ?? ''}, index=${displayClozeIndex ?? ''})`,
    editor => editor.createClozeFromSelection(cardContextId, slotId, displayClozeIndex),
    1
  );
}

export async function activateEditor(instanceId: number) {
  await runEditorAction(instanceId, 'activate editor', editor => editor.activateEditorMode(), 1);
}

export async function setReadOnlyMode(instanceId: number, isReadOnly: boolean) {
  await runEditorAction(
    instanceId,
    `set read-only mode (${isReadOnly})`,
    editor => editor.setReadOnlyMode(isReadOnly),
    1
  );
}

export function applyTheme(instanceId: number, theme: CogntEditorTheme) {
  runEditorActionSync(
    instanceId,
    `apply theme (${theme.name ?? 'unnamed'})`,
    editor => editor.applyTheme(theme)
  );
}

export function showSaveResult(instanceId: number, ok: boolean, error?: string) {
  runEditorActionSync(instanceId, `show save result (ok=${ok})`, editor =>
    editor.showStatus(ok ? 'Saved.' : `Save failed: ${error ?? 'Unknown error'}`)
  );
}

export function showExtractResult(
  instanceId: number,
  ok: boolean,
  createdPath?: string,
  error?: string
) {
  void runEditorAction(
    instanceId,
    `handle extract result (ok=${ok}, path=${createdPath ?? ''})`,
    editor => editor.handleExtractResult(ok, createdPath, error)
  );
}

export function setMentionTargets(
  instanceId: number,
  targets: Array<{ relativePath: string; title: string }>
) {
  runEditorActionSync(instanceId, `set mention targets (count=${targets.length})`, editor =>
    editor.setMentionTargets(targets)
  );
}

export function setDocumentLinks(instanceId: number, payload: DocumentLinksPayload) {
  runEditorActionSync(
    instanceId,
    `set document links (incoming=${payload.incoming?.length ?? 0}, outgoing=${payload.outgoing?.length ?? 0})`,
    editor => editor.setDocumentLinks(payload)
  );
}

export function handleMentionPreviewResult(
  instanceId: number,
  requestId: string,
  relativePath: string,
  mode: string,
  ok: boolean,
  documentContent?: string,
  error?: string
) {
  runEditorActionSync(
    instanceId,
    `handle mention preview result (request=${requestId}, ok=${ok}, path=${relativePath})`,
    editor =>
      editor.handleMentionPreviewResult({
        requestId,
        relativePath,
        mode: mode === 'full' ? 'full' : 'preview',
        ok,
        documentContent,
        error,
      })
  );
}

export function handleMentionOpenResult(instanceId: number, ok: boolean, error?: string) {
  runEditorActionSync(instanceId, `handle mention open result (ok=${ok})`, editor =>
    editor.handleMentionOpenResult(ok, error)
  );
}
