import type { EditorEventBindingOptions } from './editor-event-types';

type EditorPasteHandlingOptions = {
  recordPlainPaste: boolean;
};

const replaySaveScheduledEvents = new WeakSet<ClipboardEvent>();

export function handleEditorPasteEvent(
  options: EditorEventBindingOptions,
  event: ClipboardEvent,
  handling: EditorPasteHandlingOptions
) {
  if (options.isInputLocked()) {
    event.preventDefault();
    options.showLockedInputStatus();
    return true;
  }

  if (options.isReplayingRichHtmlPaste()) {
    scheduleReplaySaveOnce(options, event);
    return true;
  }

  if (options.handleRichHtmlPaste(event)) {
    return true;
  }

  if (!handling.recordPlainPaste) {
    return false;
  }

  options.recordUserEdit();
  if (options.isLargeDocument()) {
    options.scheduleDocumentChangedNotification();
  } else {
    options.scheduleContentChanged();
  }

  return false;
}

function scheduleReplaySaveOnce(
  options: EditorEventBindingOptions,
  event: ClipboardEvent
) {
  if (replaySaveScheduledEvents.has(event)) {
    return;
  }

  replaySaveScheduledEvents.add(event);
  options.scheduleRichHtmlPasteSave();
}
