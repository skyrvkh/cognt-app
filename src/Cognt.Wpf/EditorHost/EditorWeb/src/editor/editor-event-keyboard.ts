import {
  isBrowserZoomShortcut,
} from './image-attachments';
import {
  isEditorShortcut,
  preventReadOnlyKeyboardInput,
} from './keyboard';
import type { EditorEventBindingOptions } from './editor-event-types';

export function handleEditorKeyDown(options: EditorEventBindingOptions, event: KeyboardEvent) {
  if (isBrowserZoomShortcut(event)) {
    event.preventDefault();
    event.stopPropagation();
    return;
  }

  if (options.isInputLocked()) {
    preventReadOnlyKeyboardInput(event);
    options.showLockedInputStatus();
    return;
  }

  if (!options.getCurrentDoc() || !options.getCurrentPath()) {
    return;
  }

  if (isEditorShortcut(event, 'x', { shift: true })) {
    void options.handleCreateExtractShortcut(event, 'replace');
    return;
  }

  if (isEditorShortcut(event, 'x')) {
    void options.handleCreateExtractShortcut(event, 'copy');
    return;
  }

  if (isEditorShortcut(event, 'c', { shift: true })) {
    void options.handleClozeCommand(event);
    return;
  }

  if (isEditorShortcut(event, 'z', { primary: false, alt: true })) {
    void options.handleClozeCommand(event);
    return;
  }

  if (isEditorShortcut(event, 'i', { shift: true })) {
    void options.handleImageOcclusionShortcut(event);
  }
}
