import type { Doc } from '@blocksuite/store';

export function findEditableElement(root: ParentNode | ShadowRoot | null): HTMLElement | null {
  if (!root) {
    return null;
  }

  const editable = findElementDeep(root, '[contenteditable="true"], [contenteditable=""], textarea, input');
  if (editable) {
    return editable;
  }

  return findElementDeep(root, '.inline-editor, rich-text');
}

export function getInteractivityDiagnostic(doc: Doc | null, root: ParentNode | ShadowRoot) {
  const editable = findEditableElement(root);
  const readOnlyRichTexts = findElementsDeep(root, 'rich-text')
    .filter(element => (element as HTMLElement & { readonly?: boolean }).readonly);
  const falseContentEditableCount = findElementsDeep(root, '[contenteditable="false"]').length;

  return [
    `docReadonly=${String(doc?.readonly ?? false)}`,
    `editable=${String(!!editable)}`,
    `editableTag=${editable?.tagName.toLowerCase() ?? 'none'}`,
    `editableContentEditable=${editable?.getAttribute('contenteditable') ?? 'none'}`,
    `readOnlyRichTexts=${readOnlyRichTexts.length}`,
    `falseContentEditable=${falseContentEditableCount}`,
  ].join(', ');
}

export function findElementDeep(root: ParentNode | ShadowRoot, selector: string): HTMLElement | null {
  const direct = root.querySelector<HTMLElement>(selector);

  if (direct) {
    return direct;
  }

  for (const element of root.querySelectorAll<HTMLElement>('*')) {
    const found = element.shadowRoot ? findElementDeep(element.shadowRoot, selector) : null;
    if (found) {
      return found;
    }
  }

  return null;
}

export function findElementsDeep(root: ParentNode | ShadowRoot, selector: string): HTMLElement[] {
  const elements = Array.from(root.querySelectorAll<HTMLElement>(selector));

  if (root instanceof HTMLElement && root.shadowRoot) {
    elements.push(...findElementsDeep(root.shadowRoot, selector));
  }

  for (const element of root.querySelectorAll<HTMLElement>('*')) {
    if (element.shadowRoot) {
      elements.push(...findElementsDeep(element.shadowRoot, selector));
    }
  }

  return elements;
}

export function waitForNextAnimationFrame() {
  return new Promise<void>(resolve => {
    window.requestAnimationFrame(() => resolve());
  });
}
