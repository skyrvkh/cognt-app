import type { TextSelection } from '../cloze';
import type { EditorCaretState } from './contracts';

export function createCaretStateFromTextSelection(
  textSelection: TextSelection
): EditorCaretState | null {
  const point = textSelection.end ?? textSelection.to ?? textSelection.start ?? textSelection.from;
  const blockId = point?.blockId?.trim() ?? '';
  if (!blockId) {
    return null;
  }

  return {
    blockId,
    index: Math.max(0, point.index + Math.max(0, point.length)),
    length: 0,
  };
}

export function normalizeCaretState(
  state?: EditorCaretState | null
): EditorCaretState | null {
  const blockId = typeof state?.blockId === 'string' ? state.blockId.trim() : '';
  if (!blockId) {
    return null;
  }

  const index = typeof state?.index === 'number' && Number.isFinite(state.index)
    ? state.index
    : 0;
  const length = typeof state?.length === 'number' && Number.isFinite(state.length)
    ? state.length
    : 0;
  return {
    blockId,
    index: Math.max(0, Math.trunc(index)),
    length: Math.max(0, Math.trunc(length)),
  };
}
