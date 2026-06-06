import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';
import {
  getInteractivityDiagnostic,
  waitForNextAnimationFrame,
} from './dom';
import {
  focusFirstTextBlock,
  forceEditableRichText,
  setDocReadonly,
} from './host-operations';
import {
  logEditorTiming,
} from './timing';
import type {
  DocumentLoadStateGuard,
  DocumentLoadWorkflowOptions,
} from './document-load-types';

type DocumentLoadAssignmentOptions = Pick<
  DocumentLoadWorkflowOptions,
  'trackCurrentDoc' | 'editor' | 'interactions' | 'docChanges' | 'documentLinks' | 'flushHost'
>;

type DocumentLoadUnlockOptions = Pick<
  DocumentLoadWorkflowOptions,
  'getCurrentDoc' | 'interactions' | 'editor'
>;

type DocumentLoadDeferredOptions = Pick<
  DocumentLoadWorkflowOptions,
  'getCurrentDoc' | 'editor' | 'mentions'
>;

export async function assignLoadedDocument(
  options: DocumentLoadAssignmentOptions,
  nextDoc: Doc,
  title?: string
) {
  const assignStartedAt = performance.now();
  options.trackCurrentDoc(nextDoc);
  options.editor.doc = nextDoc;
  options.interactions.enableEditorMode();
  options.editor.hasViewport = true;
  options.docChanges.bind(nextDoc);
  options.documentLinks.setLinks({
    incoming: [],
    outgoing: [],
  });
  nextDoc.resetHistory();
  logEditorTiming('editor.doc assignment', assignStartedAt, title);

  const flushStartedAt = performance.now();
  await options.flushHost();
  logEditorTiming('host flush/render', flushStartedAt, title);
}

export async function unlockEditorAfterRender(
  options: DocumentLoadUnlockOptions,
  guard: DocumentLoadStateGuard,
  operation: string
) {
  const currentDoc = options.getCurrentDoc();
  if (guard.isStaleGeneration(guard.generation) || !currentDoc) {
    return false;
  }

  const startedAt = performance.now();
  options.interactions.enableEditorMode();
  setDocReadonly(currentDoc, false);

  await waitForNextAnimationFrame();

  const refreshedDoc = options.getCurrentDoc();
  if (guard.isStaleGeneration(guard.generation) || !refreshedDoc) {
    return false;
  }

  await forceEditableRichText(options.editor);
  logEditorTiming(`${operation} editable unlock`, startedAt);
  reportDiagnostic(
    'info',
    `Editor unlocked after ${operation} (${getInteractivityDiagnostic(refreshedDoc, document)}).`
  );
  return true;
}

export function deferPostLoadWork(
  options: DocumentLoadDeferredOptions,
  guard: DocumentLoadStateGuard
) {
  const run = () => {
    const currentDoc = options.getCurrentDoc();
    if (guard.isStaleGeneration(guard.generation) || !currentDoc) {
      return;
    }

    const focusStartedAt = performance.now();
    focusFirstTextBlock(options.editor, currentDoc);
    logEditorTiming('focus first text block', focusStartedAt);

    const mentionStartedAt = performance.now();
    options.mentions.refreshHighlights();
    logEditorTiming('mention highlight refresh', mentionStartedAt);
  };

  const idleCallback = window.requestIdleCallback;
  if (idleCallback) {
    idleCallback(run, { timeout: 1200 });
    return;
  }

  window.setTimeout(run, 80);
}
