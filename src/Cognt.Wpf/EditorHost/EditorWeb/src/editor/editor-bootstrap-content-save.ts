import { ContentSaveCoordinator } from './content-save-coordinator';
import type { CreateContentSaveCoordinatorOptions } from './editor-bootstrap-document-types';

export function createContentSaveCoordinator({
  config,
  environment,
  foundation,
  attachments,
}: CreateContentSaveCoordinatorOptions) {
  const { options } = config;
  const {
    mentions,
    runtime,
  } = foundation;

  return new ContentSaveCoordinator({
    hostElement: options.hostElement,
    isLoading: () => config.isLoading(),
    isInputLocked: environment.isInputLocked,
    hasCurrentDoc: () => Boolean(environment.getCurrentDoc()),
    hasCurrentPath: () => Boolean(environment.getCurrentPath()),
    hasPendingAttachmentPreview: () => attachments.attachmentWorkflow.hasPendingPreview,
    refreshMentionHighlights: () => mentions.refreshHighlights(),
    exportContent: () => runtime.exportCurrentContent(),
    onContentChanged: content => options.onContentChanged(content),
    onDocumentChanged: () => options.onDocumentChanged(),
  });
}
