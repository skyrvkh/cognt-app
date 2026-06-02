import {
  cogntLinkedNotePathProp,
  defaultCogntEmbedStyle,
} from './linked-note-types';
import { createCogntLinkedNoteParams } from './linked-note-paths';

export function createLinkedDocEmbedBlockProps(
  pageId: string,
  relativePath: string,
  title: string
) {
  return {
    pageId,
    params: createCogntLinkedNoteParams(relativePath),
    style: defaultCogntEmbedStyle,
    caption: null,
    title,
    description: relativePath,
    [cogntLinkedNotePathProp]: relativePath,
  } as never;
}
