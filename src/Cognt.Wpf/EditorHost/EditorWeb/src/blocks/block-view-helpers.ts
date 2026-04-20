import { BlockComponent } from '@blocksuite/block-std';

export function configureNonEditableBlockView(component: BlockComponent) {
  component.contentEditable = 'false';
}

export function stopBlockEventPropagation(event: Event) {
  event.stopPropagation();
}

export function stopBlockControlEvent(event: Event) {
  event.preventDefault();
  event.stopPropagation();
}

export function selectCurrentBlock(component: BlockComponent, event: Event) {
  event.stopPropagation();
  component.host.selection.setGroup('note', [
    component.host.selection.create('block', { blockId: component.blockId }),
  ]);
}

export function defineBlockElementOnce(
  tagName: string,
  constructor: CustomElementConstructor
) {
  if (!customElements.get(tagName)) {
    customElements.define(tagName, constructor);
  }
}
