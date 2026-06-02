import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import { createDefaultDoc } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';
import {
  getEditableTextBlock,
  getEditableTextLength,
  getSortedBlockEntries,
  hasEditableText,
} from './linked-note-editable-text';
import { createLinkedDocEmbedBlockProps } from './linked-note-embed-block';
import { getLinkedNoteTitle } from './linked-note-paths';
import { createIconOnlyLinkedPageReference } from './linked-note-reference';

export function replaceExtractBlocksWithLinkedPage(
  doc: Doc,
  blockIds: string[],
  relativePath: string,
  title: string
) {
  reportDiagnostic(
    'info',
    `Replacing extract blocks with linked page (requestedBlocks=${blockIds.length}, relativePath=${relativePath}).`
  );
  const sortedEntries = getSortedBlockEntries(doc, blockIds);
  if (sortedEntries.length === 0) {
    reportDiagnostic('error', 'Extract block replacement failed: selected blocks no longer exist.');
    throw new Error('selected blocks no longer exist');
  }

  const parent = sortedEntries[0]?.parent;
  const firstIndex = Math.max(
    0,
    sortedEntries.find(entry => entry.index >= 0)?.index ?? 0
  );
  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });

  doc.transact(() => {
    doc.addBlock(
      'affine:embed-linked-doc' as never,
      createLinkedDocEmbedBlockProps(linkedDoc.id, relativePath, titleText),
      parent as never,
      firstIndex
    );
    for (const entry of [...sortedEntries].sort((left, right) => right.index - left.index)) {
      doc.deleteBlock(entry.model as never, { deleteChildren: true });
    }
  });

  reportDiagnostic(
    'info',
    `Extract blocks replaced with linked page (selectedBlocks=${sortedEntries.length}, firstIndex=${firstIndex}, pageId=${linkedDoc.id}).`
  );
  return linkedDoc;
}

export function insertExtractBlocksLinkedPageIcon(
  doc: Doc,
  blockIds: string[],
  relativePath: string,
  title: string
) {
  reportDiagnostic(
    'info',
    `Inserting extract blocks linked-page icon (requestedBlocks=${blockIds.length}, relativePath=${relativePath}).`
  );
  const sortedEntries = getSortedBlockEntries(doc, blockIds);
  if (sortedEntries.length === 0) {
    reportDiagnostic('error', 'Extract block icon insertion failed: selected blocks no longer exist.');
    throw new Error('selected blocks no longer exist');
  }

  const titleText = getLinkedNoteTitle(relativePath, title);
  const linkedDoc = createDefaultDoc(doc.collection, { title: titleText });
  const reference = createIconOnlyLinkedPageReference(linkedDoc.id, relativePath, titleText);
  const editableEntry = [...sortedEntries]
    .reverse()
    .find(entry => hasEditableText(entry.model));

  doc.transact(() => {
    if (editableEntry && hasEditableText(editableEntry.model)) {
      const model = editableEntry.model;
      model.text!.replace(getEditableTextLength(model), 0, REFERENCE_NODE, { reference });
      reportDiagnostic('info', `Extract block linked-page icon inserted into existing editable block (pageId=${linkedDoc.id}).`);
      return;
    }

    const lastEntry = sortedEntries.at(-1);
    const parent = lastEntry?.parent;
    if (!parent) {
      reportDiagnostic('error', 'Extract block icon insertion failed: selected block parent was not found.');
      throw new Error('could not find the selected block parent');
    }

    const blockId = doc.addBlock(
      'affine:paragraph' as never,
      { type: 'text' } as never,
      parent as never,
      Math.max(0, lastEntry.index + 1)
    );
    const model = getEditableTextBlock(doc, blockId);
    if (!model?.text) {
      reportDiagnostic('error', 'Extract block icon insertion failed: compact paragraph could not be created.');
      throw new Error('could not create a compact link paragraph');
    }

    model.text.replace(0, 0, REFERENCE_NODE, { reference });
    reportDiagnostic('info', `Extract block linked-page icon inserted into new compact paragraph (blockId=${blockId}, pageId=${linkedDoc.id}).`);
  });

  reportDiagnostic(
    'info',
    `Extract blocks linked-page icon insertion completed (selectedBlocks=${sortedEntries.length}, pageId=${linkedDoc.id}).`
  );
  return linkedDoc;
}
