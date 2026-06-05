import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import { pickImageFiles } from './image-attachments';
import type { EditorCommandControllerOptions } from './editor-command-types';

export async function handleImageOcclusionShortcut(
  options: EditorCommandControllerOptions,
  event: KeyboardEvent
) {
  if (options.isInputLocked()) {
    options.showLockedInputStatus();
    event.preventDefault();
    return;
  }

  event.preventDefault();
  event.stopPropagation();

  const currentDoc = options.getCurrentDoc();
  const currentPath = options.getCurrentPath();
  if (!currentDoc || !currentPath) {
    options.showStatus('Open a note before adding image occlusion.');
    return;
  }

  const selectedImage = options.getSelectedImageBlock();
  if (!selectedImage) {
    options.showStatus('Select an image block before adding image occlusion.');
    return;
  }

  await options.flushHost();
  options.cancelPendingContentChanged();
  options.openImageOcclusion({
    type: 'openImageOcclusion',
    sourcePath: currentPath,
    imageBlockId: selectedImage.imageBlockId,
    imageRelPath: selectedImage.imageRelPath,
    content: await options.exportContent(),
  });
}

export async function insertImageFromSlashMenu(
  options: EditorCommandControllerOptions,
  context: AffineSlashMenuContext
) {
  if (options.isInputLocked()) {
    options.showLockedInputStatus();
    return;
  }

  if (!options.getCurrentDoc() || !options.getCurrentPath()) {
    options.showStatus('Open a note before adding images.');
    return;
  }

  const files = await pickImageFiles();
  if (files.length === 0) {
    return;
  }

  await options.attachmentWorkflow.attachFiles(
    files,
    file =>
      options.attachmentPlacement.insertImageAttachmentFromSlashContext(
        context,
        file.result,
        file.fallbackName,
        file.sourceFile,
        file.contentBase64
      ),
    file =>
      options.attachmentPlacement.insertImageAttachmentFromSlashContext(
        context,
        file.result,
        file.fallbackName,
        file.sourceFile,
        file.contentBase64,
        false
      )
  );
}
