import { findElementsDeep } from '../editor/dom';
import {
  cogntInlineReferenceStyleId,
  cogntLinkedDocWrapStyleId,
  embedLinkedDocTagName,
  type LinkedPageReference,
} from './linked-note-types';
import { reportDiagnostic } from '../host';
import {
  getCogntLinkedNotePath,
  getCogntLinkedNoteTitle,
  getLinkedPageReferencePath,
  getLinkedPageReferenceTitle,
  isIconOnlyLinkedPageReference,
} from './linked-note-paths';

export function getInlineLinkedPageReferenceFromEvent(event: MouseEvent): LinkedPageReference | null {
  const element = getInlineLinkedPageReferenceElementFromEvent(event);
  const reference = element ? getInlineLinkedPageReference(element) : null;
  if (!reference) {
    reportDiagnostic('warn', 'Inline linked page reference was not found for event.', {
      eventType: event.type,
      hasElement: element !== null,
    });
  }

  return reference;
}

export function getInlineLinkedPageReferenceElementFromEvent(
  event: MouseEvent
): (HTMLElement & {
  referenceInfo?: unknown;
  delta?: { attributes?: { reference?: unknown } };
}) | null {
  const path = event.composedPath();
  const element = path.find(isInlineLinkedPageReferenceElement) ?? null;
  if (!element) {
    reportDiagnostic('warn', 'Inline linked page reference element was not found for event.', {
      eventType: event.type,
      pathLength: path.length,
    });
  }

  return element;
}

export function getInlineLinkedPageReference(
  element: HTMLElement & {
    referenceInfo?: unknown;
    delta?: { attributes?: { reference?: unknown } };
  }
): LinkedPageReference | null {
  const reference = element?.referenceInfo ?? element?.delta?.attributes?.reference;

  const linkedReference = isLinkedPageReference(reference) ? reference : null;
  if (!linkedReference) {
    reportDiagnostic('warn', 'Inline linked page reference element did not contain reference metadata.', {
      hasReferenceInfo: element.referenceInfo !== undefined,
      hasDeltaReference: element.delta?.attributes?.reference !== undefined,
    });
  }

  return linkedReference;
}

export function getCogntLinkedDocOpenIntent(event: MouseEvent) {
  const path = event.composedPath();
  const block = path.find(isEmbedLinkedDocBlockElement) as
    | (HTMLElement & { model?: unknown })
    | undefined;

  if (!block || !path.some(isLinkedDocCardOpenTarget)) {
    return null;
  }

  const model = isCogntLinkedDocModel(block.model)
    && getCogntLinkedNotePathFromModel(block.model)
    ? block.model
    : null;
  if (!model) {
    reportDiagnostic('warn', 'Cognt linked doc open intent did not include a usable model.', {
      eventType: event.type,
    });
  }

  return model;
}

export function isOpenThisDocToolbarIntent(event: MouseEvent) {
  return event.composedPath().some(isOpenThisDocActionElement);
}

export function getCogntLinkedDocToolbarModel(event: MouseEvent) {
  const path = event.composedPath();
  for (const target of event.composedPath()) {
    if (typeof target !== 'object' || target === null) {
      continue;
    }

    const candidate = target as { focusModel?: unknown; model?: unknown };
    const model = candidate.focusModel ?? candidate.model;
    if (isCogntLinkedDocModel(model) && getCogntLinkedNotePathFromModel(model)) {
      return model;
    }
  }

  reportDiagnostic('warn', 'Cognt linked doc toolbar model was not found.', {
    eventType: event.type,
    pathLength: path.length,
  });
  return null;
}

export function getLinkedDocReferenceFromModel(model: unknown) {
  if (!isCogntLinkedDocModel(model)) {
    reportDiagnostic('warn', 'Cannot create linked doc reference from non-linked-doc model.', {
      modelType: typeof model,
    });
    return null;
  }

  const linkedDocModel = model as Record<string, unknown> & { pageId: string };
  const relativePath = getCogntLinkedNotePathFromModel(linkedDocModel) ?? undefined;

  return {
    pageId: linkedDocModel.pageId,
    relativePath,
  };
}

export function isCogntLinkedDocModel(model: unknown) {
  return typeof model === 'object'
    && model !== null
    && (model as { flavour?: unknown }).flavour === 'affine:embed-linked-doc'
    && typeof (model as { pageId?: unknown }).pageId === 'string';
}

export function getCogntLinkedNotePathFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    reportDiagnostic('warn', 'Cannot resolve Cognt linked note path from non-object model.', {
      modelType: typeof model,
    });
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  const relativePath = getCogntLinkedNotePath(record) ?? getCogntLinkedNotePath(props);
  if (!relativePath) {
    reportDiagnostic('warn', 'Could not resolve Cognt linked note path from model.', {
      hasProps: Object.keys(props).length > 0,
    });
  }

  return relativePath;
}

export function getLinkedNoteTitleFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    reportDiagnostic('warn', 'Cannot resolve linked note title from non-object model.', {
      modelType: typeof model,
    });
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  return getCogntLinkedNoteTitle(record) ?? getCogntLinkedNoteTitle(props);
}

export function patchRenderedEmbedLinkedDocBlocks(root: ParentNode | ShadowRoot) {
  for (const block of findElementsDeep(root, embedLinkedDocTagName)) {
    injectShadowStyle(block, linkedDocCardWrapCss());

    for (const child of findElementsDeep(
      block,
      'affine-paragraph, affine-list, rich-text, .inline-editor'
    )) {
      injectShadowStyle(child, linkedDocNestedWrapCss());
    }
  }
}

export function patchRenderedInlineLinkedNoteReferences(root: ParentNode | ShadowRoot) {
  for (const element of findElementsDeep(root, 'affine-reference')) {
    const reference = getInlineLinkedPageReference(element as HTMLElement & {
      referenceInfo?: unknown;
      delta?: { attributes?: { reference?: unknown } };
    });
    if (reference && isIconOnlyLinkedPageReference(reference)) {
      element.setAttribute('data-cognt-link-display', 'icon');
      element.setAttribute(
        'title',
        getLinkedPageReferenceTitle(reference)
          ?? getLinkedPageReferencePath(reference)
          ?? 'Linked note'
      );
      injectShadowStyle(element, iconOnlyInlineReferenceCss(), cogntInlineReferenceStyleId);
    } else {
      element.removeAttribute('data-cognt-link-display');
    }
  }
}

function isInlineLinkedPageReferenceElement(
  value: EventTarget
): value is HTMLElement & {
  referenceInfo?: unknown;
  delta?: { attributes?: { reference?: unknown } };
} {
  return value instanceof HTMLElement && value.localName === 'affine-reference';
}

function isLinkedPageReference(value: unknown): value is LinkedPageReference {
  return typeof value === 'object'
    && value !== null
    && (value as { pageId?: unknown }).pageId !== undefined
    && typeof (value as { pageId?: unknown }).pageId === 'string';
}

function isEmbedLinkedDocBlockElement(value: EventTarget) {
  return value instanceof HTMLElement && value.localName === embedLinkedDocTagName;
}

function isLinkedDocCardOpenTarget(value: EventTarget) {
  return value instanceof HTMLElement
    && (value.classList.contains('affine-embed-linked-doc-content-title-icon')
      || value.classList.contains('affine-embed-linked-doc-content-title-text'));
}

function isOpenThisDocActionElement(value: EventTarget) {
  if (!(value instanceof HTMLElement)) {
    return false;
  }

  const ariaLabel = value.getAttribute('aria-label')?.trim().toLowerCase();
  const actionType = value.getAttribute('type')?.trim().toLowerCase();

  return ariaLabel === 'open this doc'
    || actionType === 'open-this-doc'
    || actionType === 'open';
}

function injectShadowStyle(
  element: HTMLElement,
  cssText: string,
  styleId = cogntLinkedDocWrapStyleId
) {
  const root = element.shadowRoot;
  if (!root || root.getElementById(styleId)) {
    return;
  }

  const style = document.createElement('style');
  style.id = styleId;
  style.textContent = cssText;
  root.prepend(style);
}

function iconOnlyInlineReferenceCss() {
  return `
    :host([data-cognt-link-display="icon"]) {
      display: inline-flex !important;
      width: 1.1em !important;
      min-width: 1.1em !important;
      height: 1.1em !important;
      align-items: center !important;
      justify-content: center !important;
      vertical-align: -0.12em !important;
      border-radius: 999px !important;
      color: var(--cognt-editor-accent-strong, var(--affine-primary-color)) !important;
      text-decoration: none !important;
      cursor: pointer !important;
    }

    :host([data-cognt-link-display="icon"]) * {
      display: none !important;
    }

    :host([data-cognt-link-display="icon"])::after {
      content: "↗";
      display: inline-block;
      font-size: 0.82em;
      line-height: 1;
      font-weight: 700;
    }
  `;
}

function linkedDocCardWrapCss() {
  return `
    .affine-embed-linked-doc-content,
    .affine-embed-linked-doc-content-note,
    .affine-embed-doc-content-note-blocks,
    .affine-embed-doc-content-note-blocks * {
      min-width: 0 !important;
      max-width: 100% !important;
      overflow-wrap: anywhere !important;
      word-break: break-word !important;
      white-space: normal !important;
    }

    .affine-embed-linked-doc-content-note.render {
      overflow: hidden !important;
    }
  `;
}

function linkedDocNestedWrapCss() {
  return `
    :host,
    *,
    .inline-editor,
    rich-text {
      min-width: 0 !important;
      max-width: 100% !important;
      overflow-wrap: anywhere !important;
      word-break: break-word !important;
      white-space: normal !important;
    }
  `;
}
