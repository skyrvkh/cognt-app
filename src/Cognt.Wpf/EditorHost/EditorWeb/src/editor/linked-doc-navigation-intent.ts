import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import {
  getCogntLinkedDocToolbarModel,
  getCogntLinkedNotePathFromModel,
  isCogntLinkedDocModel,
  isOpenThisDocToolbarIntent,
} from '../links/linked-notes';

export function getSelectedLinkedDocModelForOpenIntent(
  editor: PageEditor,
  currentDoc: Doc | null,
  event: MouseEvent
) {
  if (!isOpenThisDocToolbarIntent(event)) {
    return null;
  }

  const toolbarModel = getCogntLinkedDocToolbarModel(event);
  if (toolbarModel) {
    return toolbarModel;
  }

  const blockSelection = editor.host?.selection.find('block') as
    | { blockId?: string }
    | null
    | undefined;
  const model = blockSelection?.blockId
    ? currentDoc?.getBlock(blockSelection.blockId)?.model
    : null;

  return isCogntLinkedDocModel(model) && getCogntLinkedNotePathFromModel(model)
    ? model
    : null;
}
