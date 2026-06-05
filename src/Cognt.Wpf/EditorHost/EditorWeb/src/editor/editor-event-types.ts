import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { PendingExtract } from '../commands';
import type { LinkedPageReference } from '../links/linked-notes';

export type EditorEventBindingOptions = {
  editor: PageEditor;
  signal: AbortSignal;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  hasDroppedFiles(event: DragEvent): boolean;
  handleFileDragOver(event: DragEvent): void;
  handleDroppedFiles(event: DragEvent): Promise<void>;
  isEditorEvent(event: Event): boolean;
  cancelNativeFileEvent(event: Event): void;
  captureNativeTextSelectionForPaste(): void;
  scheduleMentionHighlightUpdate(): void;
  recordUserEdit(): void;
  isLargeDocument(): boolean;
  scheduleDocumentChangedNotification(): void;
  scheduleContentChanged(): void;
  consumeSuppressedNativeCut(): boolean;
  openLinkedDocReference(reference: LinkedPageReference): Promise<void>;
  getSelectedLinkedDocModelForOpenIntent(event: MouseEvent): unknown;
  openLinkedDocFromModel(model: unknown): Promise<void>;
  handleCreateExtractShortcut(
    event: KeyboardEvent,
    sourceAction: PendingExtract['sourceAction']
  ): Promise<void>;
  handleClozeCommand(event: KeyboardEvent): Promise<void>;
  handleImageOcclusionShortcut(event: KeyboardEvent): Promise<void>;
};
