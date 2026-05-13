import { createDefaultDoc } from '@blocksuite/blocks';
import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import {
  type BlockModel,
  type Doc,
} from '@blocksuite/store';
import { createBlockId } from '../documents/block-suite-document';
import {
  defaultCogntEmbedStyle,
  type CogntLinkedDoc,
  type LinkedNoteTitleResolver,
} from './linked-note-types';
import {
  createCogntLinkedNoteParams,
  extractCogntEmbedTarget,
  findRawCogntInlineMentionMatches,
  getLinkedNoteTitle,
  getLinkedPageReferencePath,
  getTrimmedString,
  hasCogntLinkedNotePath,
  mergeCogntLinkedNoteParams,
  getRecordProperty,
} from './linked-note-paths';
import {
  getCogntLinkedNotePathFromModel,
  getLinkedNoteTitleFromModel,
  isCogntLinkedDocModel,
} from './linked-note-dom';
import {
  ensureLinkedDocReferenceDoc,
  ensureLinkedPageReferenceDoc,
  getBlockText,
  getDeltaReference,
  walkBlockModels,
} from './linked-note-block-text';
import {
  createLinkedPageReference,
  updateLinkedPageReference,
} from './linked-note-reference';

export function hydrateCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    return;
  }

  for (const child of [...note.children]) {
    if (child.flavour !== 'affine:paragraph') {
      continue;
    }

    const target = extractCogntEmbedTarget(child.text?.toString());
    if (!target) {
      continue;
    }

    const title = getLinkedNoteTitle(
      target.relativePath,
      target.title ?? resolveTitle?.(target.relativePath)
    );
    const linkedDoc = createDefaultDoc(doc.collection, { title });
    onLinkedDoc?.({ doc: linkedDoc, relativePath: target.relativePath });
    const parent = doc.getParent(child);
    if (!parent) {
      continue;
    }

    const index = parent.children.indexOf(child);
    doc.addBlock(
      'affine:embed-linked-doc' as never,
      {
        pageId: linkedDoc.id,
        params: createCogntLinkedNoteParams(target.relativePath),
        style: target.style ?? defaultCogntEmbedStyle,
        caption: null,
        title,
        description: target.relativePath,
        relativePath: target.relativePath,
        cogntLinkedNotePath: target.relativePath,
      } as never,
      parent,
      index
    );
    doc.deleteBlock(child, { deleteChildren: true });
  }
}

export function registerSerializedCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  let changed = false;

  for (const model of doc.getBlocks()) {
    if (!isCogntLinkedDocModel(model)) {
      continue;
    }

    const relativePath = getCogntLinkedNotePathFromModel(model);
    if (!relativePath) {
      continue;
    }

    const title = getLinkedNoteTitle(
      relativePath,
      resolveTitle?.(relativePath) ?? getLinkedNoteTitleFromModel(model)
    );
    const modelRecord = model as unknown as Record<string, unknown> & { pageId?: string };
    const existingPageId = getTrimmedString(modelRecord.pageId);
    const linkedDoc = ensureLinkedDocReferenceDoc(doc, existingPageId, title);
    const mergedParams = mergeCogntLinkedNoteParams(
      getRecordProperty(modelRecord, 'params'),
      relativePath
    );
    const needsUpdate =
      modelRecord.pageId !== linkedDoc.id
      || getLinkedNoteTitleFromModel(model) !== title
      || getCogntLinkedNotePathFromModel(model) !== relativePath
      || getTrimmedString(modelRecord.description) !== relativePath
      || !hasCogntLinkedNotePath(modelRecord.params, relativePath);

    if (needsUpdate) {
      doc.updateBlock(model as BlockModel, {
        pageId: linkedDoc.id,
        params: mergedParams,
        title,
        description: relativePath,
        relativePath,
        cogntLinkedNotePath: relativePath,
      });
      changed = true;
    }
    onLinkedDoc?.({ doc: linkedDoc, relativePath });
  }

  return changed;
}

export function registerSerializedCogntInlineLinkedNotes(
  doc: Doc,
  resolveTitle?: LinkedNoteTitleResolver
) {
  let changed = false;

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
      const relativePath = reference ? getLinkedPageReferencePath(reference) : null;

      if (reference?.type === 'LinkedPage' && reference.pageId && relativePath) {
        const linkedPage = ensureLinkedPageReferenceDoc(doc, reference, relativePath, resolveTitle);
        const needsUpdate =
          reference.pageId !== linkedPage.pageId
          || getTrimmedString(reference.title) !== linkedPage.title
          || getLinkedPageReferencePath(reference) !== relativePath
          || getTrimmedString(reference.description) !== relativePath
          || !hasCogntLinkedNotePath(reference.params, relativePath);

        if (needsUpdate) {
          text.format(index, Math.max(length, REFERENCE_NODE.length), {
            reference: updateLinkedPageReference(
              reference,
              linkedPage.pageId,
              relativePath,
              linkedPage.title
            ),
          });
          changed = true;
        }
      }

      index += length;
    }
  }

  return changed;
}

export function upgradeRuntimeRawCogntInlineMentions(
  doc: Doc,
  resolveTitle?: LinkedNoteTitleResolver
) {
  for (const model of walkBlockModels(doc.root)) {
    const text = getBlockText(model);
    if (!text) {
      continue;
    }

    const replacements: Array<{
      index: number;
      length: number;
      relativePath: string;
    }> = [];
    let index = 0;

    for (const delta of text.toDelta()) {
      const inserted = typeof delta.insert === 'string' ? delta.insert : '';
      const length = inserted.length;

      if (inserted.includes('[[')) {
        for (const match of findRawCogntInlineMentionMatches(inserted)) {
          replacements.push({
            index: index + match.index,
            length: match.length,
            relativePath: match.relativePath,
          });
        }
      }

      index += length;
    }

    for (const replacement of replacements.reverse()) {
      text.replace(
        replacement.index,
        replacement.length,
        REFERENCE_NODE,
        {
          reference: createLinkedPageReference(
            createBlockId(),
            replacement.relativePath,
            getLinkedNoteTitle(replacement.relativePath, resolveTitle?.(replacement.relativePath))
          ),
        }
      );
    }
  }
}
