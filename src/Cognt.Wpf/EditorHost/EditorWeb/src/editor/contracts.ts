import type {
  CreateExtractCommandMessage,
  SaveClozeCommandMessage,
} from '../commands';
import type {
  EditorBlobListResult,
  EditorBlobReadResult,
  EditorBlobResult,
  ListEditorBlobsMessage,
  ReadEditorBlobMessage,
  StoreEditorBlobMessage,
} from './blob-source';
import type {
  OpenMentionMessage,
  RequestMentionPreviewMessage,
  SearchMentionTargetsMessage,
} from '../mention-controller';
import type { MentionTarget } from '../mentions';
import type { LinkedNoteViewMode } from './slash-menu';

export type {
  CreateExtractCommandMessage as CreateExtractMessage,
  SaveClozeCommandMessage as SaveClozeMessage,
  LinkedNoteViewMode,
};

export type CreateLinkedNoteMessage = {
  type: 'createLinkedNote';
  sourcePath: string;
  viewMode: LinkedNoteViewMode;
};

export type CreateLinkedNoteResult = {
  ok: boolean;
  relativePath?: string;
  title?: string;
  documentContent?: string;
  error?: string;
};

export type AttachFileMessage = {
  type: 'attachFile';
  sourcePath: string;
  fileName: string;
  contentBase64: string;
};

export type AttachImageSourceMessage = {
  type: 'attachImageSource';
  sourcePath: string;
  imageSource: string;
  fallbackFileName: string;
};

export type AttachFileResult = {
  ok: boolean;
  displayName?: string;
  relativePath?: string;
  kind?: string;
  error?: string;
};

export type ReadAttachmentMessage = {
  type: 'readAttachment';
  sourcePath: string;
  imageSource: string;
};

export type ReadAttachmentResult = {
  ok: boolean;
  fileName?: string;
  relativePath?: string;
  contentType?: string;
  contentBase64?: string;
  error?: string;
};

export type OpenImageOcclusionMessage = {
  type: 'openImageOcclusion';
  sourcePath: string;
  imageBlockId: string;
  imageRelPath: string;
  content: string;
};

export type SelectedImageBlock = {
  imageBlockId: string;
  imageRelPath: string;
};

export type SelectedCardContextBlocks = {
  blockIds: string[];
  title?: string;
};

export type EditorCaretState = {
  blockId: string;
  index: number;
  length: number;
};

export type MarkdownEditorOptions = {
  hostElement: HTMLElement;
  documentLinksElement: HTMLElement;
  statusElement: HTMLElement;
  onDocumentChanged(): void;
  onContentChanged(content: string): void;
  onCurrentOutlineBlockChanged(blockId: string): void;
  onCreateExtract(message: CreateExtractCommandMessage): void;
  onSaveCloze(message: SaveClozeCommandMessage): void;
  onSearchMentionTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  onRequestMentionPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
  onCreateLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  onAttachFile(message: AttachFileMessage): Promise<AttachFileResult>;
  onAttachImageSource(message: AttachImageSourceMessage): Promise<AttachFileResult>;
  onReadNativeClipboardImage(): Promise<string | null>;
  onReadAttachment(message: ReadAttachmentMessage): Promise<ReadAttachmentResult>;
  onStoreEditorBlob(message: StoreEditorBlobMessage): Promise<EditorBlobResult>;
  onReadEditorBlob(message: ReadEditorBlobMessage): Promise<EditorBlobReadResult>;
  onListEditorBlobs(message: ListEditorBlobsMessage): Promise<EditorBlobListResult>;
  onOpenImageOcclusion(message: OpenImageOcclusionMessage): void;
};

export type LoadMarkdownOptions = {
  loadId?: number;
  path?: string;
  title?: string;
  content?: string;
  collectionRoot?: string;
  isBlockSuiteJson?: boolean;
  caret?: EditorCaretState | null;
};
