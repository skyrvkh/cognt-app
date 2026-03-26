import type { PageEditor } from '@blocksuite/presets';
import { html } from 'lit';
import {
  buildMentionPreviewHtml,
  filterMentionTargets,
  findMentionAtOffset,
  type MentionPreviewResult,
  type MentionTarget,
  type MentionViewMode,
} from './mentions';

const previewDelayMs = 220;
const previewDismissDelayMs = 200;
const previewMaxCharacters = 0;
const highlightName = 'cognt-mention';
const maxNativeMentionItems = 8;
const markdownMentionGroupName = 'Markdown Files';
const markdownMentionIcon = html`<span
  style="display:inline-flex;width:20px;height:20px;align-items:center;justify-content:center;font-weight:700;color:var(--affine-text-secondary-color);"
  >#</span
>`;

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
  markdown: string;
};

export type NativeInlineRange = {
  index: number;
  length: number;
};

export type NativeInlineEditor = {
  getInlineRange(): NativeInlineRange | null;
  insertText(range: NativeInlineRange, text: string): void;
  setInlineRange(range: NativeInlineRange | null): void;
};

type NativeLinkedMenuItem = {
  key: string;
  name: string;
  icon: unknown;
  suffix?: string;
  action: () => void | Promise<void>;
};

export type NativeLinkedMenuGroup = {
  name: string;
  items: NativeLinkedMenuItem[];
  maxDisplay?: number;
  overflowText?: string;
};

type MentionControllerOptions = {
  editor: PageEditor;
  previewElement: HTMLElement;
  getCurrentPath(): string;
  exportMarkdown(): Promise<string>;
  flushHost(): Promise<void>;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
  onRequestPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
};

type Point = {
  x: number;
  y: number;
};

export class MentionController {
  private readonly editor: PageEditor;
  private readonly previewElement: HTMLElement;
  private readonly options: MentionControllerOptions;
  private targets: MentionTarget[] = [];
  private highlightFrame: number | undefined;
  private hoverTimer: number | undefined;
  private previewHideTimer: number | undefined;
  private hoverPath = '';
  private pendingPreviewRequestId = '';
  private activeViewMode: MentionViewMode | null = null;
  private viewAnchor: Point | null = null;
  private requestCounter = 0;

  public constructor(options: MentionControllerOptions) {
    this.options = options;
    this.editor = options.editor;
    this.previewElement = options.previewElement;
    this.previewElement.addEventListener('mouseenter', () => this.cancelScheduledPreviewHide());
    this.previewElement.addEventListener('mouseleave', () => this.schedulePreviewHide());
  }

  public setTargets(targets: MentionTarget[]) {
    this.targets = targets;
  }

  public getNativeMentionMenus(
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor
  ): NativeLinkedMenuGroup[] {
    const targets = filterMentionTargets(this.targets, query);

    if (targets.length === 0) {
      return [];
    }

    return [this.createNativeMentionGroup(targets, abort, inlineEditor)];
  }

  public handlePreviewResult(result: MentionPreviewResult) {
    if (result.requestId !== this.pendingPreviewRequestId) {
      return;
    }

    this.cancelScheduledPreviewHide();
    const mode = result.mode ?? this.activeViewMode ?? 'preview';
    this.activeViewMode = mode;
    this.previewElement.dataset.mode = mode;
    this.previewElement.innerHTML = buildMentionPreviewHtml(result);
    this.previewElement.dataset.visible = 'true';

    if (this.viewAnchor) {
      this.positionFloatingElement(this.previewElement, this.viewAnchor);
    }
  }

  public handleOpenResult(ok: boolean, error?: string) {
    if (!ok) {
      this.options.showStatus(`Open failed: ${error ?? 'Could not open Markdown file.'}`);
    }
  }

  public scheduleHighlightUpdate() {
    if (this.highlightFrame !== undefined) {
      return;
    }

    this.highlightFrame = window.requestAnimationFrame(() => {
      this.highlightFrame = undefined;
      this.updateHighlights();
    });
  }

  public refreshHighlights() {
    this.cancelScheduledHighlightUpdate();
    this.updateHighlights();
  }

  public cancelScheduledHighlightUpdate() {
    if (this.highlightFrame === undefined) {
      return;
    }

    window.cancelAnimationFrame(this.highlightFrame);
    this.highlightFrame = undefined;
  }

  public handleHover(event: MouseEvent) {
    if (!this.options.getCurrentPath() || this.activeViewMode === 'full') {
      return;
    }

    const mention = this.getHitFromPoint(event.clientX, event.clientY);
    if (!mention) {
      this.clearHoverTimer();
      this.hoverPath = '';

      if (this.activeViewMode === 'preview') {
        this.schedulePreviewHide();
      }
      return;
    }

    this.cancelScheduledPreviewHide();

    if (
      mention.relativePath === this.hoverPath &&
      (this.hoverTimer !== undefined || this.activeViewMode === 'preview')
    ) {
      return;
    }

    this.hoverPath = mention.relativePath;
    this.clearHoverTimer();

    const anchor = { x: event.clientX + 12, y: event.clientY + 12 };
    this.hoverTimer = window.setTimeout(() => {
      this.hoverTimer = undefined;
      if (this.hoverPath === mention.relativePath && this.activeViewMode !== 'full') {
        this.requestPreview(mention.relativePath, anchor, 'preview');
      }
    }, previewDelayMs);
  }

  public async handleClick(event: MouseEvent) {
    const currentPath = this.options.getCurrentPath();
    if (!currentPath) {
      return;
    }

    const mention = this.getHitFromPoint(event.clientX, event.clientY);
    if (!mention) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    this.hidePreview();

    this.options.onOpenMention({
      type: 'openMention',
      sourcePath: currentPath,
      relativePath: mention.relativePath,
      markdown: await this.options.exportMarkdown(),
    });
    this.options.showStatus(`Opening ${mention.relativePath}...`);
  }

  public hidePreview() {
    this.pendingPreviewRequestId = '';
    this.activeViewMode = null;
    this.viewAnchor = null;
    this.hoverPath = '';
    this.clearHoverTimer();
    this.cancelScheduledPreviewHide();
    this.previewElement.dataset.visible = 'false';
    this.previewElement.dataset.mode = '';
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
    return this.previewElement.contains(target);
  }

  public isShowingPreview() {
    return this.activeViewMode === 'preview';
  }

  private updateHighlights() {
    const cssWithHighlights = CSS as typeof CSS & {
      highlights?: {
        set(name: string, highlight: unknown): void;
        delete(name: string): void;
      };
    };
    const highlightConstructor = (globalThis as typeof globalThis & {
      Highlight?: new (...ranges: Range[]) => unknown;
    }).Highlight;

    if (!cssWithHighlights.highlights || !highlightConstructor) {
      return;
    }

    const ranges: Range[] = [];
    const expression = /\[\[[^\]\[\n]+\]\]/g;

    for (const current of collectMentionTextNodes(this.editor)) {
      const text = current.textContent ?? '';
      let match: RegExpExecArray | null;

      while ((match = expression.exec(text)) !== null) {
        const range = document.createRange();
        range.setStart(current, match.index);
        range.setEnd(current, match.index + match[0].length);
        ranges.push(range);
      }
    }

    if (ranges.length === 0) {
      cssWithHighlights.highlights.delete(highlightName);
      return;
    }

    cssWithHighlights.highlights.set(highlightName, new highlightConstructor(...ranges));
  }

  private insertNativeMention(inlineEditor: NativeInlineEditor, target: MentionTarget) {
    const range = inlineEditor.getInlineRange();
    if (!range) {
      this.options.showStatus('Could not insert the Markdown file link at the current caret.');
      return;
    }

    const replacementText = `[[${target.relativePath}]]`;

    try {
      inlineEditor.insertText(range, replacementText);
      inlineEditor.setInlineRange({
        index: range.index + replacementText.length,
        length: 0,
      });
    } catch (error) {
      console.warn('Native mention insertion failed.', error);
      this.options.showStatus('Could not insert the Markdown file link at the current caret.');
      return;
    }

    void this.options.flushHost().then(() => {
      this.refreshHighlights();
      this.options.scheduleContentChanged();
    });
  }

  private createNativeMentionGroup(
    targets: MentionTarget[],
    abort: () => void,
    inlineEditor: NativeInlineEditor
  ): NativeLinkedMenuGroup {
    const group: NativeLinkedMenuGroup = {
      name: markdownMentionGroupName,
      items: targets.map(target => this.createNativeMentionItem(target, abort, inlineEditor)),
      maxDisplay: maxNativeMentionItems,
    };

    if (targets.length > maxNativeMentionItems) {
      group.overflowText = `${targets.length - maxNativeMentionItems} more notes`;
    }

    return group;
  }

  private createNativeMentionItem(
    target: MentionTarget,
    abort: () => void,
    inlineEditor: NativeInlineEditor
  ): NativeLinkedMenuItem {
    return {
      key: `markdown:${target.relativePath}`,
      name: target.fileName,
      suffix: target.relativePath === target.fileName ? undefined : target.relativePath,
      icon: markdownMentionIcon,
      action: () => {
        abort();
        this.insertNativeMention(inlineEditor, target);
      },
    };
  }

  private requestPreview(relativePath: string, anchor: Point, mode: MentionViewMode) {
    const currentPath = this.options.getCurrentPath();
    if (!currentPath) {
      return;
    }

    this.cancelScheduledPreviewHide();
    const requestId = this.makeRequestId();
    this.pendingPreviewRequestId = requestId;
    this.activeViewMode = mode;
    this.viewAnchor = anchor;
    this.previewElement.dataset.mode = mode;
    this.previewElement.innerHTML = `<div class="mention-preview-body mention-preview-body--static"><p>${
      mode === 'full' ? 'Opening document...' : 'Loading preview...'
    }</p></div>`;
    this.previewElement.dataset.visible = 'true';
    this.positionFloatingElement(this.previewElement, anchor);

    this.options.onRequestPreview({
      type: 'requestMentionPreview',
      requestId,
      sourcePath: currentPath,
      relativePath,
      mode,
      maxCharacters: previewMaxCharacters,
    });
  }

  private getHitFromPoint(x: number, y: number) {
    const position = getTextPositionFromPoint(x, y);
    if (position && isNodeInside(this.editor, position.node)) {
      const text = position.node.textContent ?? '';
      const mention = findMentionAtOffset(text, position.offset);
      if (mention) {
        return mention;
      }
    }

    const expression = /\[\[([^\]\[\n]+)\]\]/g;
    for (const node of collectMentionTextNodes(this.editor)) {
      const text = node.textContent ?? '';
      let match: RegExpExecArray | null;

      while ((match = expression.exec(text)) !== null) {
        const range = document.createRange();
        range.setStart(node, match.index);
        range.setEnd(node, match.index + match[0].length);

        for (const rect of Array.from(range.getClientRects())) {
          if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
            return {
              relativePath: match[1],
              start: match.index,
              end: match.index + match[0].length,
            };
          }
        }
      }
    }

    return null;
  }

  private makeRequestId() {
    this.requestCounter++;
    return `${Date.now().toString(36)}-${this.requestCounter.toString(36)}`;
  }

  private positionFloatingElement(element: HTMLElement, point: Point) {
    const margin = 12;
    const width = element.offsetWidth || 320;
    const height = element.offsetHeight || 220;
    const left = Math.min(
      Math.max(margin, point.x),
      Math.max(margin, window.innerWidth - width - margin)
    );
    const top = Math.min(
      Math.max(margin, point.y),
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

function collectMentionTextNodes(root: Node) {
  const nodes: Text[] = [];

  const visit = (node: Node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      if ((node.textContent ?? '').includes('[[')) {
        nodes.push(node as Text);
      }
      return;
    }

    if (node.nodeType !== Node.ELEMENT_NODE && node.nodeType !== Node.DOCUMENT_FRAGMENT_NODE) {
      return;
    }

    const element = node as Element;
    if (element.shadowRoot) {
      visit(element.shadowRoot);
    }

    for (const child of Array.from(node.childNodes)) {
      visit(child);
    }
  };

  visit(root);
  return nodes;
}

function getTextPositionFromPoint(x: number, y: number) {
  const documentWithCaretRange = document as Document & {
    caretRangeFromPoint?: (x: number, y: number) => Range | null;
    caretPositionFromPoint?: (x: number, y: number) => { offsetNode: Node; offset: number } | null;
  };

  const range = documentWithCaretRange.caretRangeFromPoint?.(x, y);
  if (range?.startContainer.nodeType === Node.TEXT_NODE) {
    return {
      node: range.startContainer as Text,
      offset: range.startOffset,
    };
  }

  const position = documentWithCaretRange.caretPositionFromPoint?.(x, y);
  if (position?.offsetNode.nodeType === Node.TEXT_NODE) {
    return {
      node: position.offsetNode as Text,
      offset: position.offset,
    };
  }

  return null;
}

function isNodeInside(root: Node, node: Node) {
  let current: Node | null = node;

  while (current) {
    if (current === root) {
      return true;
    }

    const currentRoot = current.getRootNode();
    if (currentRoot instanceof ShadowRoot) {
      current = currentRoot.host;
      continue;
    }

    current = current.parentNode;
  }

  return false;
}
