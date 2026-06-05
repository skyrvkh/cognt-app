import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import type {
  ExtractReplacement,
  ExtractTextReplacement,
  ExtractTextReplacementPoint,
} from '../editor/extract-replacement-types';
import { getOrderedTextSelectionPoints } from '../documents/selection-export';
import { reportDiagnostic } from '../host';
import type { CreateExtractCommandHost } from './create-extract-types';

export function createTextSelectionReplacement(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  textSelection: TextSelection,
  selectedBlockIds: string[]
): ExtractReplacement | null {
  const points = getOrderedTextSelectionPoints(textSelection);

  if (points.start.blockId === points.end.blockId) {
    const replacement = getTextSelectionReplacement(points);
    if (!replacement) {
      host.showStatus('Ctrl+X needs selected text.');
      reportDiagnostic('info', 'Extract cancelled: same-block text selection had no replaceable length.');
      return null;
    }

    return replacement;
  }

  const textRangeSelection = getReplaceableBlockSelection(
    currentDoc,
    [
      points.start.blockId,
      ...selectedBlockIds,
      points.end.blockId,
    ]
  );
  if (textRangeSelection.error) {
    const message = textRangeSelection.error === blockParentMismatchMessage
      ? 'Extract text ranges across sibling blocks only.'
      : textRangeSelection.error;
    host.showStatus(message);
    reportDiagnostic('info', `Extract cancelled: ${message}`);
    return null;
  }

  if (textRangeSelection.blockIds.length < 2) {
    host.showStatus('Ctrl+X needs selected text or a selected block.');
    reportDiagnostic('info', 'Extract cancelled: cross-block text range resolved to fewer than two blocks.');
    return null;
  }

  return {
    kind: 'text-range',
    start: points.start,
    end: points.end,
    blockIds: textRangeSelection.blockIds,
  };
}

export function getTextSelectionReplacement(
  points: {
    start: ExtractTextReplacementPoint;
    end: ExtractTextReplacementPoint;
  }
): ExtractTextReplacement | null {
  const { start, end } = points;
  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(
    getSelectionPointEndOffset(start),
    getSelectionPointEndOffset(end)
  );
  const length = selectionEnd - index;

  return length > 0
    ? {
        kind: 'text',
        blockId: start.blockId,
        index,
        length,
      }
    : null;
}

export const blockParentMismatchMessage = 'Extract selected blocks from one parent at a time.';

export function getReplaceableBlockSelection(doc: Doc, blockIds: string[]) {
  const uniqueBlockIds = getUniqueBlockIds(blockIds);
  const entries = uniqueBlockIds.flatMap(blockId => {
    const model = doc.getBlockById(blockId);
    const parent = model ? doc.getParent(model) : null;
    return model && parent ? [{ blockId, model, parent }] : [];
  });

  if (entries.length !== uniqueBlockIds.length) {
    return {
      blockIds: [],
      error: 'One of the selected blocks is no longer available.',
    };
  }

  const [firstEntry] = entries;
  if (firstEntry && entries.some(entry => entry.parent !== firstEntry.parent)) {
    return {
      blockIds: [],
      error: blockParentMismatchMessage,
    };
  }

  return {
    blockIds: uniqueBlockIds,
    error: null,
  };
}

function getSelectionPointEndOffset(point: { index: number; length: number }) {
  return point.index + Math.max(0, point.length);
}

function getUniqueBlockIds(values: string[]) {
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    const blockId = value.trim();
    if (!blockId || seen.has(blockId)) {
      continue;
    }

    seen.add(blockId);
    ids.push(blockId);
  }

  return ids;
}
