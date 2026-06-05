import type { Doc } from '@blocksuite/store';

export function getModelById<TModel>(doc: Doc, blockId: string) {
  const block = doc.getBlock(blockId) as { model?: unknown } | null | undefined;
  return (block?.model ?? doc.getBlockById(blockId) ?? null) as TModel | null;
}
