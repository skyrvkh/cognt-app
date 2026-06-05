import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import {
  createTitleFromPlainText,
  normalizeSelectionText,
} from '../documents/block-suite-document';
import {
  getBlockPlainText,
  uniqueBlockIds,
} from './block-models';
import type { SelectedCardContextBlocks } from './contracts';
import {
  getBlockSelectionBlockIds,
  getTextSelectionBlockIds,
} from './selection-block-queries';

export function getSelectedCardContextBlocks(
  editor: PageEditor,
  doc: Doc | null,
  textSelection?: TextSelection | null
): SelectedCardContextBlocks | null {
  const blockIds = getSelectedCardContextBlockIds(editor, textSelection);
  if (blockIds.length === 0) {
    return null;
  }

  return {
    blockIds,
    title: createSelectedCardContextTitle(doc, blockIds),
  };
}

export function getSelectedCardContextBlockIds(
  editor: PageEditor,
  textSelection?: TextSelection | null
) {
  const ids = textSelection && !textSelection.isCollapsed()
    ? getTextSelectionBlockIds(editor, textSelection)
    : [];

  return uniqueBlockIds(ids.length > 0 ? ids : getBlockSelectionBlockIds(editor));
}

function createSelectedCardContextTitle(doc: Doc | null, blockIds: string[]) {
  const selectedText = normalizeSelectionText(window.getSelection()?.toString() ?? '');
  if (selectedText.length > 0) {
    return createTitleFromPlainText(selectedText, 'Card context');
  }

  const blockText = blockIds
    .map(blockId => doc?.getBlock(blockId)?.model)
    .map(getBlockPlainText)
    .filter(text => text.length > 0)
    .join(' ');

  return createTitleFromPlainText(blockText, 'Card context');
}
