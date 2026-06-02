import type { Doc } from '@blocksuite/store';

export const cogntLinkedNotePathProp = 'cogntLinkedNotePath';
export const cogntLinkedNoteDisplayProp = 'cogntLinkedNoteDisplay';
export const iconOnlyCogntLinkedNoteDisplay = 'icon';
export const embedLinkedDocTagName = 'affine-embed-linked-doc-block';
export const cogntLinkedDocWrapStyleId = 'cognt-linked-doc-wrap-style';
export const cogntInlineReferenceStyleId = 'cognt-inline-reference-style';
export const defaultCogntEmbedStyle = 'horizontal';

export const cogntEmbedStyleAliases = new Map([
  ['horizontal', 'horizontal'],
  ['horizontalthin', 'horizontalThin'],
  ['list', 'list'],
  ['vertical', 'vertical'],
  ['cube', 'cube'],
]);

export type CogntLinkedDoc = {
  doc: Doc;
  relativePath: string;
};

export type LinkedNoteTitleResolver = (relativePath: string) => string | null | undefined;

export type LinkedPageReference = {
  pageId?: string;
  relativePath?: string;
  description?: string;
  cogntLinkedNotePath?: string;
  params?: Record<string, unknown>;
  title?: string;
  type?: string;
  cogntLinkedNoteDisplay?: string;
};

export type LinkedNoteResult = {
  relativePath?: string;
  title?: string;
};
