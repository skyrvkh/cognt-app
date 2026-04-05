import {
  getFloatingAnchorPoint,
  getLinkedDocPopoverContainer,
  getReferencePopoverContainer,
  getReferencePopoverRect,
  isElementAnchor,
  type FloatingAnchor,
} from './mention-controller-anchors';
import {
  buildNativeReferenceExtensionHtml,
  injectNativeMentionSearchPreviewStyle,
  injectNativeReferencePreviewStyle,
  nativeMentionSearchPreviewId,
  nativeReferencePreviewId,
} from './mention-controller-native-preview';

export type MentionPreviewRendererOptions = {
  previewElement: HTMLElement;
  shouldRenderNativeMentionSearchPreview(anchor: HTMLElement): boolean;
  shouldRenderNativeReferencePreview(anchor: HTMLElement): boolean;
  openMention(relativePath: string): void | Promise<void>;
  copyMentionPath(relativePath: string): void | Promise<void>;
};

export class MentionPreviewRenderer {
  private readonly previewElement: HTMLElement;

  public constructor(private readonly options: MentionPreviewRendererOptions) {
    this.previewElement = options.previewElement;
  }

  public renderNativeMentionSearchPreview(
    anchor: HTMLElement,
    htmlContent: string,
    attempt = 0
  ) {
    const container = getLinkedDocPopoverContainer(anchor);
    if (!container) {
      if (attempt < 8) {
        window.requestAnimationFrame(() => {
          if (this.options.shouldRenderNativeMentionSearchPreview(anchor)) {
            this.renderNativeMentionSearchPreview(anchor, htmlContent, attempt + 1);
          }
        });
      }
      return;
    }

    const root = container.getRootNode();
    if (root instanceof ShadowRoot) {
      injectNativeMentionSearchPreviewStyle(root);
    }

    let preview = container.querySelector<HTMLElement>(`#${nativeMentionSearchPreviewId}`);
    if (!preview) {
      preview = document.createElement('div');
      preview.id = nativeMentionSearchPreviewId;
      preview.className = nativeMentionSearchPreviewId;
      container.append(preview);
    }

    preview.innerHTML = htmlContent;
  }

  public renderNativeReferencePreview(
    anchor: HTMLElement,
    relativePath: string,
    htmlContent: string,
    attempt = 0
  ) {
    const container = getReferencePopoverContainer(anchor.getBoundingClientRect());
    if (!container) {
      if (attempt < 16) {
        window.requestAnimationFrame(() => {
          if (this.options.shouldRenderNativeReferencePreview(anchor)) {
            this.renderNativeReferencePreview(anchor, relativePath, htmlContent, attempt + 1);
          }
        });
      } else if (this.options.shouldRenderNativeReferencePreview(anchor)) {
        this.renderFloatingReferencePreview(anchor, htmlContent);
      }
      return;
    }

    this.previewElement.dataset.visible = 'false';
    this.previewElement.dataset.mode = '';
    this.previewElement.dataset.anchor = '';
    this.previewElement.innerHTML = '';

    const root = container.getRootNode();
    if (root instanceof ShadowRoot) {
      injectNativeReferencePreviewStyle(root);
    }

    let preview = container.querySelector<HTMLElement>(`#${nativeReferencePreviewId}`);
    if (!preview) {
      preview = document.createElement('div');
      preview.id = nativeReferencePreviewId;
      preview.className = nativeReferencePreviewId;
      container.append(preview);
    }

    preview.innerHTML = buildNativeReferenceExtensionHtml(relativePath, htmlContent);
    this.bindNativeReferenceExtension(preview, relativePath);
  }

  public renderFloatingReferencePreview(anchor: HTMLElement, htmlContent: string) {
    this.previewElement.dataset.mode = 'preview';
    this.previewElement.dataset.anchor = 'linked-page';
    this.previewElement.innerHTML = htmlContent;
    this.previewElement.dataset.visible = 'true';
    this.positionFloatingElement(this.previewElement, anchor);
    window.requestAnimationFrame(() => {
      if (this.options.shouldRenderNativeReferencePreview(anchor)) {
        this.positionFloatingElement(this.previewElement, anchor);
      }
    });
  }

  public positionFloatingElement(element: HTMLElement, anchor: FloatingAnchor) {
    const margin = 12;
    const preferred = getFloatingAnchorPoint(anchor);
    const preferredWidth = isElementAnchor(anchor)
      ? getReferencePopoverRect(anchor.getBoundingClientRect())?.width
      : undefined;

    if (preferredWidth && preferredWidth > 0) {
      element.style.minWidth = `${Math.min(
        Math.max(preferredWidth, 360),
        window.innerWidth - margin * 2
      )}px`;
    } else {
      element.style.minWidth = '';
    }

    const width = element.offsetWidth || 320;
    const height = element.offsetHeight || 220;
    const left = Math.min(
      Math.max(margin, preferred.x),
      Math.max(margin, window.innerWidth - width - margin)
    );
    const top = Math.min(
      Math.max(margin, preferred.y),
      Math.max(margin, window.innerHeight - height - margin)
    );

    element.style.left = `${left}px`;
    element.style.top = `${top}px`;
  }

  private bindNativeReferenceExtension(preview: HTMLElement, relativePath: string) {
    preview.querySelector<HTMLElement>('[data-cognt-reference-action="open"]')
      ?.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        void this.options.openMention(relativePath);
      });

    preview.querySelector<HTMLElement>('[data-cognt-reference-action="copy-path"]')
      ?.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        void this.options.copyMentionPath(relativePath);
      });
  }
}
