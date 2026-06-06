import { focusTextModel } from '@blocksuite/affine-components/rich-text';
import {
  getTextBlockLength,
  isTextBlockModel,
} from './block-models';
import {
  createCaretStateFromTextSelection,
  normalizeCaretState,
} from './caret-state';
import type { EditorCaretState } from './contracts';
import { flushEditorHost } from './host-operations';
import type {
  EditorInteractionCaretState,
  EditorInteractionOptions,
} from './editor-interaction-types';
import { createTextSelectionFromNativeRange } from './native-selection';
import { scrollBlockIntoView } from './editor-interaction-focus';
import type { TextSelection } from '../cloze';

export function clearCachedTextSelection(state: EditorInteractionCaretState) {
  state.setLastKnownTextSelectionForPaste(null);
}

export function captureNativeTextSelectionForPaste(
  options: EditorInteractionOptions,
  state: EditorInteractionCaretState
) {
  const textSelection = createTextSelectionFromNativeRange(options.editor);
  if (textSelection) {
    state.setLastKnownTextSelectionForPaste(textSelection);
  }
}

export function getCaretState(
  options: EditorInteractionOptions,
  state: EditorInteractionCaretState
): EditorCaretState | null {
  captureNativeTextSelectionForPaste(options, state);

  const doc = options.getCurrentDoc();
  const textSelection = options.getTextSelection() ?? state.lastKnownTextSelectionForPaste;
  const caret = textSelection ? createCaretStateFromTextSelection(textSelection) : null;
  if (caret && doc?.getBlock(caret.blockId)) {
    return caret;
  }

  const model = options.getCurrentInsertionBlockModel();
  const blockId = typeof (model as { id?: unknown } | null)?.id === 'string'
    ? (model as { id: string }).id
    : '';
  if (!blockId || !doc?.getBlock(blockId)) {
    return null;
  }

  return {
    blockId,
    index: Math.max(0, getTextBlockLength(model)),
    length: 0,
  };
}

export async function restoreCaretState(
  options: EditorInteractionOptions,
  state: EditorInteractionCaretState,
  caretState?: EditorCaretState | null
) {
  const normalized = normalizeCaretState(caretState);
  const doc = options.getCurrentDoc();
  if (!normalized || !doc?.getBlock(normalized.blockId)) {
    return;
  }

  const editor = options.editor;
  await flushEditorHost(editor);
  const model = doc.getBlock(normalized.blockId)?.model;
  const textLength = getTextBlockLength(model);
  const index = Math.min(normalized.index, textLength);
  const length = Math.min(normalized.length, Math.max(0, textLength - index));
  const selection = editor.host?.selection.create('text', {
    from: {
      blockId: normalized.blockId,
      index,
      length,
    },
    to: null,
  });
  if (!selection) {
    return;
  }

  editor.focus();
  editor.host?.selection.setGroup('note', [selection as never]);
  state.setLastKnownTextSelectionForPaste(selection as TextSelection);
  if (length === 0 && !state.readOnlyMode && isTextBlockModel(model)) {
    focusTextModel(editor.std, normalized.blockId, index);
  }

  await scrollBlockIntoView(options, normalized.blockId);
}
