import { type PageEditor } from '@blocksuite/presets';
import {
  type BlockModel,
  type Doc,
} from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import { reportDiagnostic } from '../host';
import {
  getTextSelectionEnd,
  getTextSelectionStart,
} from './selection-export-selection';

export function getSelectedTextBlockModels(
  editor: PageEditor,
  doc: Doc,
  textSelection: TextSelection
) {
  const siblingModels = getSiblingTextSelectionBlockModels(doc, textSelection);
  if (siblingModels) {
    reportDiagnostic(
      'info',
      `Selected text block models resolved by sibling path (count=${siblingModels.length}).`
    );
    return siblingModels;
  }

  const range = editor.std?.range.textSelectionToRange(textSelection as never);
  const components = range
    ? editor.std.range.getSelectedBlockComponentsByRange(range, {
        match: (element: { model?: { role?: string } }) => element.model?.role === 'content',
        mode: 'flat',
      })
    : [];

  const models = components
    .map((component: { model?: BlockModel }) => component.model)
    .filter((model: BlockModel | undefined): model is BlockModel => Boolean(model));

  if (models.length > 0) {
    const expanded = expandTextSelectionBlockModels(doc, textSelection, uniqueModels(models));
    reportDiagnostic(
      'info',
      `Selected text block models resolved by editor range (initial=${models.length}, expanded=${expanded.length}).`
    );
    return expanded;
  }

  const fallback = getFallbackTextSelectionBlockModels(doc, textSelection);
  reportDiagnostic('info', `Selected text block models resolved by fallback path (count=${fallback.length}).`);
  return fallback;
}

function getSiblingTextSelectionBlockModels(
  doc: Doc,
  textSelection: TextSelection
): BlockModel[] | null {
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  const startModel = doc.getBlock(start.blockId)?.model;
  const endModel = doc.getBlock(end.blockId)?.model;
  if (!startModel || !endModel) {
    return null;
  }

  if (startModel === endModel) {
    return [startModel as BlockModel];
  }

  const startParent = doc.getParent(startModel as never) as { children?: unknown[] } | null;
  const endParent = doc.getParent(endModel as never);
  const siblings = Array.isArray(startParent?.children) ? startParent.children : [];
  const startIndex = siblings.indexOf(startModel);
  const endIndex = siblings.indexOf(endModel);
  if (!startParent || startParent !== endParent || startIndex < 0 || endIndex < 0) {
    return null;
  }

  const firstIndex = Math.min(startIndex, endIndex);
  const lastIndex = Math.max(startIndex, endIndex);
  return siblings
    .slice(firstIndex, lastIndex + 1)
    .filter((model): model is BlockModel => isSelectableTextBlockModel(model));
}

function expandTextSelectionBlockModels(
  doc: Doc,
  textSelection: TextSelection,
  models: BlockModel[]
) {
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  if (start.blockId === end.blockId) {
    return models;
  }

  const startModel = doc.getBlock(start.blockId)?.model;
  const endModel = doc.getBlock(end.blockId)?.model;
  if (!startModel || !endModel) {
    return models;
  }

  const startParent = doc.getParent(startModel as never) as { children?: unknown[] } | null;
  const endParent = doc.getParent(endModel as never);
  const siblings = Array.isArray(startParent?.children) ? startParent.children : [];
  const startIndex = siblings.indexOf(startModel);
  const endIndex = siblings.indexOf(endModel);
  if (!startParent || startParent !== endParent || startIndex < 0 || endIndex < 0) {
    return models;
  }

  const firstIndex = Math.min(startIndex, endIndex);
  const lastIndex = Math.max(startIndex, endIndex);
  return siblings
    .slice(firstIndex, lastIndex + 1)
    .filter((model): model is BlockModel => isSelectableTextBlockModel(model));
}

function getFallbackTextSelectionBlockModels(
  doc: Doc,
  textSelection: TextSelection
) {
  const start = getTextSelectionStart(textSelection);
  const end = getTextSelectionEnd(textSelection);
  const startModel = doc.getBlock(start.blockId)?.model;
  const endModel = doc.getBlock(end.blockId)?.model;

  if (!startModel || !endModel) {
    const fallback = doc.getBlock(textSelection.from.blockId)?.model;
    return fallback ? [fallback as BlockModel] : [];
  }

  if (startModel === endModel) {
    return [startModel as BlockModel];
  }

  const startParent = doc.getParent(startModel as never) as { children?: unknown[] } | null;
  const endParent = doc.getParent(endModel as never);
  const siblings = Array.isArray(startParent?.children) ? startParent.children : [];
  const startIndex = siblings.indexOf(startModel);
  const endIndex = siblings.indexOf(endModel);

  if (startParent && startParent === endParent && startIndex >= 0 && endIndex >= 0) {
    const firstIndex = Math.min(startIndex, endIndex);
    const lastIndex = Math.max(startIndex, endIndex);
    return siblings
      .slice(firstIndex, lastIndex + 1)
      .filter((model): model is BlockModel => isSelectableTextBlockModel(model));
  }

  return uniqueModels([startModel as BlockModel, endModel as BlockModel]);
}

function uniqueModels(models: BlockModel[]) {
  const result: BlockModel[] = [];
  const seen = new Set<BlockModel>();
  for (const model of models) {
    if (seen.has(model)) {
      continue;
    }

    seen.add(model);
    result.push(model);
  }

  return result;
}

function isSelectableTextBlockModel(value: unknown): value is BlockModel {
  return typeof value === 'object'
    && value !== null
    && typeof (value as { id?: unknown }).id === 'string'
    && Boolean((value as { text?: unknown }).text);
}
