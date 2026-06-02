import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';

type EditableTextBlockText = {
  length?: number;
  replace(
    index: number,
    length: number,
    content: string,
    attributes?: Record<string, unknown>
  ): void;
  toString?(): string;
};

export type EditableTextBlock = {
  id?: string;
  text?: EditableTextBlockText;
  children?: unknown[];
};

type BlockParent = {
  id?: string;
  children?: unknown[];
};

export type SortedBlockEntry = {
  model: EditableTextBlock;
  parent: BlockParent;
  index: number;
};

export function getUniqueBlockIds(values: string[]) {
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    const blockId = value.trim();
    if (!blockId || seen.has(blockId)) {
      continue;
    }

    seen.add(blockId);
    ids.push(blockId);
  }

  return ids;
}

export function getSortedBlockEntries(
  doc: Doc,
  blockIds: string[]
): SortedBlockEntry[] {
  const models = getUniqueBlockIds(blockIds)
    .map(blockId => doc.getBlockById(blockId) as EditableTextBlock | null)
    .filter((model): model is EditableTextBlock => model !== null);
  if (models.length === 0) {
    return [];
  }

  const entries = models.map(model => ({
    model,
    parent: doc.getParent(model as never) as BlockParent | null,
  }));
  if (entries.some(entry => !entry.parent)) {
    reportDiagnostic('error', 'Extract block replacement failed: selected block parent not found.');
    throw new Error('could not find the selected block parent');
  }

  const parent = entries[0].parent;
  if (!parent || entries.some(entry => entry.parent !== parent)) {
    reportDiagnostic('error', 'Extract block replacement failed: selected blocks do not share one parent.');
    throw new Error('selected blocks must share one parent');
  }

  const children = Array.isArray(parent.children) ? parent.children : [];
  return entries
    .map(entry => ({
      model: entry.model,
      parent,
      index: children.indexOf(entry.model),
    }))
    .sort((left, right) => left.index - right.index);
}

export function getEditableTextBlock(
  doc: Doc,
  blockId: string
): EditableTextBlock | null {
  const model = doc.getBlockById(blockId) as EditableTextBlock | null;
  return model?.text ? model : null;
}

export function getBlockModelId(model: unknown) {
  return typeof (model as { id?: unknown } | null)?.id === 'string'
    ? (model as { id: string }).id
    : '';
}

export function getEditableTextLength(model: EditableTextBlock) {
  if (typeof model.text?.length === 'number') {
    return Math.max(0, model.text.length);
  }

  const text = typeof model.text?.toString === 'function'
    ? model.text.toString()
    : '';
  return text === '[object Object]' ? 0 : text.length;
}

export function hasEditableText(
  model: EditableTextBlock
): model is EditableTextBlock & { text: NonNullable<EditableTextBlock['text']> } {
  return Boolean(model.text);
}

export function clampTextIndex(index: number, textLength: number) {
  return Math.max(0, Math.min(Math.max(0, index), textLength));
}

export function isEmptyEditableTextBlock(model: EditableTextBlock) {
  return getEditableTextLength(model) === 0
    && (!Array.isArray(model.children) || model.children.length === 0);
}
