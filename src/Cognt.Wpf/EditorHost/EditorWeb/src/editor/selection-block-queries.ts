import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import { getBlockSelectionId } from './block-models';

export function getSelectedBlockModel(editor: PageEditor, doc: Doc | null) {
  const blockSelection = editor.host?.selection.find('block') as
    | { blockId?: string | null }
    | null
    | undefined;
  const blockId = typeof blockSelection?.blockId === 'string'
    ? blockSelection.blockId
    : null;

  return blockId && doc
    ? doc.getBlock(blockId)?.model ?? null
    : null;
}

export function getCurrentInsertionBlockModel(
  editor: PageEditor,
  doc: Doc | null,
  textSelection?: TextSelection | null
) {
  return getCurrentTextSelectionBlockModel(editor, doc, textSelection)
    ?? getSelectedBlockModel(editor, doc);
}

export function getCurrentTextSelectionBlockModel(
  editor: PageEditor,
  doc: Doc | null,
  textSelection?: TextSelection | null
) {
  if (!doc) {
    return null;
  }

  const blockIds = textSelection
    ? getTextSelectionBlockIds(editor, textSelection)
    : [];
  const blockId = blockIds.length > 0 ? blockIds[blockIds.length - 1] : null;
  return blockId
    ? doc.getBlock(blockId)?.model ?? null
    : null;
}

export function getTextSelectionBlockIds(editor: PageEditor, textSelection: TextSelection) {
  const range = editor.std?.range.textSelectionToRange(textSelection as never);
  const components = (range
    ? editor.std.range.getSelectedBlockComponentsByRange(range, {
        match: (element: { model?: { id?: string; role?: string } }) =>
          Boolean(element.model?.id) && element.model?.role === 'content',
        mode: 'flat',
      })
    : []) as Array<{ model?: { id?: string } }>;

  const componentIds = components
    .map((component: { model?: { id?: string } }) => component.model?.id)
    .filter((blockId: string | undefined): blockId is string => Boolean(blockId));
  if (componentIds.length > 0) {
    return componentIds;
  }

  return [
    textSelection.start?.blockId,
    textSelection.from?.blockId,
    textSelection.end?.blockId,
    textSelection.to?.blockId,
  ].filter((blockId: string | undefined): blockId is string => Boolean(blockId));
}

export function getBlockSelectionBlockIds(editor: PageEditor) {
  const selection = editor.host?.selection as
    | {
        find(type: string): unknown;
        filter?(type: string): unknown[];
      }
    | undefined;
  if (!selection) {
    return [];
  }

  const blockSelections = typeof selection.filter === 'function'
    ? selection.filter('block')
    : [selection.find('block')];

  return blockSelections.flatMap(getBlockSelectionId);
}

export function getTextSelectionImageCandidateBlockIds(
  editor: PageEditor,
  textSelection?: TextSelection | null
) {
  return textSelection ? getTextSelectionBlockIds(editor, textSelection) : [];
}
