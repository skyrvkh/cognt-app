import {
  defaultBlockMarkdownAdapterMatchers,
  MarkdownAdapter,
  MarkdownTransformer,
} from '@blocksuite/blocks';
import {
  DocCollection,
  Job,
  Schema,
  type BlockSnapshot,
  type Doc,
  type DocSnapshot,
  type DraftModel,
} from '@blocksuite/store';
import {
  createCogntSchemas,
  stripSerializedCogntPropertiesSnapshots,
} from '../blocks/properties';
import { stripSerializedCogntReferencesSnapshots } from '../blocks/references';
import type { BlobSource } from '../editor/blob-source';
import { reportDiagnostic } from '../host';
import {
  hydrateCogntEmbedLinkedNotes,
  registerSerializedCogntEmbedLinkedNotes,
  registerSerializedCogntInlineLinkedNotes,
  upgradeRuntimeRawCogntInlineMentions,
  upgradeSerializedRawCogntInlineMentionSnapshots,
  type CogntLinkedDoc,
  type LinkedNoteTitleResolver,
} from '../links/linked-notes';
import {
  cogntBlockSuiteDocumentKind,
  cogntBlockSuiteDocumentVersion,
  isBlockSnapshot,
  isBlockSuiteJsonPath,
  isPlainRecord,
} from './block-suite-document';

type CogntBlockSuiteDocument = {
  kind?: string;
  version?: number;
  snapshot?: unknown | null;
};

export function createEmptyMarkdownDoc(blobSource?: BlobSource) {
  reportDiagnostic('info', 'Creating empty BlockSuite markdown document.');
  const schema = new Schema().register(createCogntSchemas());
  const collection = new DocCollection({
    schema,
    ...(blobSource ? { blobSources: { main: blobSource } } : {}),
  });
  collection.meta.initialize();
  const doc = collection.createDoc();
  doc.load();
  const rootId = doc.addBlock('affine:page', {});
  doc.addBlock('affine:surface', {}, rootId);
  const noteId = doc.addBlock('affine:note', {}, rootId);
  doc.addBlock('affine:paragraph', {}, noteId);
  reportDiagnostic(
    'info',
    `Created empty BlockSuite markdown document (root=${rootId}, note=${noteId}).`
  );
  return doc;
}

export async function exportDocContent(
  doc: Doc | null,
  path: string | null | undefined,
  collectionRoot?: string | null
) {
  reportDiagnostic(
    'info',
    `Exporting document content (path=${path ?? ''}, mode=${isBlockSuiteJsonPath(path) ? 'blocksuite-json' : 'markdown'}, hasDoc=${Boolean(doc)}, hasCollectionRoot=${Boolean(collectionRoot)}).`
  );
  return isBlockSuiteJsonPath(path)
    ? exportDocBlockSuiteJson(doc, collectionRoot)
    : exportDocMarkdown(doc, collectionRoot);
}

export async function createDocFromBlockSuiteJson(
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver,
  blobSource?: BlobSource
) {
  reportDiagnostic('info', `Creating document from BlockSuite JSON (length=${content.length}).`);
  const snapshot = parseBlockSuiteDocumentSnapshot(content);
  if (!snapshot) {
    reportDiagnostic('warn', 'BlockSuite JSON content produced no snapshot; falling back to empty document.');
    const doc = createEmptyMarkdownDoc(blobSource);
    doc.resetHistory();
    return doc;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  stripSerializedCogntPropertiesSnapshots(snapshot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const seedDoc = createEmptyMarkdownDoc(blobSource);
  const job = new Job({ collection: seedDoc.collection });
  const doc = await job.snapshotToDoc(snapshot);
  if (!doc) {
    reportDiagnostic('error', 'BlockSuite snapshotToDoc returned null while loading saved document.');
    throw new Error('BlockSuite could not load the saved document snapshot.');
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
  registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
  doc.resetHistory();
  reportDiagnostic('info', 'Created document from BlockSuite JSON snapshot.');
  return doc;
}

export async function importBlockSuiteJsonIntoExistingDoc(
  doc: Doc,
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void
) {
  reportDiagnostic('info', `Importing BlockSuite JSON into existing document (length=${content.length}).`);
  const note = clearNoteChildren(doc);
  const snapshot = parseBlockSuiteDocumentSnapshot(content);

  if (!snapshot) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    doc.resetHistory();
    reportDiagnostic('info', 'Imported empty BlockSuite JSON content into existing document.');
    return;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  stripSerializedCogntPropertiesSnapshots(snapshot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const sourceNote = findFirstBlockSnapshot(snapshot.blocks, 'affine:note');
  const children = sourceNote?.children ?? [];

  if (children.length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    reportDiagnostic('warn', 'BlockSuite import snapshot had no note children; inserted empty paragraph.');
  } else {
    const job = new Job({ collection: doc.collection });
    for (let index = 0; index < children.length; index++) {
      await job.snapshotToBlock(children[index], doc, note.id, index);
    }
    reportDiagnostic(
      'info',
      `Imported BlockSuite JSON children into existing document (children=${children.length}).`
    );
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc);
  registerSerializedCogntInlineLinkedNotes(doc);
  doc.resetHistory();
  reportDiagnostic('info', 'Completed BlockSuite JSON import into existing document.');
}

export async function createDocFromMarkdown(
  markdown: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver,
  blobSource?: BlobSource
) {
  reportDiagnostic('info', `Creating document from markdown (length=${markdown.length}).`);
  const doc = createEmptyMarkdownDoc(blobSource);
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    reportDiagnostic('info', 'Markdown import content was empty; inserted empty paragraph.');
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
    upgradeRuntimeRawCogntInlineMentions(doc, resolveTitle);
    registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
    reportDiagnostic('info', 'Markdown document imported and linked-note metadata hydrated.');
  }

  doc.resetHistory();
  reportDiagnostic('info', 'Created document from markdown.');
  return doc;
}

export async function importMarkdownIntoExistingDoc(doc: Doc, markdown: string) {
  reportDiagnostic('info', `Importing markdown into existing document (length=${markdown.length}).`);
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    reportDiagnostic('info', 'Markdown import into existing document was empty; inserted empty paragraph.');
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc);
    upgradeRuntimeRawCogntInlineMentions(doc);
    registerSerializedCogntInlineLinkedNotes(doc);
    reportDiagnostic('info', 'Markdown imported into existing document and linked-note metadata hydrated.');
  }

  doc.resetHistory();
  reportDiagnostic('info', 'Completed markdown import into existing document.');
}

async function exportDocMarkdown(doc: Doc | null, collectionRoot?: string | null) {
  if (!doc) {
    reportDiagnostic('warn', 'Markdown export returned empty string because document was null.');
    return '';
  }

  const job = new Job({ collection: doc.collection });
  const snapshot = job.docToSnapshot(doc);

  if (!snapshot) {
    reportDiagnostic('warn', 'Markdown export returned empty string because doc snapshot was null.');
    return '';
  }

  normalizeSerializedImageSources(snapshot.blocks, collectionRoot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const adapter = new MarkdownAdapter(job, defaultBlockMarkdownAdapterMatchers);
  const result = await adapter.fromDocSnapshot({
    snapshot,
    assets: job.assetsManager,
  });

  const output = result.file.trimEnd();
  reportDiagnostic('info', `Markdown export completed (length=${output.length}).`);
  return output;
}

async function exportDocBlockSuiteJson(doc: Doc | null, collectionRoot?: string | null) {
  if (!doc) {
    reportDiagnostic('warn', 'BlockSuite JSON export requested with null document.');
    return `${JSON.stringify(
      {
        kind: cogntBlockSuiteDocumentKind,
        version: cogntBlockSuiteDocumentVersion,
        snapshot: null,
      },
      null,
      2
    )}\n`;
  }

  const job = new Job({ collection: doc.collection });
  const snapshot = job.docToSnapshot(doc);
  if (snapshot) {
    normalizeSerializedImageSources(snapshot.blocks, collectionRoot);
    stripSerializedCogntPropertiesSnapshots(snapshot);
    stripSerializedCogntReferencesSnapshots(snapshot);
  } else {
    reportDiagnostic('warn', 'BlockSuite JSON export had no snapshot.');
  }

  const payload: CogntBlockSuiteDocument = {
    kind: cogntBlockSuiteDocumentKind,
    version: cogntBlockSuiteDocumentVersion,
    snapshot: snapshot ?? null,
  };

  const output = `${JSON.stringify(payload, null, 2)}\n`;
  reportDiagnostic(
    'info',
    `BlockSuite JSON export completed (length=${output.length}, hasSnapshot=${Boolean(snapshot)}).`
  );
  return output;
}

function clearNoteChildren(doc: Doc) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    reportDiagnostic('error', 'Cannot clear note children because note block was not created.');
    throw new Error('BlockSuite note block was not created.');
  }

  reportDiagnostic('info', `Clearing note children before import (childCount=${note.children.length}).`);
  for (const child of [...note.children]) {
    doc.deleteBlock(child as DraftModel, { deleteChildren: true });
  }

  return note;
}

function parseBlockSuiteDocumentSnapshot(content: string): DocSnapshot | null {
  const trimmed = content.trim();
  if (trimmed.length === 0) {
    reportDiagnostic('info', 'BlockSuite document snapshot parse skipped: content is empty.');
    return null;
  }

  const parsed = JSON.parse(trimmed) as unknown;
  if (isDocSnapshot(parsed)) {
    reportDiagnostic('info', 'Parsed legacy/raw BlockSuite document snapshot.');
    return parsed;
  }

  if (!isPlainRecord(parsed)) {
    reportDiagnostic('error', 'BlockSuite document snapshot parse failed: root value is not an object.');
    throw new Error('The note file is not a BlockSuite JSON document.');
  }

  const document = parsed as CogntBlockSuiteDocument;
  if (typeof document.kind === 'string' && document.kind !== cogntBlockSuiteDocumentKind) {
    reportDiagnostic('error', `BlockSuite document snapshot parse failed: unsupported kind=${document.kind}.`);
    throw new Error('The note file was saved by an unsupported document format.');
  }

  if (
    typeof document.version === 'number'
    && document.version > cogntBlockSuiteDocumentVersion
  ) {
    reportDiagnostic('error', `BlockSuite document snapshot parse failed: newer version=${document.version}.`);
    throw new Error('The note file was saved by a newer BlockSuite document format.');
  }

  if (document.snapshot == null) {
    reportDiagnostic('warn', 'BlockSuite document snapshot parse returned null snapshot.');
    return null;
  }

  if (!isDocSnapshot(document.snapshot)) {
    reportDiagnostic('error', 'BlockSuite document snapshot parse failed: snapshot shape is invalid.');
    throw new Error('The BlockSuite document snapshot is invalid.');
  }

  reportDiagnostic('info', `Parsed BlockSuite document snapshot (version=${document.version ?? 'legacy'}).`);
  return document.snapshot;
}

function findFirstBlockSnapshot(block: BlockSnapshot, flavour: string): BlockSnapshot | null {
  if (block.flavour === flavour) {
    return block;
  }

  for (const child of block.children) {
    if (!isBlockSnapshot(child)) {
      continue;
    }

    const result = findFirstBlockSnapshot(child, flavour);
    if (result) {
      return result;
    }
  }

  return null;
}

function normalizeSerializedImageSources(
  block: BlockSnapshot,
  collectionRoot?: string | null
) {
  if (block.flavour === 'affine:image') {
    const sourceId = typeof block.props.sourceId === 'string'
      ? block.props.sourceId
      : '';
    const relativeSource = normalizeImageSourceForStorage(sourceId, collectionRoot);
    if (relativeSource) {
      block.props.sourceId = relativeSource;
      reportDiagnostic(
        'info',
        `Normalized serialized image source (block=${block.id}, sourceLength=${relativeSource.length}).`
      );
    }
  }

  for (const child of block.children) {
    if (isBlockSnapshot(child)) {
      normalizeSerializedImageSources(child, collectionRoot);
    }
  }
}

function isDocSnapshot(value: unknown): value is DocSnapshot {
  return isPlainRecord(value)
    && value.type === 'page'
    && isPlainRecord(value.meta)
    && isBlockSnapshot(value.blocks);
}

function normalizeImageSourceForStorage(sourceId: string, collectionRoot?: string | null) {
  const normalizedSource = sourceId.replace(/\\/g, '/').trim();
  if (normalizedSource.length === 0) {
    return null;
  }

  const filePath = tryGetFileUriPath(normalizedSource);
  if (filePath && collectionRoot) {
    const relativePath = tryGetCollectionRelativePath(filePath, collectionRoot);
    if (relativePath) {
      return relativePath;
    }
  }

  return normalizedSource;
}

function tryGetFileUriPath(source: string) {
  try {
    const uri = new URL(source);
    if (uri.protocol !== 'file:') {
      return null;
    }

    return decodeURIComponent(uri.pathname)
      .replace(/^\/([A-Za-z]:\/)/, '$1')
      .replace(/\\/g, '/');
  } catch {
    return null;
  }
}

function tryGetCollectionRelativePath(filePath: string, collectionRoot: string) {
  const root = collectionRoot.replace(/\\/g, '/').replace(/\/+$/, '');
  const fullPath = filePath.replace(/\\/g, '/');
  const rootPrefix = `${root}/`;
  if (root.length === 0 || !fullPath.toLowerCase().startsWith(rootPrefix.toLowerCase())) {
    return null;
  }

  return fullPath.slice(rootPrefix.length).replace(/^\/+/, '');
}
