import type { Doc } from '@blocksuite/store';

const clozeHighlightName = 'cognt-cloze';

type TextSelectionPoint = {
  blockId: string;
  index: number;
  length: number;
};

export type TextSelection = {
  from: TextSelectionPoint;
  to: TextSelectionPoint | null;
  start?: TextSelectionPoint;
  end?: TextSelectionPoint;
  isCollapsed(): boolean;
  isInSameBlock(): boolean;
};

type ClozeContext = {
  doc: Doc | null;
  textSelection: TextSelection | null | undefined;
};

export function toClozeText(text: string, index = 1) {
  return `{{c${Math.max(1, index)}::${normalizeSelectionText(text)}}}`;
}

export function replaceSelectionWithCloze(
  context: ClozeContext,
  selectedPlainText: string,
  clozeIndex = 1
) {
  const clozeText = toClozeText(selectedPlainText, clozeIndex);

  if (replaceTextSelectionWithCloze(context, clozeText)) {
    return clozeText;
  }

  throw new Error('Cloze requires a single BlockSuite text selection.');
}

export function getNextClozeIndex(root: Node) {
  let maxIndex = 0;
  const expression = /\{\{c(\d+)::?[^{}\n]*\}\}/g;

  for (const current of collectClozeTextNodes(root)) {
    const text = current.textContent ?? '';
    let match: RegExpExecArray | null;

    while ((match = expression.exec(text)) !== null) {
      maxIndex = Math.max(maxIndex, Number.parseInt(match[1], 10));
    }
  }

  return maxIndex + 1;
}

export function updateClozeHighlights(root: Node) {
  const cssWithHighlights = CSS as typeof CSS & {
    highlights?: {
      set(name: string, highlight: unknown): void;
      delete(name: string): void;
    };
  };
  const highlightConstructor = (globalThis as typeof globalThis & {
    Highlight?: new (...ranges: Range[]) => unknown;
  }).Highlight;

  if (!cssWithHighlights.highlights || !highlightConstructor) {
    return;
  }

  const ranges: Range[] = [];
  const expression = /\{\{(?:c\d+::?)?[^{}\n]+\}\}/g;

  for (const current of collectClozeTextNodes(root)) {
    const text = current.textContent ?? '';
    let match: RegExpExecArray | null;

    while ((match = expression.exec(text)) !== null) {
      const range = document.createRange();
      range.setStart(current, match.index);
      range.setEnd(current, match.index + match[0].length);

      ranges.push(range);
    }
  }

  if (ranges.length === 0) {
    cssWithHighlights.highlights.delete(clozeHighlightName);
    return;
  }

  cssWithHighlights.highlights.set(clozeHighlightName, new highlightConstructor(...ranges));
}

function replaceTextSelectionWithCloze(context: ClozeContext, clozeText: string) {
  if (!context.doc) {
    return false;
  }

  const textSelection = context.textSelection;
  if (!textSelection || textSelection.isCollapsed() || !textSelection.isInSameBlock()) {
    return false;
  }

  const range = getTextSelectionRange(textSelection);
  if (!range) {
    return false;
  }

  const model = context.doc.getBlockById(range.blockId) as
    | { text?: { replace(index: number, length: number, content: string): void } }
    | null;
  if (!model?.text) {
    return false;
  }

  try {
    context.doc.transact(() => {
      model.text!.replace(range.index, range.length, clozeText);
    });
    return true;
  } catch (error) {
    console.warn('Text selection cloze replacement failed.', error);
    return false;
  }
}

function getTextSelectionRange(textSelection: TextSelection) {
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

function normalizeSelectionText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

function collectClozeTextNodes(root: Node) {
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

    const walker = document.createTreeWalker(
      node,
      NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT
    );

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

function canContainTextNodes(node: Node) {
  return node.nodeType === Node.ELEMENT_NODE || node.nodeType === Node.DOCUMENT_FRAGMENT_NODE;
}
