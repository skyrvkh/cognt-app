import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import type { LinkedNoteViewMode } from './contracts';
import {
  createClozeFromSelection,
  handleClozeCommand,
} from './editor-command-cloze';
import {
  handleImageOcclusionShortcut,
  insertImageFromSlashMenu,
} from './editor-command-image';
import { createLinkedNoteFromSlashMenu } from './editor-command-linked-note';
import type {
  ClozeCommandRequest,
  EditorCommandControllerOptions,
} from './editor-command-types';

export type { ClozeCommandRequest } from './editor-command-types';

export class EditorCommandController {
  public constructor(private readonly options: EditorCommandControllerOptions) {}

  public async handleImageOcclusionShortcut(event: KeyboardEvent) {
    await handleImageOcclusionShortcut(this.options, event);
  }

  public async createClozeFromSelection(options: ClozeCommandRequest = {}) {
    await createClozeFromSelection(this.options, options);
  }

  public async handleClozeCommand(
    event?: KeyboardEvent,
    commandOptions: ClozeCommandRequest = {}
  ) {
    await handleClozeCommand(this.options, event, commandOptions);
  }

  public async createLinkedNoteFromSlashMenu(
    viewMode: LinkedNoteViewMode,
    context: AffineSlashMenuContext
  ) {
    await createLinkedNoteFromSlashMenu(this.options, viewMode, context);
  }

  public async insertImageFromSlashMenu(context: AffineSlashMenuContext) {
    await insertImageFromSlashMenu(this.options, context);
  }
}
