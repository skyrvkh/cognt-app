export function collectTextNodesDeep(root: Node) {
  const nodes: Text[] = [];
  const visit = (node: Node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      nodes.push(node as Text);
      return;
    }

    if (!canContainTextNodes(node)) {
      return;
    }

    if (node instanceof Element && node.shadowRoot) {
      visit(node.shadowRoot);
    }

    for (const child of Array.from(node.childNodes)) {
      visit(child);
    }
  };

  visit(root);
  return nodes;
}

export function findAncestorElement(
  element: Element,
  predicate: (element: Element) => boolean
): Element | null {
  let current: Element | null = element;
  while (current) {
    if (predicate(current)) {
      return current;
    }

    if (current.parentElement) {
      current = current.parentElement;
      continue;
    }

    const root = current.getRootNode();
    current = root instanceof ShadowRoot ? root.host : null;
  }

  return null;
}

export function getElementFromNode(node: Node | null): Element | null {
  return node instanceof Element ? node : node?.parentElement ?? null;
}

export function getSelectionAnchorElement() {
  const selection = window.getSelection();
  return getElementFromNode(selection?.anchorNode ?? null);
}

export function getClosestBlockId(element: Element) {
  return element.closest('[data-block-id]')?.getAttribute('data-block-id')?.trim() || '';
}

export function isNodeInsideRoot(node: Node, root: Node) {
  let current: Node | null = node;
  while (current) {
    if (current === root) {
      return true;
    }

    const parent: Node | null = current.parentNode;
    if (parent) {
      current = parent;
      continue;
    }

    const ownerRoot: Node = current.getRootNode();
    current = ownerRoot instanceof ShadowRoot ? ownerRoot.host : null;
  }

  return false;
}

export function canContainTextNodes(node: Node) {
  return node.nodeType === Node.ELEMENT_NODE || node.nodeType === Node.DOCUMENT_FRAGMENT_NODE;
}
