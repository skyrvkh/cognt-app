import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';

export type EditorInteractionOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  getTextSelection(): TextSelection | null | undefined;
  getCurrentInsertionBlockModel(): unknown;
  resolveOutlineBlockId(blockId: string): string | null;
  getRenderedBlockElement(blockId: string): HTMLElement | null;
  showStatus(message: string): void;
};

export type EditorInteractionModeState = {
  readOnlyMode: boolean;
  lockedInputStatusShownAt: number;
  setReadOnlyMode(readOnly: boolean): void;
  setLockedInputStatusShownAt(value: number): void;
};

export type EditorInteractionCaretState = {
  readOnlyMode: boolean;
  lastKnownTextSelectionForPaste: TextSelection | null;
  setLastKnownTextSelectionForPaste(selection: TextSelection | null): void;
};
