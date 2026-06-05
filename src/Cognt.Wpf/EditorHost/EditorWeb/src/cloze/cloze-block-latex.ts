import type { Doc } from '@blocksuite/store';
import type {
  LatexBlockModel,
  LatexBlockSelectionCandidate,
} from './cloze-types';
import {
  findAncestorElement,
  getSelectionAnchorElement,
  isNodeInsideRoot,
} from './cloze-dom-shared';

export function getLatexBlockSelectionCandidate(
  root: Node,
  doc: Doc | null
): LatexBlockSelectionCandidate | null {
  if (!doc) {
    return null;
  }

  const element = getSelectedLatexBlockElement(root);
  const model = getLatexBlockModel(element);
  const blockId = model?.id?.trim();
  const latex = getLatexBlockText(model);
  if (!element || !model || !blockId || !latex) {
    return null;
  }

  return {
    doc,
    element,
    model,
    blockId,
    latex,
  };
}

function getSelectedLatexBlockElement(root: Node): Element | null {
  const selectionElement = getSelectionAnchorElement();
  const selectionBlock = selectionElement
    ? findAncestorElement(selectionElement, isLatexBlockElement)
    : null;
  if (selectionBlock && isNodeInsideRoot(selectionBlock, root)) {
    return selectionBlock;
  }

  if (!(root instanceof Element)) {
    return null;
  }

  for (const element of Array.from(root.querySelectorAll('affine-latex'))) {
    const candidate = element as Element & { isBlockSelected?: boolean };
    if (candidate.isBlockSelected === true) {
      return candidate;
    }
  }

  return null;
}

function getLatexBlockModel(element: Element | null): LatexBlockModel | null {
  if (!element) {
    return null;
  }

  const candidate = element as unknown as {
    model?: LatexBlockModel;
    block?: { model?: LatexBlockModel };
  };
  const model = candidate.model ?? candidate.block?.model ?? null;
  return model?.flavour === 'affine:latex' || element.tagName.toLowerCase() === 'affine-latex'
    ? model
    : null;
}

function getLatexBlockText(model: LatexBlockModel | null) {
  if (!model) {
    return '';
  }

  const signalLatex = typeof model.latex$?.value === 'string' ? model.latex$.value : '';
  const propertyLatex = typeof model.latex === 'string' ? model.latex : '';
  return (signalLatex || propertyLatex).trim();
}

function isLatexBlockElement(element: Element) {
  return element.tagName.toLowerCase() === 'affine-latex';
}
