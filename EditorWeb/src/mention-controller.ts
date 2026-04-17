import type { PageEditor } from '@blocksuite/presets';
import {
  escapeHtml,
  type MentionPreviewResult,
  type MentionTarget,
  type MentionViewMode,
} from './mentions';
import { NativeMentionMenuController } from './mention-controller-native-menu';
import { MentionPreviewSession } from './mention-preview-session';

const linkedPagePreviewDelayMs = 560;
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
  private readonly preview: MentionPreviewSession;
  private targets: MentionTarget[] = [];
  private hoverTimer: number | undefined;
  private hoverPath = '';

  public constructor(options: MentionControllerOptions) {
    this.options = options;
    this.editor = options.editor;
    this.previewElement = options.previewElement;
    this.preview = new MentionPreviewSession({
      previewElement: this.previewElement,
      getCurrentPath: () => this.options.getCurrentPath(),
      exportContent: () => this.options.exportContent(),
      showStatus: message => this.options.showStatus(message),
      onRequestPreview: message => this.options.onRequestPreview(message),
      onOpenMention: message => this.options.onOpenMention(message),
    });
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
        this.preview.requestNativeMentionMenuPreview(relativePath, anchor),
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
        if (this.preview.getActiveViewMode() !== 'preview') {
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
    this.preview.handlePreviewResult(result);
  }

  public handleOpenResult(ok: boolean, error?: string) {
    this.preview.handleOpenResult(ok, error);
  }

  public scheduleHighlightUpdate() {}

  public refreshHighlights() {}

  public cancelScheduledHighlightUpdate() {}

  public handleLinkedPageHover(relativePath: string, anchor: FloatingAnchor) {
    if (!this.options.getCurrentPath() || this.preview.getActiveViewMode() === 'full') {
      return;
    }

    this.cancelScheduledPreviewHide();

    const sameNativeAnchor = isElementAnchor(anchor)
      && this.preview.getNativePreviewAnchor() === anchor;
    if (
      relativePath === this.hoverPath &&
      (
        this.hoverTimer !== undefined
        || (this.preview.getActiveViewMode() === 'preview' && sameNativeAnchor)
      )
    ) {
      return;
    }

    this.hoverPath = relativePath;
    this.clearHoverTimer();

    this.hoverTimer = window.setTimeout(() => {
      this.hoverTimer = undefined;
      if (
        this.hoverPath === relativePath
        && this.preview.getActiveViewMode() !== 'full'
      ) {
        this.preview.requestPreview(relativePath, anchor, 'preview');
      }
    }, linkedPagePreviewDelayMs);
  }

  public handleHoverMiss() {
    this.clearHoverTimer();
    this.hoverPath = '';

    if (this.preview.getActiveViewMode() === 'preview') {
      this.schedulePreviewHide();
    }
  }

  public async handleClick(_event: MouseEvent) {}

  public hidePreview() {
    this.hoverPath = '';
    this.clearHoverTimer();
    this.preview.hidePreview();
  }

  public schedulePreviewHide() {
    this.clearHoverTimer();
    this.preview.schedulePreviewHide(() => {
      this.hoverPath = '';
      this.clearHoverTimer();
    });
  }

  public cancelScheduledPreviewHide() {
    this.preview.cancelScheduledPreviewHide();
  }

  public containsFloatingUi(target: Node) {
    return this.previewElement.contains(target)
      || isReferencePopupTarget(target)
      || isLinkedDocPopoverTarget(target);
  }

  public isShowingPreview() {
    return this.preview.isShowingPreview();
  }

  public dispose() {
    this.hidePreview();
    this.cancelScheduledHighlightUpdate();
    this.eventAbort.abort();
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

