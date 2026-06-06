import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { exportDocContent } from '../documents/io';
import { flushEditorHost } from './host-operations';

const statusHideMs = 2600;

type EditorRuntimeCoordinatorOptions = {
  editor: PageEditor;
  statusElement: HTMLElement;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getCurrentCollectionRoot(): string;
  ensureReferencesBlock(): void;
  shouldPatchLinkedDocBlocks(): boolean;
};

export class EditorRuntimeCoordinator {
  private statusTimer: number | undefined;

  public constructor(private readonly options: EditorRuntimeCoordinatorOptions) {}

  public showStatus(message: string) {
    this.options.statusElement.textContent = message;
    this.options.statusElement.dataset.visible = 'true';
    window.clearTimeout(this.statusTimer);
    this.statusTimer = window.setTimeout(() => {
      this.options.statusElement.dataset.visible = 'false';
    }, statusHideMs);
  }

  public async flushHost() {
    await flushEditorHost(this.options.editor, {
      patchLinkedDocBlocks: this.options.shouldPatchLinkedDocBlocks(),
    });
  }

  public async exportCurrentContent() {
    this.options.ensureReferencesBlock();
    return exportDocContent(
      this.options.getCurrentDoc(),
      this.options.getCurrentPath(),
      this.options.getCurrentCollectionRoot()
    );
  }

  public dispose() {
    window.clearTimeout(this.statusTimer);
  }
}
