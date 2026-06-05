import {
  createBlockSuiteDocumentJsonFromPlainText,
  createTitleFromPlainText,
} from '../documents/block-suite-document';
import type { ExtractReplacement } from '../editor/extract-replacement-types';
import { reportDiagnostic } from '../host';
import type {
  CreateExtractCommandHost,
  ExtractSourceAction,
  PendingExtract,
} from './create-extract-types';

export function createPendingExtract(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number,
  selectedPlainText: string,
  selectedContent: string,
  replacement: ExtractReplacement,
  sourceAction: ExtractSourceAction
): PendingExtract {
  host.createExtract({
    type: 'createExtract',
    sourcePath,
    selectedContent,
    selectedPlainText,
  });
  reportDiagnostic(
    'info',
    `Extract payload: mode=${replacement.kind}, text=${selectedPlainText.length}, json=${selectedContent.length}.`
  );
  host.showStatus('Creating extract...');

  return {
    generation,
    sourcePath,
    replacement,
    sourceAction,
  };
}

export function isStillCurrentExtractSource(
  host: CreateExtractCommandHost,
  sourcePath: string,
  generation: number
) {
  return host.getCurrentPath() === sourcePath
    && host.getLoadGeneration() === generation;
}

export function yieldExtractPreparation() {
  return new Promise<void>(resolve => {
    const resolveAfterTimer = () => window.setTimeout(resolve, 0);
    if (typeof window.requestAnimationFrame === 'function') {
      window.requestAnimationFrame(() => resolveAfterTimer());
      return;
    }

    resolveAfterTimer();
  });
}

export function createPlainTextExtractContent(selectedPlainText: string) {
  return createBlockSuiteDocumentJsonFromPlainText(
    selectedPlainText,
    createTitleFromPlainText(selectedPlainText, 'Extract')
  );
}
