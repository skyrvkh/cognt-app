import type { Doc } from '@blocksuite/store';
import type { BlobSource } from './blob-source';
import type { MarkdownEditorOptions } from './contracts';
import {
  isBlockSuiteJsonPath,
} from '../documents/block-suite-document';
import {
  createDocFromBlockSuiteJson,
  createDocFromMarkdown,
} from '../documents/io';
import {
  type CogntLinkedDoc,
  type LinkedNoteTitleResolver,
} from '../links/linked-notes';
import { hydrateAttachmentImageBlocks } from './image-source-hydration';
import { logEditorTiming } from './timing';

export type LoadedEditorDocument = {
  doc: Doc;
  importKind: 'blocksuite-json' | 'markdown';
  operation: string;
};

type LoadEditorDocumentOptions = {
  path?: string | null;
  sourcePath: string;
  collectionRoot: string;
  content: string;
  blobSource: BlobSource;
  onLinkedDoc(linkedDoc: CogntLinkedDoc): void;
  resolveTitle: LinkedNoteTitleResolver;
  readAttachment: MarkdownEditorOptions['onReadAttachment'];
};

export async function loadEditorDocumentContent(
  options: LoadEditorDocumentOptions
): Promise<LoadedEditorDocument> {
  const isBlockSuiteJson = isBlockSuiteJsonPath(options.path);
  const operation = isBlockSuiteJson
    ? 'BlockSuite JSON import'
    : 'full Markdown import';
  const startedAt = performance.now();
  const doc = isBlockSuiteJson
    ? await createDocFromBlockSuiteJson(
        options.content,
        options.onLinkedDoc,
        options.resolveTitle,
        options.blobSource)
    : await createDocFromMarkdown(
        options.content,
        options.onLinkedDoc,
        options.resolveTitle,
        options.blobSource);

  await hydrateAttachmentImageBlocks(
    doc,
    options.sourcePath,
    options.collectionRoot,
    options.readAttachment);
  logEditorTiming(operation, startedAt, `${options.content.length} chars`);

  return {
    doc,
    importKind: isBlockSuiteJson ? 'blocksuite-json' : 'markdown',
    operation,
  };
}
