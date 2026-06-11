import {
  isBrowserZoomWheel,
} from './image-attachments';
import { handleEditorKeyDown } from './editor-event-keyboard';
import { handleLinkedDocOpenIntent } from './editor-event-linked-doc';
import { handleEditorPasteEvent } from './editor-event-paste';
import type { EditorEventBindingOptions } from './editor-event-types';

export function bindGlobalEditorEvents(options: EditorEventBindingOptions) {
  document.addEventListener(
    'keydown',
    event => handleEditorKeyDown(options, event),
    { capture: true, signal: options.signal }
  );

  document.addEventListener(
    'wheel',
    event => {
      if (!isBrowserZoomWheel(event)) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
    },
    { capture: true, passive: false, signal: options.signal }
  );

  document.addEventListener(
    'click',
    event => handleLinkedDocOpenIntent(options, event),
    { capture: true, signal: options.signal }
  );

  document.addEventListener(
    'paste',
    event => {
      if (!options.isEditorEvent(event) && !options.getCurrentDoc()) {
        return;
      }

      handleEditorPasteEvent(options, event, { recordPlainPaste: false });
    },
    { capture: true, signal: options.signal }
  );

  document.addEventListener(
    'dragover',
    event => options.handleFileDragOver(event),
    { capture: true, signal: options.signal }
  );

  document.addEventListener(
    'drop',
    event => {
      if (!options.isEditorEvent(event) || !options.hasDroppedFiles(event)) {
        return;
      }

      options.cancelNativeFileEvent(event);
      void options.handleDroppedFiles(event);
    },
    { capture: true, signal: options.signal }
  );

  document.addEventListener(
    'dblclick',
    event => handleLinkedDocOpenIntent(options, event),
    { capture: true, signal: options.signal }
  );

  document.addEventListener('selectionchange', () => {
    if (options.getCurrentDoc()) {
      options.captureNativeTextSelectionForPaste();
      options.scheduleMentionHighlightUpdate();
    }
  }, { signal: options.signal });
}
