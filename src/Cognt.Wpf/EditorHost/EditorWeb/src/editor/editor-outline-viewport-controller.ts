import type { PageEditor } from '@blocksuite/presets';
import type { EditorSelectionController } from './editor-selection-controller';

const outlineViewportAnchorY = 96;

type EditorOutlineViewportControllerOptions = {
  editor: PageEditor;
  selection: EditorSelectionController;
  signal: AbortSignal;
  onCurrentOutlineBlockChanged(blockId: string): void;
};

export class EditorOutlineViewportController {
  private currentBlockId = '';
  private frame = 0;

  public constructor(private readonly options: EditorOutlineViewportControllerOptions) {}

  public bind() {
    const handleViewportChanged = () => this.scheduleRefresh();
    document.addEventListener('scroll', handleViewportChanged, {
      capture: true,
      passive: true,
      signal: this.options.signal,
    });
    window.addEventListener('resize', handleViewportChanged, {
      passive: true,
      signal: this.options.signal,
    });
    this.options.editor.addEventListener('scroll', handleViewportChanged, {
      passive: true,
      signal: this.options.signal,
    });
    this.options.editor.addEventListener('keyup', handleViewportChanged, {
      passive: true,
      signal: this.options.signal,
    });
    this.options.editor.addEventListener('pointerup', handleViewportChanged, {
      passive: true,
      signal: this.options.signal,
    });
    this.options.editor.addEventListener('input', handleViewportChanged, {
      passive: true,
      signal: this.options.signal,
    });
    this.scheduleRefresh();
  }

  public refresh() {
    this.scheduleRefresh();
  }

  private scheduleRefresh() {
    if (this.frame !== 0) {
      return;
    }

    this.frame = window.requestAnimationFrame(() => {
      this.frame = 0;
      this.notifyCurrentHeading();
    });
  }

  private notifyCurrentHeading() {
    const blockId = this.findCurrentHeadingBlockId();
    if (!blockId || blockId === this.currentBlockId) {
      return;
    }

    this.currentBlockId = blockId;
    this.options.onCurrentOutlineBlockChanged(blockId);
  }

  private findCurrentHeadingBlockId() {
    const headingIds = this.options.selection.getOutlineHeadingBlockIds();
    let currentBlockId = '';

    for (const blockId of headingIds) {
      const element = this.options.selection.getRenderedBlockElement(blockId);
      const rect = element?.getBoundingClientRect();
      if (!rect || rect.bottom < 0) {
        continue;
      }

      if (rect.top <= outlineViewportAnchorY) {
        currentBlockId = blockId;
        continue;
      }

      return currentBlockId || blockId;
    }

    return currentBlockId;
  }
}
