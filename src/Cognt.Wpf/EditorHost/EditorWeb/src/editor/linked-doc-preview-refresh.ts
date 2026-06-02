import type { Doc } from '@blocksuite/store';
import {
  registerSerializedCogntEmbedLinkedNotes,
  registerSerializedCogntInlineLinkedNotes,
  type CogntLinkedDoc,
  type LinkedNoteTitleResolver,
} from '../links/linked-notes';

export function refreshSerializedLinkedDocBlocks(
  doc: Doc,
  resolveTitle: LinkedNoteTitleResolver,
  onLinkedDoc: (linkedDoc: CogntLinkedDoc) => void
) {
  const embedChanged = registerSerializedCogntEmbedLinkedNotes(
    doc,
    linkedDoc => onLinkedDoc(linkedDoc),
    resolveTitle
  );
  const inlineChanged = registerSerializedCogntInlineLinkedNotes(
    doc,
    resolveTitle
  );

  return embedChanged || inlineChanged;
}
