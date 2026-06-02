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
import { reportDiagnostic } from '../host';
import { createLinkedDocEmbedBlockProps } from './linked-note-embed-block';
import { getLinkedNoteTitle } from './linked-note-paths';
import { createLinkedPageReference } from './linked-note-reference';

export function insertCogntLinkedNoteEmbed(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  reportDiagnostic(
    'info',
    `Inserting Cognt linked note embed (relativePath=${relativePath}, requestedTitle=${requestedTitle ?? ''}).`
  );
  const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
  const { model } = context;
  const parent = rootComponent.doc.getParent(model);

  if (!parent) {
    reportDiagnostic('error', 'Linked note embed insert failed: insertion parent was not found.');
    throw new Error('Could not find the insertion block.');
  }

  const index = parent.children.indexOf(model);
  const title = getLinkedNoteTitle(relativePath, requestedTitle);
  const linkedDoc = createDefaultDoc(rootComponent.doc.collection, { title });
  const blockId = rootComponent.doc.addBlock(
    'affine:embed-linked-doc' as never,
    createLinkedDocEmbedBlockProps(linkedDoc.id, relativePath, title),
    parent,
    index + 1
  );

  if (model.text?.length === 0 && model.children.length === 0) {
    rootComponent.doc.deleteBlock(model);
    reportDiagnostic('info', 'Linked note embed insert removed empty source slash-menu block.');
  }

  rootComponent.host.selection.setGroup('note', [
    rootComponent.host.selection.create('block', { blockId }),
  ]);

  reportDiagnostic(
    'info',
    `Linked note embed inserted (relativePath=${relativePath}, title=${title}, blockId=${blockId}, pageId=${linkedDoc.id}).`
  );
  return linkedDoc;
}

export function insertCogntLinkedNoteInline(
  context: AffineSlashMenuContext,
  relativePath: string,
  requestedTitle?: string | null
): Doc {
  reportDiagnostic(
    'info',
    `Inserting Cognt linked note inline reference (relativePath=${relativePath}, requestedTitle=${requestedTitle ?? ''}).`
  );
  const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
  const { model } = context;
  const inlineEditor = getInlineEditorByModel(rootComponent.host, model);

  if (!inlineEditor) {
    reportDiagnostic('error', 'Linked note inline insert failed: inline editor was not found.');
    throw new Error('Could not find the inline editor for the insertion block.');
  }

  const inlineRange = inlineEditor.getInlineRange();
  if (!inlineRange) {
    reportDiagnostic('error', 'Linked note inline insert failed: inline insertion point was not found.');
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

  reportDiagnostic(
    'info',
    `Linked note inline reference inserted (relativePath=${relativePath}, title=${title}, pageId=${linkedDoc.id}, index=${inlineRange.index}).`
  );
  return linkedDoc;
}
