import type {
  InlineLatexNode,
  InlineLatexSelectionCandidate,
} from './cloze-types';
import {
  findAncestorElement,
  getClosestBlockId,
  getSelectionAnchorElement,
  isNodeInsideRoot,
} from './cloze-dom-shared';

export function getInlineLatexSelectionCandidate(root: Node): InlineLatexSelectionCandidate | null {
  const node = getSelectedInlineLatexNode(root);
  if (!node) {
    return null;
  }

  const latex = getInlineLatexText(node);
  const blockId = getClosestBlockId(node);
  const startOffset = typeof node.startOffset === 'number' ? node.startOffset : Number.NaN;
  const endOffset = typeof node.endOffset === 'number' ? node.endOffset : Number.NaN;
  if (!latex || !blockId || !Number.isFinite(startOffset) || !Number.isFinite(endOffset)) {
    return null;
  }

  return {
    node,
    blockId,
    latex,
    startOffset,
    length: Math.max(1, endOffset - startOffset),
  };
}

function getSelectedInlineLatexNode(root: Node): InlineLatexNode | null {
  const selectionElement = getSelectionAnchorElement();
  const selectionNode = selectionElement
    ? (findAncestorElement(selectionElement, isInlineLatexElement) as InlineLatexNode | null)
    : null;
  if (selectionNode && isNodeInsideRoot(selectionNode, root)) {
    return selectionNode;
  }

  if (root instanceof Element) {
    const selectedInner = root.querySelector('affine-latex-node .affine-latex[data-selected="true"]');
    const selectedNode = selectedInner?.closest('affine-latex-node') as InlineLatexNode | null;
    if (selectedNode) {
      return selectedNode;
    }
  }

  return null;
}

function getInlineLatexText(node: InlineLatexNode) {
  const deltaLatex = typeof node.deltaLatex === 'string' ? node.deltaLatex : '';
  const attributeLatex =
    typeof node.delta?.attributes?.latex === 'string' ? node.delta.attributes.latex : '';
  return (deltaLatex || attributeLatex).trim();
}

function isInlineLatexElement(element: Element) {
  return element.tagName.toLowerCase() === 'affine-latex-node';
}
