import { findElementsDeep } from '../editor/dom';
import {
  cogntInlineReferenceStyleId,
  cogntLinkedDocWrapStyleId,
  embedLinkedDocTagName,
} from './linked-note-types';
import {
  getIconOnlyInlineReferenceTitle,
  getInlineLinkedPageReference,
  isIconOnlyInlineReference,
} from './linked-note-dom-events';

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
    if (reference && isIconOnlyInlineReference(reference)) {
      element.setAttribute('data-cognt-link-display', 'icon');
      element.setAttribute('title', getIconOnlyInlineReferenceTitle(reference));
      injectShadowStyle(element, iconOnlyInlineReferenceCss(), cogntInlineReferenceStyleId);
    } else {
      element.removeAttribute('data-cognt-link-display');
    }
  }
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
