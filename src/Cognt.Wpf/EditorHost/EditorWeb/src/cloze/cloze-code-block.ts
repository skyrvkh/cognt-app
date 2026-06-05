import type {
  NativeCodeBlockSelectionCandidate,
  TextReplacementModel,
} from './cloze-types';
import {
  collectTextNodesDeep,
  findAncestorElement,
  getElementFromNode,
  isNodeInsideRoot,
} from './cloze-dom-shared';

export function getNativeCodeBlockSelectionCandidate(
  root: Node
): NativeCodeBlockSelectionCandidate | null {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0 || selection.isCollapsed) {
    return null;
  }

  const range = selection.getRangeAt(0);
  const anchorElement = getElementFromNode(selection.anchorNode);
  const codeElement = anchorElement ? findAncestorElement(anchorElement, isCodeBlockElement) : null;
  const model = getTextReplacementModel(codeElement);
  if (!codeElement || !model?.id || !model.text) {
    return null;
  }

  if (!isNodeInsideRoot(codeElement, root)) {
    return null;
  }

  const replacementRange = getTextNodeRangeWithinElement(codeElement, range);
  if (!replacementRange || replacementRange.length <= 0) {
    return null;
  }

  return {
    model,
    range: replacementRange,
  };
}

function isCodeBlockElement(element: Element) {
  return element.tagName.toLowerCase() === 'affine-code';
}

function getTextReplacementModel(element: Element | null): TextReplacementModel | null {
  if (!element) {
    return null;
  }

  const candidate = element as unknown as {
    model?: TextReplacementModel;
    block?: { model?: TextReplacementModel };
  };

  const model = candidate.model ?? candidate.block?.model ?? null;
  return model?.flavour === 'affine:code' || element.tagName.toLowerCase() === 'affine-code'
    ? model
    : null;
}

function getTextNodeRangeWithinElement(element: Element, range: Range) {
  const textNodes = collectTextNodesDeep(element);
  let startIndex: number | null = null;
  let endIndex: number | null = null;
  let cursor = 0;

  for (const textNode of textNodes) {
    const length = textNode.textContent?.length ?? 0;
    if (textNode === range.startContainer) {
      startIndex = cursor + range.startOffset;
    }

    if (textNode === range.endContainer) {
      endIndex = cursor + range.endOffset;
    }

    cursor += length;
  }

  if (startIndex === null || endIndex === null) {
    return null;
  }

  const index = Math.min(startIndex, endIndex);
  const end = Math.max(startIndex, endIndex);
  return {
    index,
    length: end - index,
  };
}
