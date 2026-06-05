import type {
  TextSelection,
  TextSelectionRange,
} from './cloze-types';
import {
  canContainTextNodes,
} from './cloze-dom-shared';

export function getTextSelectionRange(textSelection: TextSelection): TextSelectionRange | null {
  const start = textSelection.start ?? textSelection.from;
  const end = textSelection.end ?? textSelection.to ?? textSelection.from;

  if (start.blockId !== end.blockId) {
    return null;
  }

  const index = Math.min(start.index, end.index);
  const selectionEnd = Math.max(start.index + start.length, end.index + end.length);
  const length = selectionEnd - index;

  return length > 0
    ? {
        blockId: start.blockId,
        index,
        length,
      }
    : null;
}

export function normalizeSelectionText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

export function collectClozeTextNodes(root: Node) {
  const nodes: Text[] = [];

  const visit = (node: Node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      if ((node.textContent ?? '').includes('{{')) {
        nodes.push(node as Text);
      }
      return;
    }

    if (!canContainTextNodes(node)) {
      return;
    }

    if (node instanceof Element && node.shadowRoot) {
      visit(node.shadowRoot);
    }

    const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);

    for (let current = walker.nextNode(); current; current = walker.nextNode()) {
      if (current.nodeType === Node.TEXT_NODE) {
        const textNode = current as Text;
        if ((textNode.textContent ?? '').includes('{{')) {
          nodes.push(textNode);
        }
        continue;
      }

      const element = current as Element;
      if (element.shadowRoot) {
        visit(element.shadowRoot);
      }
    }
  };

  visit(root);
  return nodes;
}
