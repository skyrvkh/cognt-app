import {
  getCogntLinkedDocOpenIntent,
  getInlineLinkedPageReferenceFromEvent,
} from '../links/linked-notes';
import type { EditorEventBindingOptions } from './editor-event-types';

export function handleLinkedDocOpenIntent(
  options: EditorEventBindingOptions,
  event: MouseEvent
) {
  const reference = getInlineLinkedPageReferenceFromEvent(event);
  if (reference) {
    event.preventDefault();
    event.stopPropagation();
    void options.openLinkedDocReference(reference);
    return;
  }

  const model =
    getCogntLinkedDocOpenIntent(event) ??
    options.getSelectedLinkedDocModelForOpenIntent(event);
  if (!model) {
    return;
  }

  event.preventDefault();
  event.stopPropagation();
  void options.openLinkedDocFromModel(model);
}
