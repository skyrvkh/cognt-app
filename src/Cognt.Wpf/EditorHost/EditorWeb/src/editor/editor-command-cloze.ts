import { runClozeCommand } from '../commands';
import type {
  ClozeCommandRequest,
  EditorCommandControllerOptions,
} from './editor-command-types';

export async function createClozeFromSelection(
  options: EditorCommandControllerOptions,
  request: ClozeCommandRequest = {}
) {
  await handleClozeCommand(options, undefined, request);
}

export async function handleClozeCommand(
  options: EditorCommandControllerOptions,
  event?: KeyboardEvent,
  request: ClozeCommandRequest = {}
) {
  await runClozeCommand(
    {
      isInputLocked: () => options.isInputLocked(),
      getCurrentDoc: () => options.getCurrentDoc(),
      getCurrentPath: () => options.getCurrentPath(),
      getEditorRoot: () => options.editor,
      getTextSelection: () => options.getTextSelection(),
      clearTextSelection: () => {
        options.editor.host?.selection.clear(['text']);
        window.getSelection()?.removeAllRanges();
      },
      flushHost: () => options.flushHost(),
      cancelPendingContentChanged: () => options.cancelPendingContentChanged(),
      exportContent: () => options.exportContent(),
      saveCloze: message => options.saveCloze(message),
      showStatus: message => options.showStatus(message),
    },
    event,
    request
  );
}
