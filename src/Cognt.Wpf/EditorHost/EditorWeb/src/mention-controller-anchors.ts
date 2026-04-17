export type Point = {
  x: number;
  y: number;
};

export type FloatingAnchor = Point | HTMLElement;

export function isElementAnchor(anchor: FloatingAnchor): anchor is HTMLElement {
  return anchor instanceof HTMLElement;
}

export function getNativeMentionMenuItemFromEvent(event: MouseEvent) {
  for (const item of event.composedPath()) {
    if (item instanceof HTMLElement && isNativeMentionMenuItem(item)) {
      return item;
    }
  }

  return null;
}

export function isNativeMentionMenuItem(element: HTMLElement) {
  return Boolean(getNativeMentionMenuItemPath(element));
}

export function getNativeMentionMenuItemPath(element: HTMLElement) {
  const key = element.dataset.id;
  return key?.startsWith('note:') ? key.slice('note:'.length) : null;
}

export function findNativeMentionMenuItem(relativePath: string) {
  const key = `note:${relativePath}`;

  for (const element of Array.from(document.querySelectorAll<HTMLElement>('affine-linked-doc-popover'))) {
    const item = element.shadowRoot?.querySelector<HTMLElement>(
      `icon-button[data-id="${escapeCssIdentifier(key)}"]`
    );
    if (item) {
      return item;
    }
  }

  return null;
}

export function getLinkedDocPopoverContainer(anchor: HTMLElement) {
  const root = anchor.getRootNode();
  if (root instanceof ShadowRoot) {
    const container = root.querySelector<HTMLElement>('.linked-doc-popover');
    if (container) {
      return container;
    }
  }

  for (const element of Array.from(document.querySelectorAll<HTMLElement>('affine-linked-doc-popover'))) {
    const container = element.shadowRoot?.querySelector<HTMLElement>('.linked-doc-popover');
    if (container) {
      return container;
    }
  }

  return null;
}

export function getFloatingAnchorPoint(anchor: FloatingAnchor): Point {
  if (!isElementAnchor(anchor)) {
    return anchor;
  }

  const referenceRect = anchor.getBoundingClientRect();
  const popoverRect = getReferencePopoverRect(referenceRect);
  if (popoverRect) {
    return {
      x: popoverRect.left,
      y: popoverRect.bottom + 4,
    };
  }

  return {
    x: referenceRect.left,
    y: referenceRect.bottom + 72,
  };
}

export function getReferencePopoverRect(referenceRect: DOMRect) {
  return getReferencePopoverContainer(referenceRect)?.getBoundingClientRect() ?? null;
}

export function getReferencePopoverContainer(referenceRect: DOMRect) {
  let bestContainer: HTMLElement | null = null;
  let bestScore = Number.POSITIVE_INFINITY;

  for (const container of getReferencePopoverContainers()) {
    const rect = container.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
      continue;
    }

    const score = Math.abs(rect.left - referenceRect.left)
      + Math.abs(rect.top - referenceRect.bottom);
    if (score < bestScore) {
      bestScore = score;
      bestContainer = container;
    }
  }

  return bestContainer;
}

export function getReferencePopoverContainers() {
  const containers = new Set<HTMLElement>();

  for (const popup of Array.from(document.querySelectorAll<HTMLElement>('reference-popup'))) {
    const container = popup.shadowRoot?.querySelector<HTMLElement>(
      '.affine-reference-popover-container'
    );
    if (container) {
      containers.add(container);
    }
  }

  for (const element of Array.from(document.body.children)) {
    if (!(element instanceof HTMLElement) || !element.shadowRoot) {
      continue;
    }

    const container = element.shadowRoot.querySelector<HTMLElement>(
      '.affine-reference-popover-container'
    );
    if (container) {
      containers.add(container);
    }
  }

  return [...containers];
}

function escapeCssIdentifier(value: string) {
  return typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, match => `\\${match}`);
}
