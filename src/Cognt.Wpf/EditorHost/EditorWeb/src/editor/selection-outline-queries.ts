import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { isHeadingBlockModel } from './block-models';

export function resolveOutlineBlockId(
  doc: Doc | null,
  blockId: string,
  outlineHeadingBlockIds: string[]
) {
  const candidate = blockId.trim();
  if (!candidate || !doc) {
    return null;
  }

  if (doc.getBlock(candidate)) {
    return candidate;
  }

  const markdownMatch = /^markdown-heading-(\d+)$/i.exec(candidate);
  if (!markdownMatch) {
    return null;
  }

  const headingIndex = Number.parseInt(markdownMatch[1], 10) - 1;
  if (!Number.isInteger(headingIndex) || headingIndex < 0) {
    return null;
  }

  return outlineHeadingBlockIds[headingIndex] ?? null;
}

export function getRenderedBlockElement(editor: PageEditor, blockId: string) {
  return (editor.std?.view?.getBlock(blockId) as HTMLElement | null | undefined) ?? null;
}

export function getOutlineHeadingBlockIds(doc: Doc | null) {
  return doc
    ?.getBlocks()
    .map(block => (block as { model?: unknown }).model ?? block)
    .filter(isHeadingBlockModel)
    .map(model => model.id) ?? [];
}
