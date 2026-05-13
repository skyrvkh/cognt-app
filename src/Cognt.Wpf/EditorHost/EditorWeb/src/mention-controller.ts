import type { PageEditor } from '@blocksuite/presets';
import { type MentionTarget } from './mentions';
import type {
  MentionControllerOptions,
  NativeInlineEditor,
  NativeLinkedMenuGroup,
} from './mention-controller-types';
import { NativeMentionMenuController } from './mention-controller-native-menu';

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
  private readonly editor: PageEditor;
  private readonly options: MentionControllerOptions;
  private readonly nativeMentionMenu: NativeMentionMenuController;
  private targets: MentionTarget[] = [];

  public constructor(options: MentionControllerOptions) {
    this.options = options;
    this.editor = options.editor;
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
      refreshHighlights: () => this.refreshHighlights(),
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

  public scheduleHighlightUpdate() {}

  public refreshHighlights() {}

  public cancelScheduledHighlightUpdate() {}

  public dispose() {
    this.cancelScheduledHighlightUpdate();
  }
}

