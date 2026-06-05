import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import { normalizeSelectionText } from '../documents/block-suite-document';
import {
  createTextSelectionExtract,
  getCurrentSelection,
  getOrderedTextSelectionPoints,
} from '../documents/selection-export';
import { reportDiagnostic } from '../host';
import { createPendingExtract, createPlainTextExtractContent, isStillCurrentExtractSource, yieldExtractPreparation } from './create-extract-shared';
import { createTextSelectionReplacement, getTextSelectionReplacement } from './create-extract-selection';
import type {
  CreateExtractCommandHost,
  ExtractSourceAction,
  PendingExtract,
} from './create-extract-types';

export async function runCreateTextExtractCommand(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  editor: PageEditor,
  sourcePath: string,
  generation: number,
  textSelection: TextSelection,
  sourceAction: ExtractSourceAction
): Promise<PendingExtract | null> {
  const fastExtract = tryCreateSameBlockTextExtract(
    host,
    sourcePath,
    generation,
    textSelection,
    sourceAction
  );
  if (fastExtract) {
    return fastExtract;
  }

  host.showStatus('Preparing extract...');
  await yieldExtractPreparation();
  if (!isStillCurrentExtractSource(host, sourcePath, generation)) {
    host.showStatus('Extract cancelled because the note changed.');
    reportDiagnostic(
      'info',
      `Extract cancelled: source changed during text preparation (path=${sourcePath}, generation=${generation}).`
    );
    return null;
  }

  const extract = createTextSelectionExtract(
    currentDoc,
    editor,
    textSelection
  );
  const selectedPlainText = extract.selectedPlainText;
  if (normalizeSelectionText(selectedPlainText).length === 0) {
    host.showStatus('The selected text is empty.');
    reportDiagnostic(
      'info',
      'Extract cancelled: selected text range resolved to empty content.'
    );
    return null;
  }

  const selectedContent = extract.selectedContent ?? createPlainTextExtractContent(selectedPlainText);
  const replacement = createTextSelectionReplacement(
    host,
    currentDoc,
    textSelection,
    extract.blockIds
  );
  if (!replacement) {
    reportDiagnostic('info', 'Extract cancelled: text selection could not be converted to a replacement range.');
    return null;
  }

  return createPendingExtract(
    host,
    sourcePath,
    generation,
    selectedPlainText,
    selectedContent,
    replacement,
    sourceAction
  );
}

function tryCreateSameBlockTextExtract(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number,
  textSelection: TextSelection,
  sourceAction: ExtractSourceAction
): PendingExtract | null {
  const points = getOrderedTextSelectionPoints(textSelection);
  if (points.start.blockId !== points.end.blockId) {
    return null;
  }

  const replacement = getTextSelectionReplacement(points);
  const selectedPlainText = getCurrentSelection()?.plainText ?? '';
  if (!replacement || normalizeSelectionText(selectedPlainText).length === 0) {
    reportDiagnostic(
      'info',
      `Extract same-block fast path skipped (hasReplacement=${Boolean(replacement)}, text=${selectedPlainText.length}).`
    );
    return null;
  }

  const selectedContent = createPlainTextExtractContent(selectedPlainText);
  reportDiagnostic(
    'info',
    `Extract using same-block text fast path (text=${selectedPlainText.length}, json=${selectedContent.length}).`
  );
  return createPendingExtract(
    host,
    sourcePath,
    generation,
    selectedPlainText,
    selectedContent,
    replacement,
    sourceAction
  );
}
