import type { Doc } from '@blocksuite/store';
import { normalizeSelectionText } from '../documents/block-suite-document';
import { createBlockSelectionExtract } from '../documents/selection-export';
import { reportDiagnostic } from '../host';
import {
  createPendingExtract,
  createPlainTextExtractContent,
  isStillCurrentExtractSource,
  yieldExtractPreparation,
} from './create-extract-shared';
import { getReplaceableBlockSelection } from './create-extract-selection';
import type {
  CreateExtractCommandHost,
  ExtractSourceAction,
  PendingExtract,
} from './create-extract-types';

export async function runCreateBlockExtractCommand(
  host: CreateExtractCommandHost,
  currentDoc: Doc,
  sourcePath: string,
  generation: number,
  selectedBlockIds: string[],
  sourceAction: ExtractSourceAction
): Promise<PendingExtract | null> {
  const selection = getReplaceableBlockSelection(currentDoc, selectedBlockIds);
  if (selection.error) {
    host.showStatus(selection.error);
    reportDiagnostic('info', `Extract cancelled: ${selection.error}`);
    return null;
  }

  if (selection.blockIds.length === 0) {
    host.showStatus('Ctrl+X needs selected text or a selected block.');
    reportDiagnostic('info', 'Extract cancelled: no selected blocks.');
    return null;
  }

  host.showStatus('Preparing extract...');
  await yieldExtractPreparation();
  if (!isStillCurrentExtractSource(host, sourcePath, generation)) {
    host.showStatus('Extract cancelled because the note changed.');
    reportDiagnostic(
      'info',
      `Extract cancelled: source changed during block preparation (path=${sourcePath}, generation=${generation}).`
    );
    return null;
  }

  const extract = createBlockSelectionExtract(currentDoc, selection.blockIds);
  const selectedPlainText = extract.selectedPlainText;
  if (normalizeSelectionText(selectedPlainText).length === 0) {
    host.showStatus('The selected block does not contain extractable text.');
    reportDiagnostic(
      'warn',
      `Extract cancelled: block selection contained no text (blocks=${selection.blockIds.length}).`
    );
    return null;
  }

  const selectedContent = extract.selectedContent ?? createPlainTextExtractContent(selectedPlainText);

  return createPendingExtract(
    host,
    sourcePath,
    generation,
    selectedPlainText,
    selectedContent,
    {
      kind: 'blocks',
      blockIds: selection.blockIds,
    },
    sourceAction
  );
}
