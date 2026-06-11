import { EditorCommandController } from './editor-command-controller';
import { EditorEventBindingController } from './editor-event-binding-controller';
import type { EditorFoundation } from './editor-foundation';
import type { EditorSelectionController } from './editor-selection-controller';
import type { AttachmentSubsystem } from './editor-bootstrap-attachments';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
} from './editor-bootstrap-types';
import type { ExtractWorkflow } from './extract-workflow';
import type { LinkedDocWorkflow } from './linked-doc-workflow';
import type { ContentSaveCoordinator } from './content-save-coordinator';
import { RichHtmlPasteController } from './rich-html-paste';
import { insertRichHtmlPasteImages } from './rich-html-paste-image-insertion';
import {
  readHostImageSourceAsDataUrl,
  type RichHtmlPasteHostSourceOptions,
} from './rich-html-paste-host-source';

type CreateEditorCommandControllerOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  linkedDocs: LinkedDocWorkflow;
  attachments: AttachmentSubsystem;
};

type CreateEditorEventBindingControllerOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  attachments: AttachmentSubsystem;
  linkedDocs: LinkedDocWorkflow;
  extractWorkflow: ExtractWorkflow;
  contentSave: ContentSaveCoordinator;
  commands: EditorCommandController;
};

export function createEditorCommandController({
  config,
  environment,
  foundation,
  linkedDocs,
  attachments,
}: CreateEditorCommandControllerOptions) {
  const { options } = config;
  const {
    editor,
    selection,
    runtime,
    mentions,
  } = foundation;

  return new EditorCommandController({
    editor,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    getLoadGeneration: environment.getLoadGeneration,
    isInputLocked: environment.isInputLocked,
    showLockedInputStatus: environment.showLockedInputStatus,
    getSelectedImageBlock: () => selection.getSelectedImageBlock(),
    getTextSelection: () => selection.getTextSelection(),
    flushHost: () => runtime.flushHost(),
    cancelPendingContentChanged: environment.cancelPendingContentChanged,
    exportContent: () => runtime.exportCurrentContent(),
    saveCloze: message => options.onSaveCloze(message),
    openImageOcclusion: message => options.onOpenImageOcclusion(message),
    createLinkedNote: message => options.onCreateLinkedNote(message),
    trackLinkedDoc: (doc, relativePath) => linkedDocs.trackDoc(doc, relativePath),
    registerNestedLinkedDoc: (linkedDocDocument, generation) =>
      linkedDocs.register(linkedDocDocument, generation),
    markLinkedDocPreviewed: doc => linkedDocs.markPreviewed(doc),
    requestLinkedDocPreview: (doc, relativePath, generation) =>
      linkedDocs.requestPreview(doc, relativePath, generation),
    refreshMentionHighlights: () => mentions.refreshHighlights(),
    recordUserEdit: environment.recordUserEdit,
    notifyContentChanged: async () => options.onContentChanged(await runtime.exportCurrentContent()),
    attachmentWorkflow: attachments.attachmentWorkflow,
    attachmentPlacement: attachments.attachmentPlacement,
    showStatus: message => runtime.showStatus(message),
  });
}

export function createEditorEventBindingController({
  config,
  environment,
  foundation,
  attachments,
  linkedDocs,
  extractWorkflow,
  contentSave,
  commands,
}: CreateEditorEventBindingControllerOptions) {
  const {
    editor,
    interactions,
    mentions,
    runtime,
  } = foundation;
  const hostImageSource: RichHtmlPasteHostSourceOptions = {
    getCurrentPath: () => environment.getCurrentPath(),
    attachImageSource: message => config.options.onAttachImageSource(message),
    readAttachment: message => config.options.onReadAttachment(message),
  };
  const richHtmlPaste = new RichHtmlPasteController({
    editor,
    recordUserEdit: environment.recordUserEdit,
    scheduleContentChanged: (delayMs, refreshMentions) =>
      environment.scheduleContentChanged(delayMs, refreshMentions),
    scheduleDocumentChangedNotification: () => contentSave.scheduleDocumentChangedNotification(),
    showStatus: message => runtime.showStatus(message),
    readNativeClipboardImage: () => config.options.onReadNativeClipboardImage(),
    readImageSourceAsDataUrl: (imageSource, fallbackFileName) =>
      readHostImageSourceAsDataUrl(hostImageSource, imageSource, fallbackFileName),
    insertImages: images =>
      insertRichHtmlPasteImages({
        editor,
        getCurrentDoc: environment.getCurrentDoc,
        persistAttachmentMutation: () => attachments.attachmentPlacement.persistAttachmentMutation(),
      }, images),
  });

  return new EditorEventBindingController({
    editor,
    signal: config.signal,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    isInputLocked: environment.isInputLocked,
    showLockedInputStatus: environment.showLockedInputStatus,
    hasDroppedFiles: event => attachments.fileDrops.hasDroppedFiles(event),
    handleFileDragOver: event => attachments.fileDrops.handleFileDragOver(event),
    handleDroppedFiles: event => attachments.fileDrops.handleDroppedFiles(event),
    cancelNativeFileEvent: event => attachments.fileDrops.cancelNativeFileEvent(event),
    isReplayingRichHtmlPaste: () => richHtmlPaste.isReplaying,
    handleRichHtmlPaste: event => richHtmlPaste.handlePaste(event),
    scheduleRichHtmlPasteSave: () => richHtmlPaste.scheduleSaveAfterPaste(),
    captureNativeTextSelectionForPaste: () => interactions.captureNativeTextSelectionForPaste(),
    scheduleMentionHighlightUpdate: () => mentions.scheduleHighlightUpdate(),
    recordUserEdit: environment.recordUserEdit,
    isLargeDocument: environment.isLargeDocument,
    scheduleDocumentChangedNotification: () => contentSave.scheduleDocumentChangedNotification(),
    scheduleContentChanged: () => environment.scheduleContentChanged(),
    consumeSuppressedNativeCut: () => extractWorkflow.consumeSuppressedNativeCut(),
    openLinkedDocReference: reference => linkedDocs.openReference(reference),
    getSelectedLinkedDocModelForOpenIntent: event =>
      linkedDocs.getSelectedModelForOpenIntent(event),
    openLinkedDocFromModel: model => linkedDocs.openFromModel(model),
    handleCreateExtractShortcut: (event, sourceAction) =>
      extractWorkflow.handleShortcut(event, sourceAction),
    handleClozeCommand: event => commands.handleClozeCommand(event),
    handleImageOcclusionShortcut: event => commands.handleImageOcclusionShortcut(event),
  });
}
