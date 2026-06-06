import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { DocumentLinksController } from '../document-links';
import type { MentionController } from '../mention-controller';
import type { BlobSource } from './blob-source';
import type { LoadMarkdownOptions, MarkdownEditorOptions } from './contracts';
import type { ContentSaveCoordinator } from './content-save-coordinator';
import type { DocumentChangeCoordinator } from './document-change-coordinator';
import type { EditorInteractionController } from './editor-interactions';
import type { LinkedDocWorkflow } from './linked-doc-workflow';

export type DocumentLoadWorkflowOptions = {
  editor: PageEditor;
  blobSource: BlobSource;
  contentSave: ContentSaveCoordinator;
  docChanges: DocumentChangeCoordinator;
  documentLinks: DocumentLinksController;
  interactions: EditorInteractionController;
  linkedDocs: LinkedDocWorkflow;
  mentions: MentionController;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  setCurrentPath(path: string): void;
  getCurrentCollectionRoot(): string;
  setCurrentCollectionRoot(collectionRoot: string): void;
  getLoadGeneration(): number;
  nextLoadGeneration(): number;
  setLoadingDocument(loading: boolean): void;
  clearOutlineCache(): void;
  trackCurrentDoc(doc: Doc): void;
  flushHost(): Promise<void>;
  showStatus(message: string): void;
  readAttachment: MarkdownEditorOptions['onReadAttachment'];
};

export type DocumentLoadStateGuard = {
  generation: number;
  isStaleGeneration(generation: number): boolean;
};

export type DocumentLoadOptions = LoadMarkdownOptions;
