import {
  getInteractivityDiagnostic,
  waitForNextAnimationFrame,
} from './dom';
import type {
  EditorInteractionModeState,
  EditorInteractionOptions,
} from './editor-interaction-types';
import {
  flushEditorHost,
  focusFirstTextBlock,
  forceEditableRichText,
  forceReadonlyRichText,
  setDocReadonly,
} from './host-operations';
import { reportDiagnostic } from '../host';

export async function activateEditorMode(
  options: EditorInteractionOptions,
  state: EditorInteractionModeState
) {
  enableEditorMode(options, state);
  const editor = options.editor;
  const doc = options.getCurrentDoc();
  await flushEditorHost(editor);
  await waitForNextAnimationFrame();
  await forceEditableRichText(editor);
  focusFirstTextBlock(editor, doc);
  reportDiagnostic(
    'info',
    `Editor mode active (${getInteractivityDiagnostic(doc, document)}).`
  );
}

export async function setReadOnlyMode(
  options: EditorInteractionOptions,
  state: EditorInteractionModeState,
  readOnly: boolean
) {
  if (!readOnly) {
    await activateEditorMode(options, state);
    return;
  }

  const doc = options.getCurrentDoc();
  state.setReadOnlyMode(true);
  document.body.dataset.mode = 'readonly';
  setDocReadonly(doc, true);
  await flushEditorHost(options.editor);
  await waitForNextAnimationFrame();
  await forceReadonlyRichText(options.editor);
  options.showStatus('Read-only.');
  reportDiagnostic(
    'info',
    `Read-only mode active (${getInteractivityDiagnostic(doc, document)}).`
  );
}

export function enableEditorMode(
  options: Pick<EditorInteractionOptions, 'getCurrentDoc'>,
  state: Pick<EditorInteractionModeState, 'setReadOnlyMode'>
) {
  state.setReadOnlyMode(false);
  document.body.dataset.mode = 'editor';
  setDocReadonly(options.getCurrentDoc(), false);
}

export function showLockedInputStatus(
  options: Pick<EditorInteractionOptions, 'showStatus'>,
  state: EditorInteractionModeState
) {
  const now = performance.now();
  if (now - state.lockedInputStatusShownAt < 1200) {
    return;
  }

  state.setLockedInputStatusShownAt(now);
  options.showStatus('This view is read-only.');
}
