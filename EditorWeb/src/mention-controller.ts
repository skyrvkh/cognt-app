import type { PageEditor } from '@blocksuite/presets';
import {
  buildMentionPreviewHtml,
  escapeHtml,
  type MentionPreviewResult,
  type MentionTarget,
  type MentionViewMode,
} from './mentions';
import { NativeMentionMenuController } from './mention-controller-native-menu';

const previewDelayMs = 220;
const linkedPagePreviewDelayMs = 560;
const previewDismissDelayMs = 200;
const previewMaxCharacters = 0;
const nativeReferencePreviewId = 'cognt-reference-preview';
const nativeReferencePreviewStyleId = 'cognt-reference-preview-style';
const nativeMentionSearchPreviewId = 'cognt-linked-doc-search-preview';
const nativeMentionSearchPreviewStyleId = 'cognt-linked-doc-search-preview-style';
export type RequestMentionPreviewMessage = {
  type: 'requestMentionPreview';
  requestId: string;
  sourcePath: string;
  relativePath: string;
  mode: MentionViewMode;
  maxCharacters: number;
};

export type OpenMentionMessage = {
  type: 'openMention';
  sourcePath: string;
  relativePath: string;
  content: string;
};

export type SearchMentionTargetsMessage = {
  type: 'searchMentionTargets';
  sourcePath: string;
  query: string;
  limit: number;
};

export type NativeInlineRange = {
  index: number;
  length: number;
};

export type NativeInlineEditor = {
  getInlineRange(): NativeInlineRange | null;
  insertText(range: NativeInlineRange, text: string, attributes?: unknown): void;
  setInlineRange(range: NativeInlineRange | null): void;
};

export type NativeLinkedMenuGroup = {
  name: string;
  items: Array<{
    key: string;
    name: string;
    icon: unknown;
    suffix?: string;
    action: () => void | Promise<void>;
  }>;
  maxDisplay?: number;
  overflowText?: string;
};

type MentionControllerOptions = {
  editor: PageEditor;
  previewElement: HTMLElement;
  getCurrentPath(): string;
  exportContent(): Promise<string>;
  flushHost(): Promise<void>;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
  onSearchTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  onRequestPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
};

type Point = {
  x: number;
  y: number;
};

type FloatingAnchor = Point | HTMLElement;

export class MentionController {
  private readonly eventAbort = new AbortController();
  private readonly editor: PageEditor;
  private readonly previewElement: HTMLElement;
  private readonly options: MentionControllerOptions;
  private readonly nativeMentionMenu: NativeMentionMenuController;
  private targets: MentionTarget[] = [];
  private hoverTimer: number | undefined;
  private previewHideTimer: number | undefined;
  private hoverPath = '';
  private pendingPreviewRequestId = '';
  private activeViewMode: MentionViewMode | null = null;
  private viewAnchor: FloatingAnchor | null = null;
  private nativePreviewAnchor: HTMLElement | null = null;
  private nativeMentionMenuPreviewAnchor: HTMLElement | null = null;
  private nativeMentionMenuPreviewPath = '';
  private requestCounter = 0;

  public constructor(options: MentionControllerOptions) {
    this.options = options;
    this.editor = options.editor;
    this.previewElement = options.previewElement;
    this.nativeMentionMenu = new NativeMentionMenuController({
      editor: this.editor,
      getCurrentPath: () => this.options.getCurrentPath(),
      getTargets: () => this.targets,
      setTargets: targets => {
        this.targets = targets;
      },
      flushHost: () => this.options.flushHost(),
      scheduleContentChanged: () => this.options.scheduleContentChanged(),
      showStatus: message => this.options.showStatus(message),
      onSearchTargets: message => this.options.onSearchTargets(message),
      requestPreview: (relativePath, anchor) =>
        this.requestNativeMentionMenuPreview(relativePath, anchor),
      refreshHighlights: () => this.refreshHighlights(),
    });
    this.previewElement.addEventListener(
      'mouseenter',
      () => this.cancelScheduledPreviewHide(),
      { signal: this.eventAbort.signal }
    );
    this.previewElement.addEventListener(
      'mouseleave',
      () => this.schedulePreviewHide(),
      { signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'mousemove',
      event => {
        if (this.activeViewMode !== 'preview') {
          return;
        }

        const target = event.target as Node | null;
        if (target && (this.editor.contains(target) || this.containsFloatingUi(target))) {
          return;
        }

        this.schedulePreviewHide();
      },
      { signal: this.eventAbort.signal }
    );
    document.addEventListener(
      'mousemove',
      event => this.nativeMentionMenu.handleMouseMove(event),
      { signal: this.eventAbort.signal }
    );
  }

  public setTargets(targets: MentionTarget[]) {
    this.targets = targets;
  }

  public getNativeMentionMenus(
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor,
    abortSignal?: AbortSignal
  ): Promise<NativeLinkedMenuGroup[]> {
    return this.nativeMentionMenu.getMenus(query, abort, inlineEditor, abortSignal);
  }

  public handlePreviewResult(result: MentionPreviewResult) {
    if (result.requestId !== this.pendingPreviewRequestId) {
      return;
    }

    this.cancelScheduledPreviewHide();
    const mode = result.mode ?? this.activeViewMode ?? 'preview';
    this.activeViewMode = mode;

    if (this.nativePreviewAnchor && mode === 'preview') {
      this.renderNativeReferencePreview(
        this.nativePreviewAnchor,
        result.relativePath,
        buildMentionPreviewHtml(result)
      );
      return;
    }

    if (this.nativeMentionMenuPreviewAnchor && mode === 'preview') {
      this.renderNativeMentionSearchPreview(
        this.nativeMentionMenuPreviewAnchor,
        buildMentionPreviewHtml(result)
      );
      return;
    }

    this.previewElement.dataset.mode = mode;
    this.previewElement.innerHTML = buildMentionPreviewHtml(result);
    this.previewElement.dataset.visible = 'true';

    if (this.viewAnchor) {
      this.positionFloatingElement(this.previewElement, this.viewAnchor);
    }
  }

  public handleOpenResult(ok: boolean, error?: string) {
    if (!ok) {
      this.options.showStatus(`Open failed: ${error ?? 'Could not open note.'}`);
    }
  }

  public scheduleHighlightUpdate() {}

  public refreshHighlights() {}

  public cancelScheduledHighlightUpdate() {}

  public handleLinkedPageHover(relativePath: string, anchor: FloatingAnchor) {
    if (!this.options.getCurrentPath() || this.activeViewMode === 'full') {
      return;
    }

    this.cancelScheduledPreviewHide();

    const sameNativeAnchor = isElementAnchor(anchor) && this.nativePreviewAnchor === anchor;
    if (
      relativePath === this.hoverPath &&
      (this.hoverTimer !== undefined || (this.activeViewMode === 'preview' && sameNativeAnchor))
    ) {
      return;
    }

    this.hoverPath = relativePath;
    this.clearHoverTimer();

    this.hoverTimer = window.setTimeout(() => {
      this.hoverTimer = undefined;
      if (this.hoverPath === relativePath && this.activeViewMode !== 'full') {
        this.requestPreview(relativePath, anchor, 'preview');
      }
    }, linkedPagePreviewDelayMs);
  }

  public handleHoverMiss() {
    this.clearHoverTimer();
    this.hoverPath = '';

    if (this.activeViewMode === 'preview') {
      this.schedulePreviewHide();
    }
  }

  public async handleClick(_event: MouseEvent) {}

  public hidePreview() {
    this.pendingPreviewRequestId = '';
    this.activeViewMode = null;
    this.viewAnchor = null;
    this.nativePreviewAnchor = null;
    this.nativeMentionMenuPreviewAnchor = null;
    this.nativeMentionMenuPreviewPath = '';
    this.hoverPath = '';
    this.clearHoverTimer();
    this.cancelScheduledPreviewHide();
    removeNativeReferencePreview();
    removeNativeMentionSearchPreview();
    this.previewElement.dataset.visible = 'false';
    this.previewElement.dataset.mode = '';
    this.previewElement.dataset.anchor = '';
    this.previewElement.innerHTML = '';
  }

  public schedulePreviewHide() {
    if (this.activeViewMode !== 'preview' || this.previewHideTimer !== undefined) {
      return;
    }

    this.clearHoverTimer();
    this.previewHideTimer = window.setTimeout(() => {
      this.previewHideTimer = undefined;

      if (this.activeViewMode === 'preview') {
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

  public containsFloatingUi(target: Node) {
    return this.previewElement.contains(target)
      || isReferencePopupTarget(target)
      || isLinkedDocPopoverTarget(target);
  }

  public isShowingPreview() {
    return this.activeViewMode === 'preview';
  }

  public dispose() {
    this.hidePreview();
    this.cancelScheduledHighlightUpdate();
    this.eventAbort.abort();
  }

  private requestNativeMentionMenuPreview(relativePath: string, anchor: HTMLElement) {
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

  private requestPreview(relativePath: string, anchor: FloatingAnchor, mode: MentionViewMode) {
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
      this.previewElement.dataset.visible = 'false';
      this.previewElement.dataset.mode = '';
      this.previewElement.dataset.anchor = '';
      this.previewElement.innerHTML = '';
      this.renderNativeMentionSearchPreview(
        anchor,
        `<div class="mention-preview-body mention-preview-body--static"><p>Loading preview...</p></div>`
      );
    } else if (mode === 'preview' && isElementAnchor(anchor)) {
      this.nativePreviewAnchor = anchor;
      this.nativeMentionMenuPreviewAnchor = null;
      this.nativeMentionMenuPreviewPath = '';
      this.previewElement.dataset.visible = 'false';
      this.previewElement.dataset.mode = '';
      this.previewElement.dataset.anchor = '';
      this.previewElement.innerHTML = '';
      this.renderNativeReferencePreview(
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
      this.positionFloatingElement(this.previewElement, anchor);
      window.requestAnimationFrame(() => this.positionFloatingElement(this.previewElement, anchor));
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

  private renderNativeMentionSearchPreview(anchor: HTMLElement, htmlContent: string, attempt = 0) {
    const container = getLinkedDocPopoverContainer(anchor);
    if (!container) {
      if (attempt < 8) {
        window.requestAnimationFrame(() => {
          if (
            this.nativeMentionMenuPreviewAnchor === anchor
            && this.activeViewMode === 'preview'
          ) {
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

  private renderNativeReferencePreview(
    anchor: HTMLElement,
    relativePath: string,
    htmlContent: string,
    attempt = 0
  ) {
    const container = getReferencePopoverContainer(anchor.getBoundingClientRect());
    if (!container) {
      if (attempt < 16) {
        window.requestAnimationFrame(() => {
          if (this.nativePreviewAnchor === anchor && this.activeViewMode === 'preview') {
            this.renderNativeReferencePreview(anchor, relativePath, htmlContent, attempt + 1);
          }
        });
      } else if (this.nativePreviewAnchor === anchor && this.activeViewMode === 'preview') {
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

  private renderFloatingReferencePreview(anchor: HTMLElement, htmlContent: string) {
    this.previewElement.dataset.mode = 'preview';
    this.previewElement.dataset.anchor = 'linked-page';
    this.previewElement.innerHTML = htmlContent;
    this.previewElement.dataset.visible = 'true';
    this.positionFloatingElement(this.previewElement, anchor);
    window.requestAnimationFrame(() => {
      if (this.nativePreviewAnchor === anchor && this.activeViewMode === 'preview') {
        this.positionFloatingElement(this.previewElement, anchor);
      }
    });
  }

  private bindNativeReferenceExtension(preview: HTMLElement, relativePath: string) {
    preview.querySelector<HTMLElement>('[data-cognt-reference-action="open"]')
      ?.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        void this.openMention(relativePath);
      });

    preview.querySelector<HTMLElement>('[data-cognt-reference-action="copy-path"]')
      ?.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        void this.copyMentionPath(relativePath);
      });
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

  private positionFloatingElement(element: HTMLElement, anchor: FloatingAnchor) {
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

  private clearHoverTimer() {
    if (!this.hoverTimer) {
      return;
    }

    window.clearTimeout(this.hoverTimer);
    this.hoverTimer = undefined;
  }
}

function isNativeMentionMenuItem(element: HTMLElement) {
  return Boolean(getNativeMentionMenuItemPath(element));
}

function getNativeMentionMenuItemPath(element: HTMLElement) {
  const key = element.dataset.id;
  return key?.startsWith('note:') ? key.slice('note:'.length) : null;
}

function getLinkedDocPopoverContainer(anchor: HTMLElement) {
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

function escapeCssIdentifier(value: string) {
  return typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, match => `\\${match}`);
}

function isElementAnchor(anchor: FloatingAnchor): anchor is HTMLElement {
  return anchor instanceof HTMLElement;
}

function getFloatingAnchorPoint(anchor: FloatingAnchor): Point {
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

function getReferencePopoverRect(referenceRect: DOMRect) {
  return getReferencePopoverContainer(referenceRect)?.getBoundingClientRect() ?? null;
}

function getReferencePopoverContainer(referenceRect: DOMRect) {
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

function getReferencePopoverContainers() {
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

function buildNativeReferenceExtensionHtml(relativePath: string, previewHtml: string) {
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

function injectNativeReferencePreviewStyle(root: ShadowRoot) {
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

function injectNativeMentionSearchPreviewStyle(root: ShadowRoot) {
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

function removeNativeReferencePreview() {
  for (const container of getReferencePopoverContainers()) {
    container.querySelector(`#${nativeReferencePreviewId}`)?.remove();
  }
}

function removeNativeMentionSearchPreview() {
  for (const element of Array.from(document.querySelectorAll<HTMLElement>('affine-linked-doc-popover'))) {
    element.shadowRoot?.getElementById(nativeMentionSearchPreviewId)?.remove();
  }
}

function isReferencePopupTarget(target: Node) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.shadowRoot?.querySelector('.affine-reference-popover-container')
      ?? target.closest('reference-popup')
  );
}

function isLinkedDocPopoverTarget(target: Node) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.shadowRoot?.querySelector('.linked-doc-popover')
      ?? target.closest('affine-linked-doc-popover')
  );
}

