import { createDefaultDoc } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import { createBlockId } from '../documents/block-suite-document';
import { reportDiagnostic } from '../host';
import type { LinkedNoteTitleResolver } from './linked-note-types';
import {
  getLinkedNoteTitle,
  getTrimmedString,
} from './linked-note-paths';

export function* walkBlockModels(model: unknown): Generator<unknown> {
  if (!model) {
    reportDiagnostic('warn', 'Skipped block model walk because model was empty.');
    return;
  }

  yield model;

  const children = (model as { children?: unknown }).children;
  if (!Array.isArray(children)) {
    return;
  }

  for (const child of children) {
    yield* walkBlockModels(child);
  }
}

export function getBlockText(model: unknown) {
  const text = (model as { text?: unknown } | null)?.text;
  if (
    typeof text === 'object'
    && text !== null
    && typeof (text as { toDelta?: unknown }).toDelta === 'function'
    && typeof (text as { format?: unknown }).format === 'function'
  ) {
    return text as {
      toDelta(): Array<{
        insert?: unknown;
        attributes?: Record<string, unknown>;
      }>;
      format(index: number, length: number, format: Record<string, unknown>): void;
      replace(
        index: number,
        length: number,
        content: string,
        attributes?: Record<string, unknown>
      ): void;
    };
  }

  reportDiagnostic('warn', 'Block model did not expose an editable text adapter.', {
    modelType: typeof model,
    hasText: text !== undefined && text !== null,
  });
  return null;
}

export function getDeltaReference(delta: {
  attributes?: Record<string, unknown>;
}) {
  const reference = delta.attributes?.reference;
  if (typeof reference === 'object' && reference !== null) {
    return reference as {
      pageId?: string;
      title?: string;
      description?: string;
      relativePath?: string;
      cogntLinkedNotePath?: string;
      params?: Record<string, unknown>;
      type?: string;
    };
  }

  return null;
}

export function ensureLinkedPageReferenceDoc(
  doc: Doc,
  reference: Record<string, unknown>,
  relativePath: string,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const title = getLinkedNoteTitle(
    relativePath,
    resolveTitle?.(relativePath) ?? getTrimmedString(reference.title)
  );
  const pageId = getTrimmedString(reference.pageId) ?? createBlockId();

  if (!doc.collection.getDoc(pageId)) {
    createDefaultDoc(doc.collection, { id: pageId, title });
  } else {
    doc.collection.setDocMeta(pageId, { title });
  }

  return { pageId, title };
}

export function ensureLinkedDocReferenceDoc(doc: Doc, pageId: string | null, title: string) {
  const existingDoc = pageId ? doc.collection.getDoc(pageId) : null;
  const linkedDoc = existingDoc ?? createDefaultDoc(
    doc.collection,
    pageId ? { id: pageId, title } : { title }
  );

  doc.collection.setDocMeta(linkedDoc.id, { title });
  return linkedDoc;
}
