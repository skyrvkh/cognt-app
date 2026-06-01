import { MarkdownTransformer } from '@blocksuite/blocks';
import { getInlineEditorByModel } from '@blocksuite/affine-components/rich-text';
import type { PageEditor } from '@blocksuite/presets';
import type { BlockModel, Doc } from '@blocksuite/store';
import type { AttachFileResult } from './contracts';
import { getEventBlockModel } from './block-models';
import { registerImageAttachmentBlob } from './image-attachments';

export type ImageAttachmentInsertPayload = {
  result: AttachFileResult & { relativePath: string };
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

export function tryInsertInlineAttachmentReference(
  editor: PageEditor,
  event: Event,
  referenceText: string
) {
  const model = getEventBlockModel(event);
  return model
    ? tryInsertAttachmentReferenceIntoTextModel(editor, model, referenceText)
    : false;
}

export function tryInsertAttachmentReferenceIntoTextModel(
  editor: PageEditor,
  model: BlockModel,
  referenceText: string
) {
  const host = editor.host;
  const inlineEditor = host ? getInlineEditorByModel(host, model) : null;
  const range = inlineEditor?.getInlineRange();
  if (inlineEditor && range) {
    inlineEditor.insertText(range, referenceText);
    inlineEditor.setInlineRange({
      index: range.index + referenceText.length,
      length: 0,
    });
    return true;
  }

  const text = (model as {
    text?: {
      length?: number;
      replace(index: number, length: number, content: string): void;
    };
  }).text;
  if (text && typeof text.replace === 'function') {
    text.replace(text.length ?? 0, 0, referenceText);
    return true;
  }

  return false;
}

export async function tryInsertImageAttachmentBlockAfterModel(
  doc: Doc,
  editor: PageEditor,
  model: BlockModel,
  payload: ImageAttachmentInsertPayload,
  shouldSelectBlock: () => boolean
) {
  const parent = doc.getParent(model);
  if (!parent) {
    return null;
  }

  const index = parent.children.indexOf(model);
  return await insertImageAttachmentBlock(
    doc,
    editor,
    parent,
    Math.max(0, index + 1),
    payload,
    shouldSelectBlock);
}

export async function appendImageAttachmentBlock(
  doc: Doc,
  editor: PageEditor,
  payload: ImageAttachmentInsertPayload,
  shouldSelectBlock: () => boolean
) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;
  if (!note) {
    return null;
  }

  return await insertImageAttachmentBlock(
    doc,
    editor,
    note,
    note.children.length,
    payload,
    shouldSelectBlock);
}

export async function insertImageAttachmentBlock(
  doc: Doc,
  editor: PageEditor,
  parent: BlockModel,
  index: number,
  payload: ImageAttachmentInsertPayload,
  shouldSelectBlock: () => boolean
) {
  const relativePath = await registerImageAttachmentBlob(
    doc,
    payload.result,
    payload.fallbackName,
    payload.sourceFile,
    payload.contentBase64);

  return insertPersistedImageAttachmentBlock(
    doc,
    editor,
    parent,
    index,
    relativePath,
    payload.sourceFile.size,
    shouldSelectBlock);
}

export async function appendAttachmentReferenceBlock(doc: Doc, referenceText: string) {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;
  if (!note) {
    return;
  }

  await MarkdownTransformer.importMarkdownToBlock({
    doc,
    markdown: referenceText,
    blockId: note.id,
  });
}

function insertPersistedImageAttachmentBlock(
  doc: Doc,
  editor: PageEditor,
  parent: BlockModel,
  index: number,
  relativePath: string,
  size: number | undefined,
  shouldSelectBlock: () => boolean
) {
  doc.captureSync();
  const blockId = doc.addBlock(
    'affine:image' as never,
    {
      sourceId: relativePath,
      ...(typeof size === 'number' && size > 0 ? { size } : {}),
    } as never,
    parent,
    index
  );

  if (blockId && shouldSelectBlock()) {
    editor.host?.selection.setGroup('note', [
      editor.host.selection.create('block', { blockId }),
    ]);
  }

  return blockId;
}
