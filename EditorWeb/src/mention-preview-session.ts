import {
  buildMentionPreviewHtml,
  type MentionPreviewResult,
  type MentionViewMode,
} from './mentions';
import {
  isElementAnchor,
  isNativeMentionMenuItem,
  type FloatingAnchor,
} from './mention-controller-anchors';
import {
  removeNativeMentionSearchPreview,
  removeNativeReferencePreview,
} from './mention-controller-native-preview';
import type {
  OpenMentionMessage,
  RequestMentionPreviewMessage,
} from './mention-controller-types';
import { MentionPreviewRenderer } from './mention-preview-renderer';

const previewDismissDelayMs = 200;
const previewMaxCharacters = 0;

export type MentionPreviewSessionOptions = {
  previewElement: HTMLElement;
  getCurrentPath(): string;
  exportContent(): Promise<string>;
  showStatus(message: string): void;
  onRequestPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
};

export class MentionPreviewSession {
  private readonly previewElement: HTMLElement;
  private readonly renderer: MentionPreviewRenderer;
  private previewHideTimer: number | undefined;
  private pendingPreviewRequestId = '';
  private activeViewMode: MentionViewMode | null = null;
  private viewAnchor: FloatingAnchor | null = null;
  private nativePreviewAnchor: HTMLElement | null = null;
  private nativeMentionMenuPreviewAnchor: HTMLElement | null = null;
  private nativeMentionMenuPreviewPath = '';
  private requestCounter = 0;

  public constructor(private readonly options: MentionPreviewSessionOptions) {
    this.previewElement = options.previewElement;
    this.renderer = new MentionPreviewRenderer({
      previewElement: this.previewElement,
      shouldRenderNativeMentionSearchPreview: anchor =>
        this.nativeMentionMenuPreviewAnchor === anchor && this.activeViewMode === 'preview',
      shouldRenderNativeReferencePreview: anchor =>
        this.nativePreviewAnchor === anchor && this.activeViewMode === 'preview',
      openMention: relativePath => this.openMention(relativePath),
      copyMentionPath: relativePath => this.copyMentionPath(relativePath),
    });
  }

  public getActiveViewMode() {
    return this.activeViewMode;
  }

  public getNativePreviewAnchor() {
    return this.nativePreviewAnchor;
  }

  public isShowingPreview() {
    return this.activeViewMode === 'preview';
  }

  public handlePreviewResult(result: MentionPreviewResult) {
    if (result.requestId !== this.pendingPreviewRequestId) {
      return;
    }

    this.cancelScheduledPreviewHide();
    const mode = result.mode ?? this.activeViewMode ?? 'preview';
    this.activeViewMode = mode;

    if (this.nativePreviewAnchor && mode === 'preview') {
      this.renderer.renderNativeReferencePreview(
        this.nativePreviewAnchor,
        result.relativePath,
        buildMentionPreviewHtml(result)
      );
      return;
    }

    if (this.nativeMentionMenuPreviewAnchor && mode === 'preview') {
      this.renderer.renderNativeMentionSearchPreview(
        this.nativeMentionMenuPreviewAnchor,
        buildMentionPreviewHtml(result)
      );
      return;
    }

    this.previewElement.dataset.mode = mode;
    this.previewElement.innerHTML = buildMentionPreviewHtml(result);
    this.previewElement.dataset.visible = 'true';

    if (this.viewAnchor) {
      this.renderer.positionFloatingElement(this.previewElement, this.viewAnchor);
    }
  }

  public handleOpenResult(ok: boolean, error?: string) {
    if (!ok) {
      this.options.showStatus(`Open failed: ${error ?? 'Could not open note.'}`);
    }
  }

  public hidePreview() {
    this.pendingPreviewRequestId = '';
    this.activeViewMode = null;
    this.viewAnchor = null;
    this.nativePreviewAnchor = null;
    this.nativeMentionMenuPreviewAnchor = null;
    this.nativeMentionMenuPreviewPath = '';
    this.cancelScheduledPreviewHide();
    removeNativeReferencePreview();
    removeNativeMentionSearchPreview();
    this.previewElement.dataset.visible = 'false';
    this.previewElement.dataset.mode = '';
    this.previewElement.dataset.anchor = '';
    this.previewElement.innerHTML = '';
  }

  public schedulePreviewHide(onBeforeHide: () => void) {
    if (this.activeViewMode !== 'preview' || this.previewHideTimer !== undefined) {
      return;
    }

    this.previewHideTimer = window.setTimeout(() => {
      this.previewHideTimer = undefined;

      if (this.activeViewMode === 'preview') {
        onBeforeHide();
        this.hidePreview();
      }
    }, previewDismissDelayMs);
  }

  public cancelScheduledPreviewHide() {
    if (this.previewHideTimer === undefined) {
      return;
    }

    window.clearTimeout(this.previewHideTimer);
    this.previewHideTimer = undefined;
  }

  public requestNativeMentionMenuPreview(relativePath: string, anchor: HTMLElement) {
    if (
      this.nativeMentionMenuPreviewAnchor === anchor
      && this.nativeMentionMenuPreviewPath === relativePath
      && this.activeViewMode === 'preview'
    ) {
      return;
    }

    this.nativeMentionMenuPreviewAnchor = anchor;
    this.nativeMentionMenuPreviewPath = relativePath;
    this.nativePreviewAnchor = null;
    this.requestPreview(relativePath, anchor, 'preview');
  }

  public requestPreview(relativePath: string, anchor: FloatingAnchor, mode: MentionViewMode) {
    const currentPath = this.options.getCurrentPath();
    if (!currentPath) {
      return;
    }

    this.cancelScheduledPreviewHide();
    const requestId = this.makeRequestId();
    this.pendingPreviewRequestId = requestId;
    this.activeViewMode = mode;
    this.viewAnchor = anchor;

    if (mode === 'preview' && isElementAnchor(anchor) && isNativeMentionMenuItem(anchor)) {
      this.nativePreviewAnchor = null;
      this.nativeMentionMenuPreviewAnchor = anchor;
      this.hideFloatingPreview();
      this.renderer.renderNativeMentionSearchPreview(
        anchor,
        `<div class="mention-preview-body mention-preview-body--static"><p>Loading preview...</p></div>`
      );
    } else if (mode === 'preview' && isElementAnchor(anchor)) {
      this.nativePreviewAnchor = anchor;
      this.nativeMentionMenuPreviewAnchor = null;
      this.nativeMentionMenuPreviewPath = '';
      this.hideFloatingPreview();
      this.renderer.renderNativeReferencePreview(
        anchor,
        relativePath,
        `<div class="mention-preview-body mention-preview-body--static"><p>Loading preview...</p></div>`
      );
    } else {
      this.nativePreviewAnchor = null;
      this.nativeMentionMenuPreviewAnchor = null;
      this.nativeMentionMenuPreviewPath = '';
      this.previewElement.dataset.mode = mode;
      this.previewElement.dataset.anchor = isElementAnchor(anchor) ? 'linked-page' : '';
      this.previewElement.innerHTML = `<div class="mention-preview-body mention-preview-body--static"><p>${
        mode === 'full' ? 'Opening document...' : 'Loading preview...'
      }</p></div>`;
      this.previewElement.dataset.visible = 'true';
      this.renderer.positionFloatingElement(this.previewElement, anchor);
      window.requestAnimationFrame(() =>
        this.renderer.positionFloatingElement(this.previewElement, anchor)
      );
    }

    this.options.onRequestPreview({
      type: 'requestMentionPreview',
      requestId,
      sourcePath: currentPath,
      relativePath,
      mode,
      maxCharacters: previewMaxCharacters,
    });
  }

  private hideFloatingPreview() {
    this.previewElement.dataset.visible = 'false';
    this.previewElement.dataset.mode = '';
    this.previewElement.dataset.anchor = '';
    this.previewElement.innerHTML = '';
  }

  private async openMention(relativePath: string) {
    const currentPath = this.options.getCurrentPath();
    if (!currentPath) {
      return;
    }

    this.hidePreview();
    this.options.onOpenMention({
      type: 'openMention',
      sourcePath: currentPath,
      relativePath,
      content: await this.options.exportContent(),
    });
    this.options.showStatus(`Opening ${relativePath}...`);
  }

  private async copyMentionPath(relativePath: string) {
    try {
      await navigator.clipboard.writeText(relativePath);
      this.options.showStatus('Copied linked note path.');
    } catch (error) {
      console.warn('Could not copy linked note path.', error);
      this.options.showStatus('Could not copy linked note path.');
    }
  }

  private makeRequestId() {
    this.requestCounter++;
    return `${Date.now().toString(36)}-${this.requestCounter.toString(36)}`;
  }
}
