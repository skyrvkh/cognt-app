import type { EditorFoundation } from './editor-foundation';
import {
  createAttachmentSubsystem,
} from './editor-bootstrap-attachments';
import {
  createContentSaveCoordinator,
} from './editor-bootstrap-content-save';
import {
  createDocumentChangeCoordinator,
} from './editor-bootstrap-doc-changes';
import {
  createExtractWorkflow,
} from './editor-bootstrap-extract';
import {
  createDocumentLoadWorkflow,
} from './editor-bootstrap-load';
import {
  createEditorCommandController,
  createEditorEventBindingController,
} from './editor-bootstrap-interactions';
import {
  createDocumentLinksController,
  createLinkedDocWorkflow,
} from './editor-bootstrap-links';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
  EditorWorkflowBundle,
  EditorWorkflowRefs,
} from './editor-bootstrap-types';

type CreateEditorWorkflowBundleOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  refs: EditorWorkflowRefs;
};

export function createEditorWorkflowBundle({
  config,
  environment,
  foundation,
  refs,
}: CreateEditorWorkflowBundleOptions): EditorWorkflowBundle {
  const { options } = config;
  const {
    selection,
    runtime,
  } = foundation;
  let contentSave: ReturnType<typeof createContentSaveCoordinator> | null = null;

  const linkedDocs = createLinkedDocWorkflow({
    config,
    environment,
    foundation,
    scheduleDocumentChangedNotification: () => {
      contentSave?.scheduleDocumentChangedNotification();
    },
  });
  refs.linkedDocs = linkedDocs;

  const documentLinks = createDocumentLinksController({
    options,
    environment,
    refs,
  });
  refs.documentLinks = documentLinks;

  const attachments = createAttachmentSubsystem({
    config,
    environment,
    foundation,
  });

  contentSave = createContentSaveCoordinator({
    config,
    environment,
    foundation,
    attachments,
  });

  const docChanges = createDocumentChangeCoordinator({
    config,
    environment,
    selection,
    runtime,
    linkedDocs,
    documentLinks,
    contentSave,
  });

  const extractWorkflow = createExtractWorkflow({
    config,
    environment,
    foundation,
    linkedDocs,
    contentSave,
  });

  const documentLoad = createDocumentLoadWorkflow({
    config,
    environment,
    foundation,
    selection,
    linkedDocs,
    documentLinks,
    contentSave,
    docChanges,
  });

  const commands = createEditorCommandController({
    config,
    environment,
    foundation,
    linkedDocs,
    attachments,
  });

  const events = createEditorEventBindingController({
    config,
    environment,
    foundation,
    attachments,
    linkedDocs,
    extractWorkflow,
    contentSave,
    commands,
  });

  return {
    documentLinks,
    contentSave,
    docChanges,
    extractWorkflow,
    linkedDocs,
    documentLoad,
    commands,
    events,
  };
}
