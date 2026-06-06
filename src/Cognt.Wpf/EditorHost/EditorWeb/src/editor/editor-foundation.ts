import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { createEmptyMarkdownDoc } from '../documents/io';
import { MentionController } from '../mention-controller';
import { createCogntBlobSource } from './blob-source';
import type { MarkdownEditorOptions } from './contracts';
import { EditorInteractionController } from './editor-interactions';
import { EditorRuntimeCoordinator } from './editor-runtime-coordinator';
import { EditorSelectionController } from './editor-selection-controller';

type EditorFoundationConfig = {
  options: MarkdownEditorOptions;
  getCurrentDoc(): Doc | null;
  trackCurrentDoc(doc: Doc): void;
  getCurrentPath(): string;
  getCurrentCollectionRoot(): string;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  ensureReferencesBlock(): void;
  shouldPatchLinkedDocBlocks(): boolean;
};

export type EditorFoundation = {
  blobSource: ReturnType<typeof createCogntBlobSource>;
  editor: PageEditor;
  selection: EditorSelectionController;
  interactions: EditorInteractionController;
  runtime: EditorRuntimeCoordinator;
  mentions: MentionController;
};

export function createEditorFoundation(config: EditorFoundationConfig): EditorFoundation {
  const { options } = config;
  const blobSource = createCogntBlobSource({
    getSourcePath: () => config.getCurrentPath(),
    storeBlob: message => options.onStoreEditorBlob(message),
    readBlob: message => options.onReadEditorBlob(message),
    listBlobs: message => options.onListEditorBlobs(message),
  });
  const editor = document.createElement('page-editor') as PageEditor;
  editor.id = 'editor';
  editor.doc = createEmptyMarkdownDoc(blobSource);
  config.trackCurrentDoc(editor.doc);
  editor.hasViewport = true;

  const selection = new EditorSelectionController({
    editor,
    getCurrentDoc: () => config.getCurrentDoc(),
    getCurrentCollectionRoot: () => config.getCurrentCollectionRoot(),
  });
  const runtime = new EditorRuntimeCoordinator({
    editor,
    statusElement: options.statusElement,
    getCurrentDoc: () => config.getCurrentDoc(),
    getCurrentPath: () => config.getCurrentPath(),
    getCurrentCollectionRoot: () => config.getCurrentCollectionRoot(),
    ensureReferencesBlock: () => config.ensureReferencesBlock(),
    shouldPatchLinkedDocBlocks: () => config.shouldPatchLinkedDocBlocks(),
  });
  const interactions = new EditorInteractionController({
    editor,
    getCurrentDoc: () => config.getCurrentDoc(),
    getTextSelection: () => selection.getTextSelection(),
    getCurrentInsertionBlockModel: () => selection.getCurrentInsertionBlockModel(),
    resolveOutlineBlockId: blockId => selection.resolveOutlineBlockId(blockId),
    getRenderedBlockElement: blockId => selection.getRenderedBlockElement(blockId),
    showStatus: message => runtime.showStatus(message),
  });
  const mentions = new MentionController({
    editor,
    getCurrentPath: () => config.getCurrentPath(),
    flushHost: () => runtime.flushHost(),
    scheduleContentChanged: () => config.scheduleContentChanged(),
    showStatus: message => runtime.showStatus(message),
    onSearchTargets: async message => {
      const targets = await options.onSearchMentionTargets(message);
      return targets;
    },
  });

  return {
    blobSource,
    editor,
    selection,
    interactions,
    runtime,
    mentions,
  };
}
