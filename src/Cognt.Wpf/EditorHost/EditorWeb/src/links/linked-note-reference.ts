import type { LinkedPageReference } from './linked-note-types';
import {
  createCogntLinkedNoteParams,
  mergeCogntLinkedNoteParams,
} from './linked-note-paths';

export function createLinkedPageReference(
  pageId: string,
  relativePath: string,
  title: string
): LinkedPageReference {
  return {
    type: 'LinkedPage',
    pageId,
    params: createCogntLinkedNoteParams(relativePath),
    title,
    description: relativePath,
    relativePath,
    cogntLinkedNotePath: relativePath,
  };
}

export function updateLinkedPageReference(
  reference: Record<string, unknown>,
  pageId: string,
  relativePath: string,
  title: string
): LinkedPageReference {
  return {
    ...reference,
    type: 'LinkedPage',
    pageId,
    params: mergeCogntLinkedNoteParams(reference.params, relativePath),
    title,
    description: relativePath,
    relativePath,
    cogntLinkedNotePath: relativePath,
  };
}
