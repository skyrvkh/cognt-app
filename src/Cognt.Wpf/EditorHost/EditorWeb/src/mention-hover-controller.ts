import type { MentionViewMode } from './mentions';
import {
  isElementAnchor,
  type FloatingAnchor,
} from './mention-controller-anchors';

const linkedPagePreviewDelayMs = 560;

export type MentionHoverControllerOptions = {
  getCurrentPath(): string;
  getActiveViewMode(): MentionViewMode | null;
  getNativePreviewAnchor(): HTMLElement | null;
  cancelScheduledPreviewHide(): void;
  schedulePreviewHide(): void;
  requestPreview(relativePath: string, anchor: FloatingAnchor, mode: MentionViewMode): void;
};

export class MentionHoverController {
  private hoverTimer: number | undefined;
  private hoverPath = '';

  public constructor(private readonly options: MentionHoverControllerOptions) {}

  public handleLinkedPageHover(relativePath: string, anchor: FloatingAnchor) {
    if (!this.options.getCurrentPath() || this.options.getActiveViewMode() === 'full') {
      return;
    }

    this.options.cancelScheduledPreviewHide();

    const sameNativeAnchor = isElementAnchor(anchor)
      && this.options.getNativePreviewAnchor() === anchor;
    if (
      relativePath === this.hoverPath &&
      (
        this.hoverTimer !== undefined ||
        (this.options.getActiveViewMode() === 'preview' && sameNativeAnchor)
      )
    ) {
      return;
    }

    this.hoverPath = relativePath;
    this.clear();

    this.hoverTimer = window.setTimeout(() => {
      this.hoverTimer = undefined;
      if (this.hoverPath === relativePath && this.options.getActiveViewMode() !== 'full') {
        this.options.requestPreview(relativePath, anchor, 'preview');
      }
    }, linkedPagePreviewDelayMs);
  }

  public handleHoverMiss() {
    this.clear();
    this.hoverPath = '';

    if (this.options.getActiveViewMode() === 'preview') {
      this.options.schedulePreviewHide();
    }
  }

  public reset() {
    this.hoverPath = '';
    this.clear();
  }

  public clear() {
    if (!this.hoverTimer) {
      return;
    }

    window.clearTimeout(this.hoverTimer);
    this.hoverTimer = undefined;
  }
}
