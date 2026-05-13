import { getReferencePopoverContainers } from './mention-controller-anchors';

function escapeHtml(value: string) {
  const element = document.createElement('div');
  element.textContent = value;
  return element.innerHTML;
}

export const nativeReferencePreviewId = 'cognt-reference-preview';
export const nativeMentionSearchPreviewId = 'cognt-linked-doc-search-preview';
const nativeReferencePreviewStyleId = 'cognt-reference-preview-style';
const nativeMentionSearchPreviewStyleId = 'cognt-linked-doc-search-preview-style';

export function buildNativeReferenceExtensionHtml(relativePath: string, previewHtml: string) {
  return `
    <div class="cognt-reference-actions" data-cognt-reference-path="${escapeHtml(relativePath)}">
      <div class="cognt-reference-meta">
        <span class="cognt-reference-kicker">Cognt note</span>
        <span class="cognt-reference-path">${escapeHtml(relativePath)}</span>
      </div>
      <button type="button" class="cognt-reference-action" data-cognt-reference-action="open">
        Open
      </button>
      <button type="button" class="cognt-reference-action" data-cognt-reference-action="copy-path">
        Copy path
      </button>
    </div>
    <div class="cognt-reference-preview-content">
      ${previewHtml}
    </div>
  `;
}

export function injectNativeReferencePreviewStyle(root: ShadowRoot) {
  if (root.getElementById(nativeReferencePreviewStyleId)) {
    return;
  }

  const style = document.createElement('style');
  style.id = nativeReferencePreviewStyleId;
  style.textContent = `
    .affine-reference-popover-container {
      min-width: 420px;
      max-width: min(720px, calc(100vw - 24px));
    }

    .affine-reference-popover-container editor-toolbar.affine-reference-popover.view {
      box-sizing: border-box;
      width: 100%;
    }

    #${nativeReferencePreviewId} {
      box-sizing: border-box;
      width: 100%;
      max-height: 340px;
      margin-top: 8px;
      overflow: hidden;
      border: 1px solid var(--affine-border-color, rgba(30, 41, 59, 0.12));
      border-radius: 8px;
      background: var(--affine-background-primary-color, #fff);
      box-shadow: var(--affine-shadow-2, 0 10px 30px rgba(15, 23, 42, 0.12));
      color: var(--affine-text-primary-color, #1f2937);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm, 14px);
      line-height: 1.45;
    }

    #${nativeReferencePreviewId} .cognt-reference-actions {
      box-sizing: border-box;
      display: flex;
      align-items: center;
      gap: 8px;
      min-width: 0;
      padding: 8px 10px;
      border-bottom: 1px solid var(--affine-border-color, rgba(30, 41, 59, 0.12));
      background: var(--affine-background-secondary-color, #f8fafc);
    }

    #${nativeReferencePreviewId} .cognt-reference-meta {
      display: grid;
      flex: 1 1 auto;
      min-width: 0;
      gap: 1px;
    }

    #${nativeReferencePreviewId} .cognt-reference-kicker {
      color: var(--affine-text-secondary-color, #6b7280);
      font-size: 11px;
      line-height: 1.2;
    }

    #${nativeReferencePreviewId} .cognt-reference-path {
      min-width: 0;
      overflow: hidden;
      color: var(--affine-text-primary-color, #1f2937);
      font-size: 12px;
      line-height: 1.25;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    #${nativeReferencePreviewId} .cognt-reference-action {
      flex: 0 0 auto;
      height: 28px;
      padding: 0 10px;
      border: 1px solid var(--affine-border-color, rgba(30, 41, 59, 0.14));
      border-radius: 6px;
      background: var(--affine-background-primary-color, #fff);
      color: var(--affine-text-primary-color, #1f2937);
      cursor: pointer;
      font: inherit;
      font-size: 12px;
      line-height: 26px;
    }

    #${nativeReferencePreviewId} .cognt-reference-action:hover {
      background: var(--affine-hover-color, rgba(30, 41, 59, 0.06));
    }

    #${nativeReferencePreviewId} .cognt-reference-preview-content {
      min-height: 0;
    }

    #${nativeReferencePreviewId} .mention-preview-body {
      box-sizing: border-box;
      max-height: 256px;
      overflow: auto;
      padding: 12px 14px;
      outline: none;
    }

    #${nativeReferencePreviewId} .mention-preview-body--static {
      color: var(--affine-text-secondary-color, #6b7280);
    }

    #${nativeReferencePreviewId} .mention-preview-body p {
      margin: 0 0 8px;
      overflow-wrap: anywhere;
      word-break: break-word;
      white-space: normal;
    }

    #${nativeReferencePreviewId} .mention-preview-body p:last-child {
      margin-bottom: 0;
    }

    #${nativeReferencePreviewId} .mention-preview-empty,
    #${nativeReferencePreviewId} .mention-preview-error {
      color: var(--affine-text-secondary-color, #6b7280);
    }
  `;
  root.append(style);
}

export function injectNativeMentionSearchPreviewStyle(root: ShadowRoot) {
  if (root.getElementById(nativeMentionSearchPreviewStyleId)) {
    return;
  }

  const style = document.createElement('style');
  style.id = nativeMentionSearchPreviewStyleId;
  style.textContent = `
    .linked-doc-popover {
      width: 420px;
      max-width: min(720px, calc(100vw - 24px));
    }

    .linked-doc-popover .group icon-button {
      width: 100%;
    }

    #${nativeMentionSearchPreviewId} {
      box-sizing: border-box;
      width: 100%;
      max-height: 240px;
      margin-top: 8px;
      overflow: hidden;
      border-top: 1px solid var(--affine-border-color, rgba(30, 41, 59, 0.12));
      color: var(--affine-text-primary-color, #1f2937);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm, 14px);
      line-height: 1.45;
    }

    #${nativeMentionSearchPreviewId} .mention-preview-body {
      box-sizing: border-box;
      max-height: 238px;
      overflow: auto;
      padding: 12px 14px 4px;
      outline: none;
    }

    #${nativeMentionSearchPreviewId} .mention-preview-body--static {
      color: var(--affine-text-secondary-color, #6b7280);
    }

    #${nativeMentionSearchPreviewId} .mention-preview-body p {
      margin: 0 0 8px;
      overflow-wrap: anywhere;
      word-break: break-word;
      white-space: normal;
    }

    #${nativeMentionSearchPreviewId} .mention-preview-body p:last-child {
      margin-bottom: 0;
    }

    #${nativeMentionSearchPreviewId} .mention-preview-empty,
    #${nativeMentionSearchPreviewId} .mention-preview-error {
      color: var(--affine-text-secondary-color, #6b7280);
    }
  `;
  root.append(style);
}

export function removeNativeReferencePreview() {
  for (const container of getReferencePopoverContainers()) {
    container.querySelector(`#${nativeReferencePreviewId}`)?.remove();
  }
}

export function removeNativeMentionSearchPreview() {
  for (const element of Array.from(document.querySelectorAll<HTMLElement>('affine-linked-doc-popover'))) {
    element.shadowRoot?.getElementById(nativeMentionSearchPreviewId)?.remove();
  }
}

export function isReferencePopupTarget(target: Node) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.shadowRoot?.querySelector('.affine-reference-popover-container')
      ?? target.closest('reference-popup')
  );
}

export function isLinkedDocPopoverTarget(target: Node) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.shadowRoot?.querySelector('.linked-doc-popover')
      ?? target.closest('affine-linked-doc-popover')
  );
}
