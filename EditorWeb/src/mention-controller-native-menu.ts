import { REFERENCE_NODE } from '@blocksuite/affine-components/rich-text';
import { createDefaultDoc } from '@blocksuite/blocks';
import type { PageEditor } from '@blocksuite/presets';
import { html } from 'lit';
import {
  filterMentionTargets,
  type MentionTarget,
} from './mentions';
import {
  findNativeMentionMenuItem,
  getNativeMentionMenuItemFromEvent,
  getNativeMentionMenuItemPath,
} from './mention-controller-anchors';
import { removeNativeMentionSearchPreview } from './mention-controller-native-preview';
import {
  getMentionDisplayTitle,
  getMentionMenuSuffix,
  normalizeMentionPathKey,
} from './mention-controller-targets';
import { delayWithAbort } from './mention-controller-timing';
import type {
  NativeInlineEditor,
  NativeLinkedMenuGroup,
  SearchMentionTargetsMessage,
} from './mention-controller-types';

const mentionSearchDebounceMs = 500;
const maxNativeMentionItems = 8;
const nativeMentionSearchLimit = 32;
const noteMentionGroupName = 'Notes';
const cogntLinkedNotePathProp = 'cogntLinkedNotePath';
const noteMentionIcon = html`<span
  style="display:inline-flex;width:20px;height:20px;align-items:center;justify-content:center;font-weight:700;color:var(--affine-text-secondary-color);"
  >#</span
>`;

export type NativeMentionMenuControllerOptions = {
  editor: PageEditor;
  getCurrentPath(): string;
  getTargets(): MentionTarget[];
  setTargets(targets: MentionTarget[]): void;
  flushHost(): Promise<void>;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
  onSearchTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  requestPreview(relativePath: string, anchor: HTMLElement): void;
  refreshHighlights(): void;
};

export class NativeMentionMenuController {
  private searchCounter = 0;

  public constructor(private readonly options: NativeMentionMenuControllerOptions) {}

  public async getMenus(
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor,
    abortSignal?: AbortSignal
  ) {
    const targets = await this.searchMentionTargets(query, abortSignal);
    this.scheduleFirstPreview(targets, abortSignal);

    return targets.length === 0
      ? []
      : [this.createMentionGroup(targets, abort, inlineEditor)];
  }

  public handleMouseMove(event: MouseEvent) {
    const target = getNativeMentionMenuItemFromEvent(event);
    if (!target) {
      return;
    }

    const relativePath = getNativeMentionMenuItemPath(target);
    if (!relativePath) {
      return;
    }

    this.options.requestPreview(relativePath, target);
  }

  private insertMention(inlineEditor: NativeInlineEditor, target: MentionTarget) {
    const range = inlineEditor.getInlineRange();
    if (!range) {
      this.options.showStatus('Could not insert the note link at the current caret.');
      return;
    }

    const title = getMentionDisplayTitle(target);
    const linkedDoc = createDefaultDoc(this.options.editor.doc.collection, { title });
    const reference = {
      type: 'LinkedPage',
      pageId: linkedDoc.id,
      params: {
        [cogntLinkedNotePathProp]: target.relativePath,
      },
      title,
      description: target.relativePath,
      [cogntLinkedNotePathProp]: target.relativePath,
    };

    try {
      inlineEditor.insertText(range, REFERENCE_NODE, {
        reference,
      } as never);
      inlineEditor.setInlineRange({
        index: range.index + REFERENCE_NODE.length,
        length: 0,
      });
    } catch (error) {
      console.warn('Native mention insertion failed.', error);
      this.options.showStatus('Could not insert the note link at the current caret.');
      return;
    }

    void this.options.flushHost().then(() => {
      this.options.refreshHighlights();
      this.options.scheduleContentChanged();
    });
  }

  private createMentionGroup(
    targets: MentionTarget[],
    abort: () => void,
    inlineEditor: NativeInlineEditor
  ): NativeLinkedMenuGroup {
    const group: NativeLinkedMenuGroup = {
      name: noteMentionGroupName,
      items: targets.map(target => ({
        key: `note:${target.relativePath}`,
        name: getMentionDisplayTitle(target),
        suffix: getMentionMenuSuffix(target),
        icon: noteMentionIcon,
        action: () => {
          abort();
          this.insertMention(inlineEditor, target);
        },
      })),
      maxDisplay: maxNativeMentionItems,
    };

    if (targets.length > maxNativeMentionItems) {
      group.overflowText = `${targets.length - maxNativeMentionItems} more notes`;
    }

    return group;
  }

  private async searchMentionTargets(query: string, abortSignal?: AbortSignal) {
    const currentPath = this.options.getCurrentPath();
    if (!currentPath) {
      return [];
    }

    const signal = abortSignal ?? new AbortController().signal;
    const searchId = ++this.searchCounter;

    try {
      await delayWithAbort(mentionSearchDebounceMs, signal);

      if (signal.aborted || searchId !== this.searchCounter) {
        return [];
      }

      const targets = await this.options.onSearchTargets({
        type: 'searchMentionTargets',
        sourcePath: currentPath,
        query,
        limit: nativeMentionSearchLimit,
      });

      if (signal.aborted || searchId !== this.searchCounter) {
        return [];
      }

      this.mergeTargets(targets);
      return targets;
    } catch (error) {
      if (signal.aborted) {
        return [];
      }

      console.warn('Mention search failed.', error);
      return filterMentionTargets(
        this.options.getTargets(),
        query,
        nativeMentionSearchLimit
      );
    }
  }

  private scheduleFirstPreview(targets: MentionTarget[], abortSignal?: AbortSignal) {
    const firstTarget = targets[0];
    if (!firstTarget || abortSignal?.aborted) {
      removeNativeMentionSearchPreview();
      return;
    }

    window.setTimeout(() => {
      if (abortSignal?.aborted) {
        return;
      }

      const anchor = findNativeMentionMenuItem(firstTarget.relativePath);
      if (anchor) {
        this.options.requestPreview(firstTarget.relativePath, anchor);
      }
    }, 0);
  }

  private mergeTargets(targets: MentionTarget[]) {
    if (targets.length === 0) {
      return;
    }

    const merged = new Map(this.options.getTargets().map(target => [
      normalizeMentionPathKey(target.relativePath),
      target,
    ]));
    for (const target of targets) {
      merged.set(normalizeMentionPathKey(target.relativePath), target);
    }

    this.options.setTargets([...merged.values()]);
  }
}
