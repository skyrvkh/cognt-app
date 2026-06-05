import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import { runCreateLinkedNoteCommand } from '../commands';
import type { LinkedNoteViewMode } from './contracts';
import type { EditorCommandControllerOptions } from './editor-command-types';

export async function createLinkedNoteFromSlashMenu(
  options: EditorCommandControllerOptions,
  viewMode: LinkedNoteViewMode,
  context: AffineSlashMenuContext
) {
  await runCreateLinkedNoteCommand(
    {
      isInputLocked: () => options.isInputLocked(),
      getCurrentDoc: () => options.getCurrentDoc(),
      getCurrentPath: () => options.getCurrentPath(),
      getLoadGeneration: () => options.getLoadGeneration(),
      createLinkedNote: message => options.createLinkedNote(message),
      trackLinkedDoc: (doc, relativePath) => options.trackLinkedDoc(doc, relativePath),
      registerNestedLinkedDoc: (linkedDoc, generation) =>
        options.registerNestedLinkedDoc(linkedDoc, generation),
      markLinkedDocPreviewed: doc => options.markLinkedDocPreviewed(doc),
      requestLinkedDocPreview: (doc, relativePath, generation) =>
        options.requestLinkedDocPreview(doc, relativePath, generation),
      flushHost: () => options.flushHost(),
      refreshMentionHighlights: () => options.refreshMentionHighlights(),
      cancelPendingContentChanged: () => options.cancelPendingContentChanged(),
      recordUserEdit: () => options.recordUserEdit(),
      notifyContentChanged: () => options.notifyContentChanged(),
      showStatus: message => options.showStatus(message),
    },
    viewMode,
    context
  );
}
