import { focusTextModel } from '@blocksuite/affine-components/rich-text';
import { isTextBlockModel } from './block-models';
import {
  findEditableElement,
  waitForNextAnimationFrame,
} from './dom';
import type { EditorInteractionOptions } from './editor-interaction-types';

export async function focusEditor(
  options: EditorInteractionOptions,
  activateEditorMode: () => Promise<void>
) {
  await activateEditorMode();

  const editor = options.editor;
  const target =
    findEditableElement(editor) ??
    findEditableElement(editor.shadowRoot) ??
    findEditableElement(document);

  editor.focus();
  target?.focus({ preventScroll: true });
}

export async function focusBlock(
  options: EditorInteractionOptions,
  readOnlyMode: boolean,
  blockId: string
) {
  const targetBlockId = options.resolveOutlineBlockId(blockId);
  const doc = options.getCurrentDoc();
  if (!targetBlockId || !doc) {
    options.showStatus('The selected heading is no longer available.');
    return;
  }

  const editor = options.editor;
  editor.focus();
  const model = doc.getBlock(targetBlockId)?.model;
  if (!readOnlyMode && isTextBlockModel(model)) {
    focusTextModel(editor.std, targetBlockId, 0);
  } else {
    editor.host?.selection.setGroup('note', [
      editor.host.selection.create('block', { blockId: targetBlockId }),
    ]);
  }

  await scrollBlockIntoView(options, targetBlockId);
  options.showStatus('Moved to heading.');
}

export async function scrollBlockIntoView(
  options: Pick<EditorInteractionOptions, 'getRenderedBlockElement'>,
  blockId: string
) {
  await waitForNextAnimationFrame();
  options.getRenderedBlockElement(blockId)?.scrollIntoView({
    block: 'center',
    inline: 'nearest',
    behavior: 'auto',
  });
}
