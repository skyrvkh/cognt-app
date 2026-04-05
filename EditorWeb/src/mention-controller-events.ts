import type { PageEditor } from '@blocksuite/presets';

export type MentionControllerEventHost = {
  editor: PageEditor;
  previewElement: HTMLElement;
  signal: AbortSignal;
  isShowingPreview(): boolean;
  containsFloatingUi(target: Node): boolean;
  schedulePreviewHide(): void;
  cancelScheduledPreviewHide(): void;
  handleNativeMentionMenuMove(event: MouseEvent): void;
};

export function bindMentionControllerEvents(host: MentionControllerEventHost) {
  host.previewElement.addEventListener(
    'mouseenter',
    () => host.cancelScheduledPreviewHide(),
    { signal: host.signal }
  );
  host.previewElement.addEventListener(
    'mouseleave',
    () => host.schedulePreviewHide(),
    { signal: host.signal }
  );
  document.addEventListener(
    'mousemove',
    event => {
      if (!host.isShowingPreview()) {
        return;
      }

      const target = event.target as Node | null;
      if (target && (host.editor.contains(target) || host.containsFloatingUi(target))) {
        return;
      }

      host.schedulePreviewHide();
    },
    { signal: host.signal }
  );
  document.addEventListener(
    'mousemove',
    event => host.handleNativeMentionMenuMove(event),
    { signal: host.signal }
  );
}
