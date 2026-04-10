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
  upgradeSerializedCogntPropertiesSnapshots,
} from '../blocks/properties';
import { stripSerializedCogntReferencesSnapshots } from '../blocks/references';
import {
  cogntBlockSuiteDocumentKind,
  cogntBlockSuiteDocumentVersion,
  isBlockSnapshot,
  isBlockSuiteJsonPath,
  isPlainRecord,
} from './block-suite-document';
import {
  hydrateCogntEmbedLinkedNotes,
  registerSerializedCogntEmbedLinkedNotes,
  registerSerializedCogntInlineLinkedNotes,
  upgradeRuntimeRawCogntInlineMentions,
  upgradeSerializedRawCogntInlineMentionSnapshots,
  type CogntLinkedDoc,
  type LinkedNoteTitleResolver,
} from '../links/linked-notes';

type CogntBlockSuiteDocument = {
  kind?: string;
  version?: number;
  snapshot?: DocSnapshot | null;
};

export function createEmptyMarkdownDoc() {
  const schema = new Schema().register(createCogntSchemas());
  const collection = new DocCollection({ schema });
  collection.meta.initialize();
  const doc = collection.createDoc();
  doc.load();
  const rootId = doc.addBlock('affine:page', {});
  doc.addBlock('affine:surface', {}, rootId);
  const noteId = doc.addBlock('affine:note', {}, rootId);
  doc.addBlock('affine:paragraph', {}, noteId);
  return doc;
}

export async function createDocFromBlockSuiteJson(
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const snapshot = parseBlockSuiteDocumentSnapshot(content);
  if (!snapshot) {
    const doc = createEmptyMarkdownDoc();
    doc.resetHistory();
    return doc;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  upgradeSerializedCogntPropertiesSnapshots(snapshot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const seedDoc = createEmptyMarkdownDoc();
  const job = new Job({ collection: seedDoc.collection });
  const doc = await job.snapshotToDoc(snapshot);
  if (!doc) {
    throw new Error('BlockSuite could not load the saved document snapshot.');
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
  registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
  doc.resetHistory();
  return doc;
}

export async function importBlockSuiteJsonIntoExistingDoc(
  doc: Doc,
  content: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void
) {
  const note = clearNoteChildren(doc);
  const snapshot = parseBlockSuiteDocumentSnapshot(content);

  if (!snapshot) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    doc.resetHistory();
    return;
  }

  upgradeSerializedRawCogntInlineMentionSnapshots(snapshot);
  upgradeSerializedCogntPropertiesSnapshots(snapshot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const sourceNote = findFirstBlockSnapshot(snapshot.blocks, 'affine:note');
  const children = sourceNote?.children?.filter(isBlockSnapshot) ?? [];

  if (children.length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    const job = new Job({ collection: doc.collection });
    for (let index = 0; index < children.length; index++) {
      await job.snapshotToBlock(children[index], doc, note.id, index);
    }
  }

  registerSerializedCogntEmbedLinkedNotes(doc, onLinkedDoc);
  registerSerializedCogntInlineLinkedNotes(doc);
  doc.resetHistory();
}

export async function createDocFromMarkdown(
  markdown: string,
  onLinkedDoc?: (linkedDoc: CogntLinkedDoc) => void,
  resolveTitle?: LinkedNoteTitleResolver
) {
  const doc = createEmptyMarkdownDoc();
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc, onLinkedDoc, resolveTitle);
    upgradeRuntimeRawCogntInlineMentions(doc, resolveTitle);
    registerSerializedCogntInlineLinkedNotes(doc, resolveTitle);
  }

  doc.resetHistory();
  return doc;
}

export async function importMarkdownIntoExistingDoc(doc: Doc, markdown: string) {
  const note = clearNoteChildren(doc);

  if (markdown.trim().length === 0) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
  } else {
    await MarkdownTransformer.importMarkdownToBlock({
      doc,
      markdown,
      blockId: note.id,
    });
    hydrateCogntEmbedLinkedNotes(doc);
    upgradeRuntimeRawCogntInlineMentions(doc);
    registerSerializedCogntInlineLinkedNotes(doc);
  }

  doc.resetHistory();
}

export async function exportDocContent(
  doc: Doc | null,
  path: string | null | undefined,
  collectionRoot?: string | null
) {
  return isBlockSuiteJsonPath(path)
    ? exportDocBlockSuiteJson(doc, collectionRoot)
    : exportDocMarkdown(doc, collectionRoot);
}

async function exportDocMarkdown(doc: Doc | null, collectionRoot?: string | null) {
  if (!doc) {
    return '';
  }

  const job = new Job({ collection: doc.collection });
  const snapshot = job.docToSnapshot(doc);

  if (!snapshot) {
    return '';
  }

  normalizeSerializedImageSources(snapshot.blocks, collectionRoot);
  stripSerializedCogntReferencesSnapshots(snapshot);
  const adapter = new MarkdownAdapter(job, defaultBlockMarkdownAdapterMatchers);
  const result = await adapter.fromDocSnapshot({
    snapshot,
    assets: job.assetsManager,
  });

  return result.file.trimEnd();
}

async function exportDocBlockSuiteJson(doc: Doc | null, collectionRoot?: string | null) {
  if (!doc) {
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
    upgradeSerializedCogntPropertiesSnapshots(snapshot);
    stripSerializedCogntReferencesSnapshots(snapshot);
  }
  const payload: CogntBlockSuiteDocument = {
    kind: cogntBlockSuiteDocumentKind,
    version: cogntBlockSuiteDocumentVersion,
    snapshot: snapshot ?? null,
  };

  return `${JSON.stringify(payload, null, 2)}\n`;
}

function clearNoteChildren(doc: Doc) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;

  if (!note) {
    throw new Error('BlockSuite note block was not created.');
  }

  for (const child of [...note.children]) {
    doc.deleteBlock(child as DraftModel, { deleteChildren: true });
  }

  return note;
}

function parseBlockSuiteDocumentSnapshot(content: string): DocSnapshot | null {
  const trimmed = content.trim();
  if (trimmed.length === 0) {
    return null;
  }

  const parsed = JSON.parse(trimmed) as unknown;
  if (isDocSnapshot(parsed)) {
    return parsed;
  }

  if (!isPlainRecord(parsed)) {
    throw new Error('The note file is not a BlockSuite JSON document.');
  }

  const document = parsed as CogntBlockSuiteDocument;
  if (typeof document.kind === 'string' && document.kind !== cogntBlockSuiteDocumentKind) {
    throw new Error('The note file was saved by an unsupported document format.');
  }

  if (
    typeof document.version === 'number' &&
    document.version > cogntBlockSuiteDocumentVersion
  ) {
    throw new Error('The note file was saved by a newer BlockSuite document format.');
  }

  if (document.snapshot == null) {
    return null;
  }

  if (!isDocSnapshot(document.snapshot)) {
    throw new Error('The BlockSuite document snapshot is invalid.');
  }

  return document.snapshot;
}

function isDocSnapshot(value: unknown): value is DocSnapshot {
  return isPlainRecord(value)
    && value.type === 'page'
    && isPlainRecord(value.meta)
    && isBlockSnapshot(value.blocks);
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

function normalizeSerializedImageSources(block: BlockSnapshot, collectionRoot?: string | null) {
  if (block.flavour === 'affine:image') {
    const sourceId = typeof block.props.sourceId === 'string'
      ? block.props.sourceId
      : '';
    const relativeSource = normalizeImageSourceForStorage(sourceId, collectionRoot);
    if (relativeSource) {
      block.props.sourceId = relativeSource;
    }
  }

  for (const child of block.children) {
    if (isBlockSnapshot(child)) {
      normalizeSerializedImageSources(child, collectionRoot);
    }
  }
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
