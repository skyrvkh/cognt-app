import { reportDiagnostic } from '../host';
import type { LinkedPageReference } from './linked-note-types';
import { embedLinkedDocTagName } from './linked-note-types';
import {
  getLinkedPageReferencePath,
  getLinkedPageReferenceTitle,
  isIconOnlyLinkedPageReference,
} from './linked-note-paths';
import {
  getCogntLinkedNotePathFromModel,
  isCogntLinkedDocModel,
} from './linked-note-dom-models';

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

export function getIconOnlyInlineReferenceTitle(reference: LinkedPageReference) {
  return getLinkedPageReferenceTitle(reference)
    ?? getLinkedPageReferencePath(reference)
    ?? 'Linked note';
}

export function isIconOnlyInlineReference(reference: LinkedPageReference) {
  return isIconOnlyLinkedPageReference(reference);
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
