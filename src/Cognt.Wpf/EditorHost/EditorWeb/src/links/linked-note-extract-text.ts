import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import { createDefaultDoc } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import type {
  ExtractTextReplacement,
  ExtractTextReplacementPoint,
  ExtractTextRangeReplacement,
} from '../editor/extract-replacement-types';
import { reportDiagnostic } from '../host';
import {
  clampTextIndex,
  getBlockModelId,
  getEditableTextBlock,
  getEditableTextLength,
  getUniqueBlockIds,
  isEmptyEditableTextBlock,
} from './linked-note-editable-text';
import { getLinkedNoteTitle } from './linked-note-paths';
import {
  createIconOnlyLinkedPageReference,
  createLinkedPageReference,
} from './linked-note-reference';

type TextLinkReplacement = ExtractTextReplacement | ExtractTextRangeReplacement;

export function replaceExtractTextRangeWithLinkedPage(
  doc: Doc,
  range: TextLinkReplacement,
  relativePath: string,
  title: string
): Doc {
  reportDiagnostic(
    'info',
    `Replacing extract text range with linked page (kind=${range.kind}, relativePath=${relativePath}, titleLength=${title.length}).`
  );
  if (range.kind === 'text-range') {
    return replaceExtractTextSpanWithLinkedPage(doc, range, relativePath, title);
  }

  const model = doc.getBlockById(range.blockId) as
    | {
        text?: {
          replace(
            index: number,
            length: number,
            content: string,
            attributes?: Record<string, unknown>
          ): void;
        };
      }
    | null;
  if (!model?.text) {
    reportDiagnostic('error', `Extract text replacement failed: editable text block missing (block=${range.blockId}).`);
    throw new Error('selected block no longer has editable text');
  }

  const linkedDoc = createDefaultDoc(doc.collection, { title });
  const reference = createLinkedPageReference(linkedDoc.id, relativePath, title);

  doc.transact(() => {
    model.text!.replace(range.index, range.length, REFERENCE_NODE, {
      reference,
    });
  });

  reportDiagnostic(
    'info',
    `Extract text range replaced with linked page (block=${range.blockId}, index=${range.index}, length=${range.length}, pageId=${linkedDoc.id}).`
  );
  return linkedDoc;
}

export function insertExtractTextRangeLinkedPageIcon(
  doc: Doc,
  range: TextLinkReplacement,
  relativePath: string,
  title: string
): Doc {
  reportDiagnostic(
    'info',
    `Inserting extract text range linked-page icon (kind=${range.kind}, relativePath=${relativePath}, titleLength=${title.length}).`
  );
  if (range.kind === 'text-range') {
    return insertExtractTextSpanLinkedPageIcon(doc, range, relativePath, title);
  }

  const model = getEditableTextBlock(doc, range.blockId);
  if (!model?.text) {
    reportDiagnostic('error', `Extract text icon insertion failed: editable text block missing (block=${range.blockId}).`);
    throw new Error('selected block no longer has editable text');
  }

  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });
  const reference = createIconOnlyLinkedPageReference(linkedDoc.id, relativePath, titleText);
  const insertIndex = clampTextIndex(
    range.index + Math.max(0, range.length),
    getEditableTextLength(model)
  );

  doc.transact(() => {
    model.text!.replace(insertIndex, 0, REFERENCE_NODE, { reference });
  });

  reportDiagnostic(
    'info',
    `Extract text range linked-page icon inserted (block=${range.blockId}, insertIndex=${insertIndex}, pageId=${linkedDoc.id}).`
  );
  return linkedDoc;
}

function replaceExtractTextSpanWithLinkedPage(
  doc: Doc,
  range: ExtractTextRangeReplacement,
  relativePath: string,
  title: string
): Doc {
  reportDiagnostic(
    'info',
    `Replacing extract text span with linked page (blocks=${range.blockIds.length}, relativePath=${relativePath}).`
  );
  const startModel = getEditableTextBlock(doc, range.start.blockId);
  const endModel = getEditableTextBlock(doc, range.end.blockId);
  if (!startModel || !endModel) {
    reportDiagnostic('error', 'Extract text-span replacement failed: range blocks no longer have editable text.');
    throw new Error('selected range blocks no longer have editable text');
  }

  if (startModel === endModel) {
    const index = Math.min(range.start.index, range.end.index);
    const selectionEnd = Math.max(
      getSelectionPointEndOffset(range.start),
      getSelectionPointEndOffset(range.end)
    );
    return replaceExtractTextRangeWithLinkedPage(
      doc,
      {
        kind: 'text',
        blockId: range.start.blockId,
        index,
        length: Math.max(0, selectionEnd - index),
      },
      relativePath,
      title
    );
  }

  const parent = doc.getParent(startModel as never) as { children?: unknown[] } | null;
  const endParent = doc.getParent(endModel as never);
  const siblings = Array.isArray(parent?.children) ? parent.children : [];
  const startIndex = siblings.indexOf(startModel);
  const endIndex = siblings.indexOf(endModel);
  if (!parent || parent !== endParent || startIndex < 0 || endIndex < 0) {
    reportDiagnostic('error', 'Extract text-span replacement failed: selected blocks are not siblings.');
    throw new Error('range replacement supports selected text across sibling blocks only');
  }

  const firstPoint = startIndex <= endIndex ? range.start : range.end;
  const lastPoint = startIndex <= endIndex ? range.end : range.start;
  const firstModel = startIndex <= endIndex ? startModel : endModel;
  const lastModel = startIndex <= endIndex ? endModel : startModel;
  const firstIndex = Math.min(startIndex, endIndex);
  const lastIndex = Math.max(startIndex, endIndex);
  const selectedIds = new Set(getUniqueBlockIds([
    firstPoint.blockId,
    ...range.blockIds,
    lastPoint.blockId,
  ]));
  const selectedMiddleModels = siblings
    .slice(firstIndex + 1, lastIndex)
    .filter(model => selectedIds.has(getBlockModelId(model)));

  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });
  const reference = createLinkedPageReference(linkedDoc.id, relativePath, titleText);

  doc.transact(() => {
    const firstTextLength = getEditableTextLength(firstModel);
    const firstReplaceIndex = clampTextIndex(firstPoint.index, firstTextLength);
    firstModel.text!.replace(
      firstReplaceIndex,
      Math.max(0, firstTextLength - firstReplaceIndex),
      REFERENCE_NODE,
      { reference }
    );

    const lastTextLength = getEditableTextLength(lastModel);
    const lastDeleteLength = clampTextIndex(
      getSelectionPointEndOffset(lastPoint),
      lastTextLength
    );
    lastModel.text!.replace(0, lastDeleteLength, '');

    for (const model of [...selectedMiddleModels].reverse()) {
      doc.deleteBlock(model as never, { deleteChildren: true });
    }

    if (isEmptyEditableTextBlock(lastModel)) {
      doc.deleteBlock(lastModel as never, { deleteChildren: true });
    }
  });

  reportDiagnostic(
    'info',
    `Extract text span replaced with linked page (relativePath=${relativePath}, pageId=${linkedDoc.id}, deletedMiddleBlocks=${selectedMiddleModels.length}).`
  );
  return linkedDoc;
}

function insertExtractTextSpanLinkedPageIcon(
  doc: Doc,
  range: ExtractTextRangeReplacement,
  relativePath: string,
  title: string
): Doc {
  reportDiagnostic(
    'info',
    `Inserting extract text span linked-page icon (blocks=${range.blockIds.length}, relativePath=${relativePath}).`
  );
  const startModel = getEditableTextBlock(doc, range.start.blockId);
  const endModel = getEditableTextBlock(doc, range.end.blockId);
  if (!startModel || !endModel) {
    reportDiagnostic('error', 'Extract text-span icon insertion failed: range blocks no longer have editable text.');
    throw new Error('selected range blocks no longer have editable text');
  }

  if (startModel === endModel) {
    const index = Math.min(range.start.index, range.end.index);
    const selectionEnd = Math.max(
      getSelectionPointEndOffset(range.start),
      getSelectionPointEndOffset(range.end)
    );
    return insertExtractTextRangeLinkedPageIcon(
      doc,
      {
        kind: 'text',
        blockId: range.start.blockId,
        index,
        length: Math.max(0, selectionEnd - index),
      },
      relativePath,
      title
    );
  }

  const parent = doc.getParent(startModel as never) as { children?: unknown[] } | null;
  const endParent = doc.getParent(endModel as never);
  const siblings = Array.isArray(parent?.children) ? parent.children : [];
  const startIndex = siblings.indexOf(startModel);
  const endIndex = siblings.indexOf(endModel);
  if (!parent || parent !== endParent || startIndex < 0 || endIndex < 0) {
    reportDiagnostic('error', 'Extract text-span icon insertion failed: selected blocks are not siblings.');
    throw new Error('range icon insertion supports selected text across sibling blocks only');
  }

  const lastPoint = startIndex <= endIndex ? range.end : range.start;
  const lastModel = startIndex <= endIndex ? endModel : startModel;
  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });
  const reference = createIconOnlyLinkedPageReference(linkedDoc.id, relativePath, titleText);
  const insertIndex = clampTextIndex(
    getSelectionPointEndOffset(lastPoint),
    getEditableTextLength(lastModel)
  );

  doc.transact(() => {
    lastModel.text!.replace(insertIndex, 0, REFERENCE_NODE, { reference });
  });

  reportDiagnostic(
    'info',
    `Extract text span linked-page icon inserted (relativePath=${relativePath}, pageId=${linkedDoc.id}, insertIndex=${insertIndex}).`
  );
  return linkedDoc;
}

function getSelectionPointEndOffset(point: ExtractTextReplacementPoint) {
  return point.index + Math.max(0, point.length);
}
