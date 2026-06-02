import type { LinkedPageReference } from './linked-note-types';
import {
  cogntLinkedNoteDisplayProp,
  iconOnlyCogntLinkedNoteDisplay,
} from './linked-note-types';
import {
  createIconOnlyCogntLinkedNoteParams,
  createCogntLinkedNoteParams,
  mergeCogntLinkedNoteParams,
} from './linked-note-paths';
import { reportDiagnostic } from '../host';

export function createLinkedPageReference(
  pageId: string,
  relativePath: string,
  title: string
): LinkedPageReference {
  reportDiagnostic(
    'info',
    `Creating linked page reference (pageId=${pageId}, relativePath=${relativePath}, titleLength=${title.length}).`
  );
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

export function createIconOnlyLinkedPageReference(
  pageId: string,
  relativePath: string,
  title: string
): LinkedPageReference {
  reportDiagnostic(
    'info',
    `Creating icon-only linked page reference (pageId=${pageId}, relativePath=${relativePath}, titleLength=${title.length}).`
  );
  return {
    type: 'LinkedPage',
    pageId,
    params: createIconOnlyCogntLinkedNoteParams(relativePath),
    title,
    description: relativePath,
    relativePath,
    cogntLinkedNotePath: relativePath,
    [cogntLinkedNoteDisplayProp]: iconOnlyCogntLinkedNoteDisplay,
  };
}

export function updateLinkedPageReference(
  reference: Record<string, unknown>,
  pageId: string,
  relativePath: string,
  title: string
): LinkedPageReference {
  reportDiagnostic(
    'info',
    `Updating linked page reference (pageId=${pageId}, relativePath=${relativePath}, titleLength=${title.length}).`
  );
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
