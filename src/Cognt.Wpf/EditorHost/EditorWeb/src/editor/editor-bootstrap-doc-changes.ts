import { DocumentChangeCoordinator } from './document-change-coordinator';
import type { CreateDocumentChangeCoordinatorOptions } from './editor-bootstrap-document-types';

export function createDocumentChangeCoordinator({
  config,
  environment,
  selection,
  runtime,
  linkedDocs,
  documentLinks,
  contentSave,
}: CreateDocumentChangeCoordinatorOptions) {
  return new DocumentChangeCoordinator({
    isLoading: () => config.isLoading(),
    getCurrentDoc: environment.getCurrentDoc,
    isLargeDocument: environment.isLargeDocument,
    ensureReferencesBlock: doc => documentLinks.ensureReferencesBlock(doc),
    clearOutlineCache: () => selection.clearOutlineCache(),
    scheduleContentChanged: environment.scheduleContentChanged,
    scheduleDocumentChangedNotification: () => contentSave.scheduleDocumentChangedNotification(),
    scheduleLinkedDocPreviewRefresh: () => linkedDocs.schedulePreviewRefresh(),
    showStatus: message => runtime.showStatus(message),
  });
}
