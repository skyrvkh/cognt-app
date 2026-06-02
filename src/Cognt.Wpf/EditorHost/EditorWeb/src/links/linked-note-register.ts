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
} from './linked-note-dom-models';
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
import { reportDiagnostic } from '../host';

export function hydrateCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  reportDiagnostic('info', 'Hydrating Cognt embed linked notes from paragraphs.');
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    reportDiagnostic('warn', 'Embed linked note hydration skipped: note block not found.');
    return;
  }

  let hydratedCount = 0;
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
    hydratedCount++;
  }

  reportDiagnostic('info', `Embed linked note hydration completed (hydrated=${hydratedCount}).`);
}

export function registerSerializedCogntEmbedLinkedNotes(
  doc: Doc,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  reportDiagnostic('info', 'Registering serialized embed linked notes.');
  let changed = false;
  let visited = 0;
  let registered = 0;

  for (const model of doc.getBlocks()) {
    visited++;
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
      reportDiagnostic(
        'info',
        `Serialized embed linked note model updated (relativePath=${relativePath}, pageId=${linkedDoc.id}).`
      );
    }
    onLinkedDoc?.({ doc: linkedDoc, relativePath });
    registered++;
  }

  reportDiagnostic(
    'info',
    `Serialized embed linked note registration completed (visited=${visited}, registered=${registered}, changed=${changed}).`
  );
  return changed;
}

export function registerSerializedCogntInlineLinkedNotes(
  doc: Doc,
  resolveTitle?: LinkedNoteTitleResolver
) {
  reportDiagnostic('info', 'Registering serialized inline linked notes.');
  let changed = false;
  let visitedBlocks = 0;
  let visitedReferences = 0;
  let updatedReferences = 0;

  for (const model of walkBlockModels(doc.root)) {
    visitedBlocks++;
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
        visitedReferences++;
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
          updatedReferences++;
          reportDiagnostic(
            'info',
            `Serialized inline linked note reference updated (relativePath=${relativePath}, pageId=${linkedPage.pageId}).`
          );
        }
      }

      index += length;
    }
  }

  reportDiagnostic(
    'info',
    `Serialized inline linked note registration completed (blocks=${visitedBlocks}, references=${visitedReferences}, updated=${updatedReferences}, changed=${changed}).`
  );
  return changed;
}

export function upgradeRuntimeRawCogntInlineMentions(
  doc: Doc,
  resolveTitle?: LinkedNoteTitleResolver
) {
  reportDiagnostic('info', 'Upgrading runtime raw Cognt inline mentions.');
  let visitedBlocks = 0;
  let replacementCount = 0;
  for (const model of walkBlockModels(doc.root)) {
    visitedBlocks++;
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
      replacementCount++;
    }
  }

  reportDiagnostic(
    'info',
    `Runtime raw Cognt inline mention upgrade completed (blocks=${visitedBlocks}, replacements=${replacementCount}).`
  );
}
