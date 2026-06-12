import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { DocumentLinksController } from '../document-links';
import type { MentionController } from '../mention-controller';
import type { ContentSaveCoordinator } from './content-save-coordinator';
import type { MarkdownEditorOptions } from './contracts';
import type { DocumentChangeCoordinator } from './document-change-coordinator';
import type { DocumentLoadWorkflow } from './document-load-workflow';
import type { EditorCommandController } from './editor-command-controller';
import type { EditorEventBindingController } from './editor-event-binding-controller';
import type { EditorInteractionController } from './editor-interactions';
import type { EditorOutlineViewportController } from './editor-outline-viewport-controller';
import type { EditorRuntimeCoordinator } from './editor-runtime-coordinator';
import type { EditorSelectionController } from './editor-selection-controller';
import type { ExtractWorkflow } from './extract-workflow';
import type { LinkedDocWorkflow } from './linked-doc-workflow';

export type EditorBootstrapConfig = {
  options: MarkdownEditorOptions;
  signal: AbortSignal;
  getCurrentDoc(): Doc | null;
  trackCurrentDoc(doc: Doc): void;
  getCurrentPath(): string;
  setCurrentPath(path: string): void;
  getCurrentCollectionRoot(): string;
  setCurrentCollectionRoot(collectionRoot: string): void;
  getLoadGeneration(): number;
  nextLoadGeneration(): number;
  isLoading(): boolean;
  setLoadingDocument(loading: boolean): void;
  incrementUserEditGeneration(): void;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  cancelPendingContentChanged(): void;
  isLargeDocument(): boolean;
};

export type EditorBootstrapEnvironment = {
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getCurrentCollectionRoot(): string;
  getLoadGeneration(): number;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  isLargeDocument(): boolean;
};

export type EditorWorkflowRefs = {
  linkedDocs: LinkedDocWorkflow | null;
  documentLinks: DocumentLinksController | null;
};

export type EditorBootstrapResult = {
  editor: PageEditor;
  runtime: EditorRuntimeCoordinator;
  selection: EditorSelectionController;
  interactions: EditorInteractionController;
  outlineViewport: EditorOutlineViewportController;
  mentions: MentionController;
  documentLinks: DocumentLinksController;
  contentSave: ContentSaveCoordinator;
  docChanges: DocumentChangeCoordinator;
  extractWorkflow: ExtractWorkflow;
  linkedDocs: LinkedDocWorkflow;
  documentLoad: DocumentLoadWorkflow;
  commands: EditorCommandController;
};

export type EditorWorkflowBundle = Pick<
  EditorBootstrapResult,
  | 'documentLinks'
  | 'contentSave'
  | 'docChanges'
  | 'extractWorkflow'
  | 'linkedDocs'
  | 'documentLoad'
  | 'commands'
> & {
  events: EditorEventBindingController;
};
