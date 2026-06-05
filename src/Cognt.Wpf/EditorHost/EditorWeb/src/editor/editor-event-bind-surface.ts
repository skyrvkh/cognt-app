import type { EditorEventBindingOptions } from './editor-event-types';

export function bindEditorSurfaceEvents(options: EditorEventBindingOptions) {
  options.editor.addEventListener(
    'beforeinput',
    event => {
      if (event.inputType === 'deleteByCut' && options.consumeSuppressedNativeCut()) {
        event.preventDefault();
        return;
      }

      if (options.isInputLocked()) {
        event.preventDefault();
        options.showLockedInputStatus();
      }
    },
    { capture: true, signal: options.signal }
  );

  options.editor.addEventListener('input', () => {
    if (options.isInputLocked()) {
      return;
    }

    options.recordUserEdit();
    options.scheduleMentionHighlightUpdate();
    if (options.isLargeDocument()) {
      options.scheduleDocumentChangedNotification();
    } else {
      options.scheduleContentChanged();
    }
  }, { signal: options.signal });

  options.editor.addEventListener('keyup', () => {
    if (options.isInputLocked()) {
      return;
    }

    options.captureNativeTextSelectionForPaste();
    options.scheduleMentionHighlightUpdate();
    if (!options.isLargeDocument()) {
      options.scheduleContentChanged();
    }
  }, { signal: options.signal });

  options.editor.addEventListener('pointerup', () => {
    options.captureNativeTextSelectionForPaste();
  }, { signal: options.signal });

  options.editor.addEventListener('paste', event => {
    if (options.isInputLocked()) {
      event.preventDefault();
      options.showLockedInputStatus();
      return;
    }

    options.recordUserEdit();
    if (options.isLargeDocument()) {
      options.scheduleDocumentChangedNotification();
    } else {
      options.scheduleContentChanged();
    }
  }, { signal: options.signal });

  options.editor.addEventListener('cut', event => {
    if (options.consumeSuppressedNativeCut()) {
      event.preventDefault();
      return;
    }

    if (options.isInputLocked()) {
      event.preventDefault();
      options.showLockedInputStatus();
    }
  }, { signal: options.signal });

  options.editor.addEventListener('drop', event => {
    if (options.isInputLocked() || options.hasDroppedFiles(event)) {
      options.cancelNativeFileEvent(event);
      if (options.isInputLocked()) {
        options.showLockedInputStatus();
      }
    }
  }, { signal: options.signal });
}
