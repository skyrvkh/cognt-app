import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  uniqueBlockIds,
} from './block-models';
import type { SelectedImageBlock } from './contracts';
import {
  createSelectedImageBlock,
  isImageBlockModel,
} from './image-source-models';
import {
  getBlockSelectionBlockIds,
  getTextSelectionImageCandidateBlockIds,
} from './selection-block-queries';

export function getImageBlocksInDocument(doc: Doc | null) {
  if (!doc) {
    return [];
  }

  return doc
    .getBlocks()
    .map(block => (block as { model?: unknown }).model ?? block)
    .filter(isImageBlockModel);
}

export function getSelectedImageBlock(
  editor: PageEditor,
  doc: Doc | null,
  textSelection: TextSelection | null | undefined,
  collectionRoot: string
): SelectedImageBlock | null {
  const selectedBlockIds = uniqueBlockIds([
    ...getBlockSelectionBlockIds(editor),
    ...getTextSelectionImageCandidateBlockIds(editor, textSelection),
  ]);

  for (const blockId of selectedBlockIds) {
    const model = doc?.getBlock(blockId)?.model;
    const selectedImage = createSelectedImageBlock(model, collectionRoot);
    if (selectedImage) {
      return selectedImage;
    }
  }

  const persistableImages = getImageBlocksInDocument(doc)
    .map(image => createSelectedImageBlock(image, collectionRoot))
    .filter((image): image is SelectedImageBlock => image !== null);
  return persistableImages.length === 1
    ? persistableImages[0]
    : null;
}
