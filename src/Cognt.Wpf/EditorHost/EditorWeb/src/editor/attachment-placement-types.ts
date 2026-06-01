import type { PageEditor } from '@blocksuite/presets';
import type { BlockModel, Doc } from '@blocksuite/store';

export type AttachmentPlacementOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  getCurrentInsertionBlockModel(): BlockModel | null;
  getCurrentTextSelectionBlockModel(): BlockModel | null;
  flushHost(): Promise<void>;
  refreshMentionHighlights(): void;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  notifyContentChanged(): Promise<void>;
};
