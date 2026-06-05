import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import type { CogntLinkedDoc } from '../links/linked-notes';
import type {
  CreateLinkedNoteMessage,
  CreateLinkedNoteResult,
  OpenImageOcclusionMessage,
  SaveClozeMessage,
  SelectedImageBlock,
} from './contracts';
import type { AttachmentPlacementController } from './attachment-placement';
import type { AttachmentWorkflow } from './attachment-workflow';

export type EditorCommandControllerOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  getSelectedImageBlock(): SelectedImageBlock | null;
  getTextSelection(): TextSelection | null | undefined;
  flushHost(): Promise<void>;
  cancelPendingContentChanged(): void;
  exportContent(): Promise<string>;
  saveCloze(message: SaveClozeMessage): void;
  openImageOcclusion(message: OpenImageOcclusionMessage): void;
  createLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  trackLinkedDoc(doc: Doc, relativePath: string): void;
  registerNestedLinkedDoc(linkedDoc: CogntLinkedDoc, generation: number): void;
  markLinkedDocPreviewed(doc: Doc): void;
  requestLinkedDocPreview(doc: Doc, relativePath: string, generation: number): void;
  refreshMentionHighlights(): void;
  recordUserEdit(): void;
  notifyContentChanged(): Promise<void>;
  attachmentWorkflow: AttachmentWorkflow;
  attachmentPlacement: AttachmentPlacementController;
  showStatus(message: string): void;
};

export type ClozeCommandRequest = {
  cardContextId?: string | null;
  slotId?: string | null;
  displayClozeIndex?: number | null;
};
