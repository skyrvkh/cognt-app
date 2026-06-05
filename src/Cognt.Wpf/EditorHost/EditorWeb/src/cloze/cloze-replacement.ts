import {
  getLatexBlockSelectionCandidate,
} from './cloze-block-latex';
import {
  getNativeCodeBlockSelectionCandidate,
} from './cloze-code-block';
import {
  getInlineLatexSelectionCandidate,
} from './cloze-inline-latex';
import {
  getModelById,
} from './cloze-models';
import {
  getTextSelectionRange,
  normalizeSelectionText,
} from './cloze-text';
import type {
  ClozeContext,
  InlineLatexSelectionCandidate,
  LatexBlockModel,
  LatexBlockSelectionCandidate,
  TextReplacementModel,
} from './cloze-types';

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

  if (replaceNativeCodeBlockSelectionWithCloze(context, clozeText)) {
    return clozeText;
  }

  if (replaceNativeLatexSelectionWithCloze(context, clozeText)) {
    return clozeText;
  }

  throw new Error('Cloze requires a single BlockSuite text, code, or equation selection.');
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
  } catch {
    return false;
  }
}

function replaceNativeLatexSelectionWithCloze(context: ClozeContext, clozeText: string) {
  if (!context.editorRoot) {
    return false;
  }

  const inlineCandidate = getInlineLatexSelectionCandidate(context.editorRoot);
  if (inlineCandidate) {
    return replaceInlineLatexWithCloze(inlineCandidate, clozeText);
  }

  const blockCandidate = getLatexBlockSelectionCandidate(context.editorRoot, context.doc);
  if (blockCandidate) {
    return replaceLatexBlockWithCloze(blockCandidate, clozeText);
  }

  return false;
}

function replaceInlineLatexWithCloze(candidate: InlineLatexSelectionCandidate, clozeText: string) {
  if (!candidate.node.editor) {
    return false;
  }

  try {
    candidate.node.editor.insertText(
      {
        index: candidate.startOffset,
        length: candidate.length,
      },
      clozeText
    );
    return true;
  } catch {
    return false;
  }
}

function replaceLatexBlockWithCloze(candidate: LatexBlockSelectionCandidate, clozeText: string) {
  const { doc, model } = candidate;
  if (!doc || !model.id) {
    return false;
  }

  const currentModel = getModelById<LatexBlockModel>(doc, model.id) ?? model;
  const parent = doc.getParent(currentModel as never) as
    | { id?: string; flavour?: string; children?: unknown[] }
    | null;
  if (!parent?.id || !Array.isArray(parent.children)) {
    return false;
  }

  const index = parent.children.indexOf(currentModel);
  if (index < 0) {
    return false;
  }

  try {
    doc.transact(() => {
      const paragraphId = doc.addBlock(
        'affine:paragraph' as never,
        { type: 'text' } as never,
        parent.id as never,
        index
      );
      const paragraph = getModelById<TextReplacementModel>(doc, paragraphId);
      if (!paragraph?.text) {
        throw new Error('could not create replacement paragraph for equation cloze');
      }

      paragraph.text.replace(0, 0, clozeText);
      doc.deleteBlock(currentModel as never, { deleteChildren: true });
    });
    return true;
  } catch {
    return false;
  }
}

function replaceNativeCodeBlockSelectionWithCloze(context: ClozeContext, clozeText: string) {
  if (!context.doc || !context.editorRoot) {
    return false;
  }

  const candidate = getNativeCodeBlockSelectionCandidate(context.editorRoot);
  if (!candidate) {
    return false;
  }

  const currentModel = context.doc.getBlockById(candidate.model.id ?? '') as TextReplacementModel | null;
  const model = currentModel?.text ? currentModel : candidate.model;
  if (!model.text || !candidate.range) {
    return false;
  }

  try {
    context.doc.transact(() => {
      model.text!.replace(candidate.range.index, candidate.range.length, clozeText);
    });
    return true;
  } catch {
    return false;
  }
}
