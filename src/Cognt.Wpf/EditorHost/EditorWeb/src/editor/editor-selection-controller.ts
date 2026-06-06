import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import type {
  SelectedCardContextBlocks,
  SelectedImageBlock,
} from './contracts';
import {
  getBlockSelectionBlockIds,
  getCurrentInsertionBlockModel,
  getCurrentTextSelectionBlockModel,
} from './selection-block-queries';
import {
  getSelectedCardContextBlocks,
} from './selection-card-context-queries';
import {
  getSelectedImageBlock,
} from './selection-image-queries';
import {
  getOutlineHeadingBlockIds,
  getRenderedBlockElement,
  resolveOutlineBlockId,
} from './selection-outline-queries';

type EditorSelectionControllerOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  getCurrentCollectionRoot(): string;
};

export class EditorSelectionController {
  private outlineHeadingBlockIds: string[] | null = null;

  public constructor(private readonly options: EditorSelectionControllerOptions) {}

  public clearOutlineCache() {
    this.outlineHeadingBlockIds = null;
  }

  public getTextSelection() {
    return this.options.editor.std?.selection.find('text') as TextSelection | null | undefined;
  }

  public getSelectedImageBlock(): SelectedImageBlock | null {
    return getSelectedImageBlock(
      this.options.editor,
      this.options.getCurrentDoc(),
      this.getTextSelection(),
      this.options.getCurrentCollectionRoot());
  }

  public getSelectedCardContextBlocks(): SelectedCardContextBlocks | null {
    return getSelectedCardContextBlocks(
      this.options.editor,
      this.options.getCurrentDoc(),
      this.getTextSelection());
  }

  public getCurrentInsertionBlockModel() {
    return getCurrentInsertionBlockModel(
      this.options.editor,
      this.options.getCurrentDoc(),
      this.getTextSelection());
  }

  public getCurrentTextSelectionBlockModel() {
    return getCurrentTextSelectionBlockModel(
      this.options.editor,
      this.options.getCurrentDoc(),
      this.getTextSelection());
  }

  public getBlockSelectionBlockIds() {
    return getBlockSelectionBlockIds(this.options.editor);
  }

  public resolveOutlineBlockId(blockId: string) {
    return resolveOutlineBlockId(
      this.options.getCurrentDoc(),
      blockId,
      this.getOutlineHeadingBlockIds());
  }

  public getRenderedBlockElement(blockId: string) {
    return getRenderedBlockElement(this.options.editor, blockId);
  }

  private getOutlineHeadingBlockIds() {
    if (this.outlineHeadingBlockIds) {
      return this.outlineHeadingBlockIds;
    }

    this.outlineHeadingBlockIds = getOutlineHeadingBlockIds(
      this.options.getCurrentDoc()
    );
    return this.outlineHeadingBlockIds;
  }
}
