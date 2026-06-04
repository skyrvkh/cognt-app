import type { TextSelection } from '../cloze';
import { reportDiagnostic } from '../host';
import type {
  TextSelectionPointSnapshot,
  TextSelectionSnapshot,
} from './selection-export-types';

export function getOrderedTextSelectionPoints(textSelection: TextSelection) {
  const points = {
    start: cloneTextSelectionPoint(getTextSelectionStart(textSelection)),
    end: cloneTextSelectionPoint(getTextSelectionEnd(textSelection)),
  };
  reportDiagnostic(
    'info',
    `Ordered text selection points created (start=${points.start.blockId}:${points.start.index}/${points.start.length}, end=${points.end.blockId}:${points.end.index}/${points.end.length}).`
  );
  return points;
}

export function cloneTextSelection(selection: TextSelection): TextSelectionSnapshot {
  const snapshot = {
    from: cloneTextSelectionPoint(selection.from),
    to: selection.to ? cloneTextSelectionPoint(selection.to) : null,
    start: selection.start ? cloneTextSelectionPoint(selection.start) : undefined,
    end: selection.end ? cloneTextSelectionPoint(selection.end) : undefined,
  };
  reportDiagnostic('info', `Text selection cloned (from=${snapshot.from.blockId}, hasTo=${Boolean(snapshot.to)}).`);
  return snapshot;
}

export function getTextSelectionSnapshotRange(selection: TextSelectionSnapshot) {
  const start = selection.start ?? selection.from;
  const end = selection.end ?? selection.to ?? selection.from;

  if (start.blockId !== end.blockId) {
    reportDiagnostic(
      'info',
      `Text selection snapshot range skipped: blocks differ (start=${start.blockId}, end=${end.blockId}).`
    );
    return null;
  }

  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    start.index + Math.max(0, start.length),
    end.index + Math.max(0, end.length)
  );
  const length = selectionEnd - index;

  const range = length > 0
    ? {
        blockId: start.blockId,
        index,
        length,
      }
    : null;

  reportDiagnostic(
    'info',
    `Text selection snapshot range resolved (hasRange=${Boolean(range)}, block=${start.blockId}, index=${index}, length=${length}).`
  );
  return range;
}

export function getTextSelectionStart(textSelection: TextSelection) {
  return textSelection.start
    ?? (isReverseTextSelection(textSelection) ? (textSelection.to ?? textSelection.from) : textSelection.from);
}

export function getTextSelectionEnd(textSelection: TextSelection) {
  return textSelection.end
    ?? (isReverseTextSelection(textSelection) ? textSelection.from : (textSelection.to ?? textSelection.from));
}

export function getSameBlockSelectionRange(
  start: { index: number; length: number },
  end: { index: number; length: number }
) {
  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    getSelectionPointEndOffset(start),
    getSelectionPointEndOffset(end)
  );

  return {
    index,
    length: Math.max(0, selectionEnd - index),
  };
}

export function getSelectionPointEndOffset(point: { index: number; length: number }) {
  return point.index + Math.max(0, point.length);
}

function cloneTextSelectionPoint(point: TextSelectionPointSnapshot): TextSelectionPointSnapshot {
  return {
    blockId: point.blockId,
    index: point.index,
    length: point.length,
  };
}

function isReverseTextSelection(textSelection: TextSelection) {
  return Boolean((textSelection as TextSelection & { reverse?: boolean }).reverse);
}
