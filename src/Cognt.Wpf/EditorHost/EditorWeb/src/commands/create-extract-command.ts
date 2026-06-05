import { reportDiagnostic } from '../host';
import { runCreateBlockExtractCommand } from './create-extract-blocks';
import { runCreateTextExtractCommand } from './create-extract-text';
export type {
  CreateExtractCommandHost,
  CreateExtractCommandMessage,
  ExtractBlocksReplacement,
  ExtractReplacement,
  ExtractSourceAction,
  ExtractTextReplacement,
  ExtractTextReplacementPoint,
  ExtractTextReplacementRange,
  ExtractTextRangeReplacement,
  PendingExtract,
} from './create-extract-types';
import type {
  CreateExtractCommandHost,
  ExtractSourceAction,
  PendingExtract,
} from './create-extract-types';

export async function runCreateExtractCommand(
  host: CreateExtractCommandHost,
  event: KeyboardEvent,
  sourceAction: ExtractSourceAction = 'replace'
): Promise<PendingExtract | null> {
  reportDiagnostic('info', `Extract command requested (sourceAction=${sourceAction}).`);
  event.preventDefault();
  event.stopPropagation();

  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before extracting text.');
    reportDiagnostic('info', 'Extract command skipped: editor input is locked.');
    return null;
  }

  const currentDoc = host.getCurrentDoc();
  if (!currentDoc) {
    host.showStatus('Open a note before extracting content.');
    reportDiagnostic('info', 'Extract command skipped: no current document.');
    return null;
  }

  const sourcePath = host.getCurrentPath();
  const generation = host.getLoadGeneration();
  const editor = host.getEditor();
  const textSelection = host.getTextSelection();
  if (textSelection && !textSelection.isCollapsed()) {
    reportDiagnostic('info', `Extract command using text selection path (path=${sourcePath}, generation=${generation}).`);
    return runCreateTextExtractCommand(
      host,
      currentDoc,
      editor,
      sourcePath,
      generation,
      textSelection,
      sourceAction);
  }

  const selectedBlockIds = host.getBlockSelectionBlockIds();
  reportDiagnostic(
    'info',
    `Extract command using block selection path (path=${sourcePath}, generation=${generation}, blocks=${selectedBlockIds.length}).`
  );
  return runCreateBlockExtractCommand(
    host,
    currentDoc,
    sourcePath,
    generation,
    selectedBlockIds,
    sourceAction);
}
