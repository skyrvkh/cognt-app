import { ExtractWorkflow } from './extract-workflow';
import type { CreateExtractWorkflowOptions } from './editor-bootstrap-document-types';

export function createExtractWorkflow({
  config,
  environment,
  foundation,
  linkedDocs,
  contentSave,
}: CreateExtractWorkflowOptions) {
  const { options } = config;
  const {
    editor,
    selection,
    runtime,
  } = foundation;

  return new ExtractWorkflow({
    editor,
    isInputLocked: environment.isInputLocked,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    getLoadGeneration: environment.getLoadGeneration,
    getTextSelection: () => selection.getTextSelection(),
    getBlockSelectionBlockIds: () => selection.getBlockSelectionBlockIds(),
    createExtract: message => options.onCreateExtract(message),
    resolveLinkedNoteTitle: relativePath => linkedDocs.resolveTitle(relativePath) ?? undefined,
    trackLinkedDoc: (doc, relativePath) => linkedDocs.trackDoc(doc, relativePath),
    requestLinkedDocPreview: (doc, relativePath, generation) =>
      linkedDocs.requestPreview(doc, relativePath, generation),
    cancelPendingContentChanged: environment.cancelPendingContentChanged,
    recordUserEdit: environment.recordUserEdit,
    schedulePostExtractSourceSave: () => contentSave.schedulePostExtractSourceSave(),
    showStatus: message => runtime.showStatus(message),
  });
}
