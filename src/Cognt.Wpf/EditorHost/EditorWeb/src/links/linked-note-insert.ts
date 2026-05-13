import {
  createDefaultDoc,
  type AffineSlashMenuContext,
} from '@blocksuite/blocks';
import {
  getInlineEditorByModel,
  REFERENCE_NODE,
} from '@blocksuite/affine-components/rich-text';
import type { Doc } from '@blocksuite/store';
import type { CogntSlashMenuRootComponent } from '../blocksuite-compat';
import {
  cogntLinkedNotePathProp,
  defaultCogntEmbedStyle,
} from './linked-note-types';
import {
  createCogntLinkedNoteParams,
  getLinkedNoteTitle,
} from './linked-note-paths';
import {
  createLinkedPageReference,
  updateLinkedPageReference,
} from './linked-note-reference';
import {
  getBlockText,
  getDeltaReference,
  walkBlockModels,
} from './linked-note-block-text';

export type ExtractTextReplacementRange = {
  kind?: 'text';
  blockId: string;
  index: number;
  length: number;
};

export type ExtractTextRangeReplacement = {
  kind: 'text-range';
  start: ExtractTextReplacementPoint;
  end: ExtractTextReplacementPoint;
  blockIds: string[];
};

export type ExtractTextReplacementPoint = {
  blockId: string;
  index: number;
  length: number;
};

export type ExtractTextReplacement =
  | ExtractTextReplacementRange
  | ExtractTextRangeReplacement;

export function insertCogntLinkedNoteEmbed(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
  const { model } = context;
  const parent = rootComponent.doc.getParent(model);

  if (!parent) {
    throw new Error('Could not find the insertion block.');
  }

  const index = parent.children.indexOf(model);
  const title = getLinkedNoteTitle(relativePath, requestedTitle);
  const linkedDoc = createDefaultDoc(rootComponent.doc.collection, { title });
  const blockId = rootComponent.doc.addBlock(
    'affine:embed-linked-doc' as never,
    {
      pageId: linkedDoc.id,
      params: createCogntLinkedNoteParams(relativePath),
      style: defaultCogntEmbedStyle,
      caption: null,
      title,
      description: relativePath,
      [cogntLinkedNotePathProp]: relativePath,
    } as never,
    parent,
    index + 1
  );

  if (model.text?.length === 0 && model.children.length === 0) {
    rootComponent.doc.deleteBlock(model);
  }

  rootComponent.host.selection.setGroup('note', [
    rootComponent.host.selection.create('block', { blockId }),
  ]);

  return linkedDoc;
}

export function insertCogntLinkedNoteInline(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
  const { model } = context;
  const inlineEditor = getInlineEditorByModel(rootComponent.host, model);

  if (!inlineEditor) {
    throw new Error('Could not find the inline editor for the insertion block.');
  }

  const inlineRange = inlineEditor.getInlineRange();
  if (!inlineRange) {
    throw new Error('Could not find the inline insertion point.');
  }

  const title = getLinkedNoteTitle(relativePath, requestedTitle);
  const linkedDoc = createDefaultDoc(rootComponent.doc.collection, { title });
  const reference = createLinkedPageReference(linkedDoc.id, relativePath, title);
  inlineEditor.insertText(inlineRange, REFERENCE_NODE, {
    reference,
  } as never);
  inlineEditor.setInlineRange({
    index: inlineRange.index + REFERENCE_NODE.length,
    length: 0,
  });

  return linkedDoc;
}

export function attachCogntPathToInlineReference(
  doc: Doc,
  pageId: string,
  relativePath: string,
  title?: string | null
) {
  for (const model of walkBlockModels(doc.root)) {
    const text = getBlockText(model);
    if (!text) {
      continue;
    }

    let index = 0;
    for (const delta of text.toDelta()) {
      const inserted = typeof delta.insert === 'string' ? delta.insert : '';
      const length = inserted.length;
      const reference = getDeltaReference(delta);

      if (reference?.pageId === pageId) {
        text.format(index, Math.max(length, REFERENCE_NODE.length), {
          reference: updateLinkedPageReference(
            reference,
            pageId,
            relativePath,
            title ?? reference.title ?? relativePath
          ),
        });
        return true;
      }

      index += length;
    }
  }

  return false;
}

export function replaceExtractTextRangeWithLinkedPage(
  doc: Doc,
  range: ExtractTextReplacement,
  relativePath: string,
  title: string
): Doc {
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
    throw new Error('selected block no longer has editable text');
  }

  const linkedDoc = createDefaultDoc(doc.collection, { title });
  const reference = createLinkedPageReference(linkedDoc.id, relativePath, title);

  doc.transact(() => {
    model.text!.replace(range.index, range.length, REFERENCE_NODE, {
      reference,
    });
  });

  return linkedDoc;
}

function replaceExtractTextSpanWithLinkedPage(
  doc: Doc,
  range: ExtractTextRangeReplacement,
  relativePath: string,
  title: string
): Doc {
  const startModel = getEditableTextBlock(doc, range.start.blockId);
  const endModel = getEditableTextBlock(doc, range.end.blockId);
  if (!startModel || !endModel) {
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
    .filter((model): model is EditableTextBlock => selectedIds.has(getBlockModelId(model)));

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

  return linkedDoc;
}

export function replaceExtractBlocksWithLinkedPage(
  doc: Doc,
  blockIds: string[],
  relativePath: string,
  title: string
) {
  const models = getUniqueBlockIds(blockIds)
    .map(blockId => doc.getBlockById(blockId))
    .filter((model: unknown): model is NonNullable<unknown> => Boolean(model));
  if (models.length === 0) {
    throw new Error('selected blocks no longer exist');
  }

  const entries = models.map(model => ({
    model,
    parent: doc.getParent(model as never) as { id?: string; children?: unknown[] } | null,
  }));
  if (entries.some(entry => !entry.parent)) {
    throw new Error('could not find the selected block parent');
  }

  const parent = entries[0].parent;
  if (!parent || entries.some(entry => entry.parent !== parent)) {
    throw new Error('selected blocks must share one parent');
  }

  const children = Array.isArray(parent.children) ? parent.children : [];
  const sortedEntries = entries
    .map(entry => ({
      ...entry,
      index: children.indexOf(entry.model),
    }))
    .sort((left, right) => left.index - right.index);
  const firstIndex = sortedEntries.find(entry => entry.index >= 0)?.index;
  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });

  doc.transact(() => {
    doc.addBlock(
      'affine:embed-linked-doc' as never,
      {
        pageId: linkedDoc.id,
        params: createCogntLinkedNoteParams(relativePath),
        style: defaultCogntEmbedStyle,
        caption: null,
        title: titleText,
        description: relativePath,
        [cogntLinkedNotePathProp]: relativePath,
      } as never,
      parent as never,
      firstIndex
    );
    for (const entry of [...sortedEntries].sort((left, right) => right.index - left.index)) {
      doc.deleteBlock(entry.model as never, { deleteChildren: true });
    }
  });

  return linkedDoc;
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

type EditableTextBlock = {
  id?: string;
  text?: {
    length?: number;
    replace(
      index: number,
      length: number,
      content: string,
      attributes?: Record<string, unknown>
    ): void;
    toString?(): string;
  };
  children?: unknown[];
};

function getEditableTextBlock(doc: Doc, blockId: string): EditableTextBlock | null {
  const model = doc.getBlockById(blockId) as EditableTextBlock | null;
  return model?.text ? model : null;
}

function getBlockModelId(model: unknown) {
  return typeof (model as { id?: unknown } | null)?.id === 'string'
    ? (model as { id: string }).id
    : '';
}

function getEditableTextLength(model: EditableTextBlock) {
  if (typeof model.text?.length === 'number') {
    return Math.max(0, model.text.length);
  }

  const text = typeof model.text?.toString === 'function'
    ? model.text.toString()
    : '';
  return text === '[object Object]' ? 0 : text.length;
}

function clampTextIndex(index: number, textLength: number) {
  return Math.max(0, Math.min(Math.max(0, index), textLength));
}

function getSelectionPointEndOffset(point: ExtractTextReplacementPoint) {
  return point.index + Math.max(0, point.length);
}

function isEmptyEditableTextBlock(model: EditableTextBlock) {
  return getEditableTextLength(model) === 0
    && (!Array.isArray(model.children) || model.children.length === 0);
}
