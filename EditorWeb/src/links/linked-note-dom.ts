import { findElementsDeep } from '../editor/dom';
import {
  cogntLinkedDocWrapStyleId,
  embedLinkedDocTagName,
  type LinkedPageReference,
} from './linked-note-types';
import {
  getCogntLinkedNotePath,
  getCogntLinkedNoteTitle,
} from './linked-note-paths';

export function getInlineLinkedPageReferenceFromEvent(event: MouseEvent): LinkedPageReference | null {
  const element = getInlineLinkedPageReferenceElementFromEvent(event);
  return element ? getInlineLinkedPageReference(element) : null;
}

export function getInlineLinkedPageReferenceElementFromEvent(
  event: MouseEvent
): (HTMLElement & {
  referenceInfo?: unknown;
  delta?: { attributes?: { reference?: unknown } };
}) | null {
  return event.composedPath().find(isInlineLinkedPageReferenceElement) ?? null;
}

export function getInlineLinkedPageReference(
  element: HTMLElement & {
    referenceInfo?: unknown;
    delta?: { attributes?: { reference?: unknown } };
  }
): LinkedPageReference | null {
  const reference = element?.referenceInfo ?? element?.delta?.attributes?.reference;

  return isLinkedPageReference(reference) ? reference : null;
}

export function getCogntLinkedDocOpenIntent(event: MouseEvent) {
  const path = event.composedPath();
  const block = path.find(isEmbedLinkedDocBlockElement) as
    | (HTMLElement & { model?: unknown })
    | undefined;

  if (!block || !path.some(isLinkedDocCardOpenTarget)) {
    return null;
  }

  return isCogntLinkedDocModel(block.model)
    && getCogntLinkedNotePathFromModel(block.model)
    ? block.model
    : null;
}

export function isOpenThisDocToolbarIntent(event: MouseEvent) {
  return event.composedPath().some(isOpenThisDocActionElement);
}

export function getCogntLinkedDocToolbarModel(event: MouseEvent) {
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

  return null;
}

export function getLinkedDocReferenceFromModel(model: unknown) {
  if (!isCogntLinkedDocModel(model)) {
    return null;
  }

  const linkedDocModel = model as Record<string, unknown> & { pageId: string };

  return {
    pageId: linkedDocModel.pageId,
    relativePath: getCogntLinkedNotePathFromModel(linkedDocModel) ?? undefined,
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
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  return getCogntLinkedNotePath(record) ?? getCogntLinkedNotePath(props);
}

export function getLinkedNoteTitleFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
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

function injectShadowStyle(element: HTMLElement, cssText: string) {
  const root = element.shadowRoot;
  if (!root || root.getElementById(cogntLinkedDocWrapStyleId)) {
    return;
  }

  const style = document.createElement('style');
  style.id = cogntLinkedDocWrapStyleId;
  style.textContent = cssText;
  root.prepend(style);
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
