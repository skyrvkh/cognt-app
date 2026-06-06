import { AttachmentPlacementController } from './attachment-placement';
import { AttachmentWorkflow } from './attachment-workflow';
import type { EditorFoundation } from './editor-foundation';
import { FileDropController } from './file-drop-controller';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
} from './editor-bootstrap-types';

export type AttachmentSubsystem = {
  attachmentPlacement: AttachmentPlacementController;
  attachmentWorkflow: AttachmentWorkflow;
  fileDrops: FileDropController;
};

type CreateAttachmentSubsystemOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
};

export function createAttachmentSubsystem({
  config,
  environment,
  foundation,
}: CreateAttachmentSubsystemOptions): AttachmentSubsystem {
  const { options } = config;
  const {
    editor,
    selection,
    runtime,
    mentions,
  } = foundation;

  const attachmentPlacement = new AttachmentPlacementController({
    editor,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentInsertionBlockModel: () => selection.getCurrentInsertionBlockModel(),
    getCurrentTextSelectionBlockModel: () => selection.getCurrentTextSelectionBlockModel(),
    flushHost: () => runtime.flushHost(),
    refreshMentionHighlights: () => mentions.refreshHighlights(),
    cancelPendingContentChanged: environment.cancelPendingContentChanged,
    recordUserEdit: environment.recordUserEdit,
    notifyContentChanged: async () => options.onContentChanged(await runtime.exportCurrentContent()),
  });

  const attachmentWorkflow = new AttachmentWorkflow({
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    attachFile: message => options.onAttachFile(message),
    cancelPendingContentChanged: environment.cancelPendingContentChanged,
    persistAttachmentMutation: () => attachmentPlacement.persistAttachmentMutation(),
    scheduleContentChanged: () => environment.scheduleContentChanged(),
    showStatus: message => runtime.showStatus(message),
  });

  const fileDrops = new FileDropController({
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    isInputLocked: environment.isInputLocked,
    showLockedInputStatus: environment.showLockedInputStatus,
    showStatus: message => runtime.showStatus(message),
    attachmentPlacement,
    attachmentWorkflow,
  });

  return {
    attachmentPlacement,
    attachmentWorkflow,
    fileDrops,
  };
}
