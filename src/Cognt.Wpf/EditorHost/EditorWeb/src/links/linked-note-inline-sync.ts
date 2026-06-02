import type { Doc } from '@blocksuite/store';
import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import { reportDiagnostic } from '../host';
import {
  getBlockText,
  getDeltaReference,
  walkBlockModels,
} from './linked-note-block-text';
import { updateLinkedPageReference } from './linked-note-reference';

export function attachCogntPathToInlineReference(
  doc: Doc,
  pageId: string,
  relativePath: string,
  title?: string | null
) {
  reportDiagnostic(
    'info',
    `Attaching Cognt path to inline reference (pageId=${pageId}, relativePath=${relativePath}).`
  );
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
        reportDiagnostic(
          'info',
          `Attached Cognt path to inline reference (pageId=${pageId}, relativePath=${relativePath}, index=${index}).`
        );
        return true;
      }

      index += length;
    }
  }

  reportDiagnostic('warn', `Inline reference path attach skipped: page id not found (pageId=${pageId}).`);
  return false;
}
