import type { PageEditor } from '@blocksuite/presets';
import { type MentionTarget } from './mentions';
import type {
  MentionControllerOptions,
  NativeInlineEditor,
  NativeLinkedMenuGroup,
} from './mention-controller-types';
import { NativeMentionMenuController } from './mention-controller-native-menu';
import { reportDiagnostic } from './host';

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
    reportDiagnostic('info', `Mention controller created (path=${options.getCurrentPath()}).`);
    this.nativeMentionMenu = new NativeMentionMenuController({
      editor: this.editor,
      getCurrentPath: () => this.options.getCurrentPath(),
      getTargets: () => this.targets,
      setTargets: targets => {
        this.targets = targets;
        reportDiagnostic('info', `Mention controller native targets updated (count=${targets.length}).`);
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
    reportDiagnostic('info', `Mention controller targets set (count=${targets.length}, path=${this.options.getCurrentPath()}).`);
  }

  public getNativeMentionMenus(
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor,
    abortSignal?: AbortSignal
  ): Promise<NativeLinkedMenuGroup[]> {
    reportDiagnostic(
      'info',
      `Mention menu query started (path=${this.options.getCurrentPath()}, queryLength=${query.length}, targets=${this.targets.length}).`
    );
    return this.nativeMentionMenu.getMenus(query, abort, inlineEditor, abortSignal)
      .then(groups => {
        reportDiagnostic(
          'info',
          `Mention menu query completed (queryLength=${query.length}, groups=${groups.length}).`
        );
        return groups;
      })
      .catch(error => {
        reportDiagnostic('error', `Mention menu query failed: ${errorMessage(error)}`);
        throw error;
      });
  }

  public scheduleHighlightUpdate() {
    reportDiagnostic('info', `Mention highlight update scheduled (path=${this.options.getCurrentPath()}).`);
  }

  public refreshHighlights() {
    reportDiagnostic('info', `Mention highlights refreshed (path=${this.options.getCurrentPath()}, targets=${this.targets.length}).`);
  }

  public cancelScheduledHighlightUpdate() {
    reportDiagnostic('info', `Mention highlight update cancelled (path=${this.options.getCurrentPath()}).`);
  }

  public dispose() {
    this.cancelScheduledHighlightUpdate();
    reportDiagnostic('info', `Mention controller disposed (path=${this.options.getCurrentPath()}).`);
  }
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
