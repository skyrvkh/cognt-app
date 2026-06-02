import { importBlockSuiteJsonIntoExistingDoc } from '../documents/io';
import { reportDiagnostic } from '../host';
import type { MentionPreviewResult } from '../mentions';
import type { CogntLinkedDoc } from '../links/linked-notes';
import type { PendingLinkedDocPreview } from './linked-doc-preview-state';

type ApplyLinkedDocPreviewResultOptions = {
  pending: PendingLinkedDocPreview;
  result: MentionPreviewResult;
  registerLinkedDoc(linkedDoc: CogntLinkedDoc, generation: number): void;
  getLoadGeneration(): number;
  hasCurrentDoc(): boolean;
  isLoading(): boolean;
  flushHost(): Promise<void>;
};

export async function applyLinkedDocPreviewResult({
  pending,
  result,
  registerLinkedDoc,
  getLoadGeneration,
  hasCurrentDoc,
  isLoading,
  flushHost,
}: ApplyLinkedDocPreviewResultOptions) {
  if (!result.ok) {
    reportDiagnostic(
      'warn',
      `Could not preview linked doc ${pending.relativePath}: ${result.error ?? 'Preview unavailable.'}`
    );
    return;
  }

  try {
    await importBlockSuiteJsonIntoExistingDoc(
      pending.doc,
      result.documentContent ?? '',
      linkedDoc => registerLinkedDoc(linkedDoc, pending.generation)
    );
    if (
      pending.generation === getLoadGeneration()
      && hasCurrentDoc()
      && !isLoading()
    ) {
      await flushHost();
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    reportDiagnostic('warn', `Could not render linked doc preview ${pending.relativePath}: ${message}`);
  }
}
