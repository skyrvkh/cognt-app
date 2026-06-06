import type { DocumentLinksController } from '../document-links';
import { DocumentLinksController as DocumentLinksControllerImpl } from '../document-links';
import type { EditorFoundation } from './editor-foundation';
import { LinkedDocWorkflow } from './linked-doc-workflow';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
  EditorWorkflowRefs,
} from './editor-bootstrap-types';

type CreateLinkedDocWorkflowOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  scheduleDocumentChangedNotification(): void;
};

type CreateDocumentLinksControllerOptions = {
  options: EditorBootstrapConfig['options'];
  environment: EditorBootstrapEnvironment;
  refs: EditorWorkflowRefs;
};

export function createLinkedDocWorkflow({
  config,
  environment,
  foundation,
  scheduleDocumentChangedNotification,
}: CreateLinkedDocWorkflowOptions) {
  const { options } = config;
  const {
    editor,
    runtime,
  } = foundation;

  return new LinkedDocWorkflow({
    editor,
    signal: config.signal,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    getLoadGeneration: environment.getLoadGeneration,
    isLoading: () => config.isLoading(),
    isLargeDocument: environment.isLargeDocument,
    createLinkedNote: message => options.onCreateLinkedNote(message),
    requestMentionPreview: message => options.onRequestMentionPreview(message),
    openMention: message => options.onOpenMention(message),
    exportContent: () => runtime.exportCurrentContent(),
    flushHost: () => runtime.flushHost(),
    cancelPendingContentChanged: environment.cancelPendingContentChanged,
    recordUserEdit: environment.recordUserEdit,
    notifyContentChanged: content => options.onContentChanged(content),
    scheduleDocumentChangedNotification,
    scheduleContentChanged: () => environment.scheduleContentChanged(),
    showStatus: message => runtime.showStatus(message),
  });
}

export function createDocumentLinksController({
  options,
  environment,
  refs,
}: CreateDocumentLinksControllerOptions): DocumentLinksController {
  return new DocumentLinksControllerImpl({
    container: options.documentLinksElement,
    getDoc: environment.getCurrentDoc,
    openDocument: relativePath => refs.linkedDocs?.openDocumentLink(relativePath) ?? Promise.resolve(),
  });
}
