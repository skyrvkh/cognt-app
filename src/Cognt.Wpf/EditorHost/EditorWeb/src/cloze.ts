import type { Doc } from '@blocksuite/store';
import { getLatexBlockSelectionCandidate } from './cloze/cloze-block-latex';
import { getNativeCodeBlockSelectionCandidate } from './cloze/cloze-code-block';
import { getInlineLatexSelectionCandidate } from './cloze/cloze-inline-latex';
import { replaceSelectionWithCloze, toClozeText } from './cloze/cloze-replacement';
import { collectClozeTextNodes } from './cloze/cloze-text';

export { replaceSelectionWithCloze, toClozeText };
export type { ClozeContext, NativeClozeSelection, TextSelection } from './cloze/cloze-types';

export function getNativeCodeBlockSelectionBlockId(root: Node) {
  return getNativeCodeBlockSelectionCandidate(root)?.model.id?.trim() || null;
}

export function getNativeClozeSelection(root: Node, doc: Doc | null) {
  const codeCandidate = getNativeCodeBlockSelectionCandidate(root);
  const codeBlockId = codeCandidate?.model.id?.trim();
  if (codeCandidate && codeBlockId) {
    return {
      blockId: codeBlockId,
      plainText: window.getSelection()?.toString().trim() || '',
    };
  }

  const inlineLatexCandidate = getInlineLatexSelectionCandidate(root);
  if (inlineLatexCandidate) {
    return {
      blockId: inlineLatexCandidate.blockId,
      plainText: inlineLatexCandidate.latex,
    };
  }

  const blockLatexCandidate = getLatexBlockSelectionCandidate(root, doc);
  if (blockLatexCandidate) {
    return {
      blockId: blockLatexCandidate.blockId,
      plainText: blockLatexCandidate.latex,
    };
  }

  return null;
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
