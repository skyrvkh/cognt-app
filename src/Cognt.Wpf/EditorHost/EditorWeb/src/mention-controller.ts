import type { PageEditor } from '@blocksuite/presets';
import {
  type MentionPreviewResult,
  type MentionTarget,
} from './mentions';
import type {
  MentionControllerOptions,
  NativeInlineEditor,
  NativeLinkedMenuGroup,
} from './mention-controller-types';
import { type FloatingAnchor } from './mention-controller-anchors';
import {
  isLinkedDocPopoverTarget,
  isReferencePopupTarget,
} from './mention-controller-native-preview';
import { NativeMentionMenuController } from './mention-controller-native-menu';
import { MentionHoverController } from './mention-hover-controller';
import { bindMentionControllerEvents } from './mention-controller-events';
import { MentionPreviewSession } from './mention-preview-session';

export type {
  MentionControllerOptions,
  NativeInlineEditor,
  NativeInlineRange,
  NativeLinkedMenuGroup,
  OpenMentionMessage,
  RequestMentionPreviewMessage,
  SearchMentionTargetsMessage,
} from './mention-controller-types';

export class MentionController {
  private readonly eventAbort = new AbortController();
  private readonly editor: PageEditor;
  private readonly previewElement: HTMLElement;
  private readonly options: MentionControllerOptions;
  private readonly nativeMentionMenu: NativeMentionMenuController;
  private readonly preview: MentionPreviewSession;
  private readonly hover: MentionHoverController;
  private targets: MentionTarget[] = [];

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
    this.hover = new MentionHoverController({
      getCurrentPath: () => this.options.getCurrentPath(),
      getActiveViewMode: () => this.preview.getActiveViewMode(),
      getNativePreviewAnchor: () => this.preview.getNativePreviewAnchor(),
      cancelScheduledPreviewHide: () => this.cancelScheduledPreviewHide(),
      schedulePreviewHide: () => this.schedulePreviewHide(),
      requestPreview: (relativePath, anchor, mode) =>
        this.preview.requestPreview(relativePath, anchor, mode),
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
    bindMentionControllerEvents({
      editor: this.editor,
      previewElement: this.previewElement,
      signal: this.eventAbort.signal,
      isShowingPreview: () => this.isShowingPreview(),
      containsFloatingUi: target => this.containsFloatingUi(target),
      schedulePreviewHide: () => this.schedulePreviewHide(),
      cancelScheduledPreviewHide: () => this.cancelScheduledPreviewHide(),
      handleNativeMentionMenuMove: event => this.nativeMentionMenu.handleMouseMove(event),
    });
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
    this.hover.handleLinkedPageHover(relativePath, anchor);
  }

  public handleHoverMiss() {
    this.hover.handleHoverMiss();
  }

  public async handleClick(_event: MouseEvent) {}

  public hidePreview() {
    this.hover.reset();
    this.preview.hidePreview();
  }

  public schedulePreviewHide() {
    this.hover.clear();
    this.preview.schedulePreviewHide(() => this.hover.reset());
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

}

