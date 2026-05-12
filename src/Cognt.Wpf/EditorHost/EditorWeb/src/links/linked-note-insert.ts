import {
  createDefaultDoc,
  type AffineSlashMenuContext,
} from '@blocksuite/blocks';
import {
  getInlineEditorByModel,
  REFERENCE_NODE,
} from '@blocksuite/affine-components/rich-text';
import type { Doc } from '@blocksuite/store';
import type { TextSelectionSnapshot } from '../documents/selection-export';
import { getTextSelectionSnapshotRange } from '../documents/selection-export';
import {
  cogntLinkedNotePathProp,
  defaultCogntEmbedStyle,
} from './linked-note-types';
import {
  createCogntLinkedNoteParams,
  getLinkedNoteTitle,
  mergeCogntLinkedNoteParams,
} from './linked-note-paths';
import { createLinkedPageReference } from './linked-note-reference';
import {
  getBlockText,
  getDeltaReference,
  walkBlockModels,
} from './linked-note-block-text';

export function insertCogntLinkedNoteEmbed(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  const { rootComponent, model } = context;
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
  const { rootComponent, model } = context;
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
          reference: {
            ...reference,
            type: 'LinkedPage',
            pageId,
            params: mergeCogntLinkedNoteParams(reference.params, relativePath),
            title: title ?? reference.title,
            description: relativePath,
            [cogntLinkedNotePathProp]: relativePath,
          },
        });
        return true;
      }

      index += length;
    }
  }

  return false;
}

export function replaceExtractSelectionWithLinkedPage(
  doc: Doc,
  selection: TextSelectionSnapshot,
  relativePath: string,
  title: string
) {
  const range = getTextSelectionSnapshotRange(selection);
  if (!range) {
    throw new Error('source replacement supports text selected inside one block');
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

