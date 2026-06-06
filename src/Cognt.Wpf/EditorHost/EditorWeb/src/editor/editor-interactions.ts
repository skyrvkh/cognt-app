import type { EditorCaretState } from './contracts';
import type { TextSelection } from '../cloze';
import {
  clearCachedTextSelection,
  getCaretState,
  restoreCaretState,
  captureNativeTextSelectionForPaste,
} from './editor-interaction-caret';
import {
  focusBlock,
  focusEditor,
} from './editor-interaction-focus';
import {
  activateEditorMode,
  enableEditorMode,
  setReadOnlyMode,
  showLockedInputStatus,
} from './editor-interaction-mode';
import type { EditorInteractionOptions } from './editor-interaction-types';

export class EditorInteractionController {
  private readOnlyMode = false;
  private lockedInputStatusShownAt = 0;
  private lastKnownTextSelectionForPaste: TextSelection | null = null;

  public constructor(private readonly options: EditorInteractionOptions) {}

  public get isLocked() {
    return this.readOnlyMode;
  }

  public clearCachedTextSelection() {
    clearCachedTextSelection(this.createCaretState());
  }

  public async focusEditor() {
    await focusEditor(this.options, () => this.activateEditorMode());
  }

  public async focusBlock(blockId: string) {
    await focusBlock(this.options, this.readOnlyMode, blockId);
  }

  public getCaretState(): EditorCaretState | null {
    return getCaretState(this.options, this.createCaretState());
  }

  public async restoreCaretState(state?: EditorCaretState | null) {
    await restoreCaretState(this.options, this.createCaretState(), state);
  }

  public async activateEditorMode() {
    await activateEditorMode(this.options, this.createModeState());
  }

  public async setReadOnlyMode(readOnly: boolean) {
    await setReadOnlyMode(this.options, this.createModeState(), readOnly);
  }

  public enableEditorMode() {
    enableEditorMode(this.options, this.createModeState());
  }

  public showLockedInputStatus() {
    showLockedInputStatus(this.options, this.createModeState());
  }

  public captureNativeTextSelectionForPaste() {
    captureNativeTextSelectionForPaste(this.options, this.createCaretState());
  }

  private createModeState() {
    return {
      readOnlyMode: this.readOnlyMode,
      lockedInputStatusShownAt: this.lockedInputStatusShownAt,
      setReadOnlyMode: (readOnly: boolean) => {
        this.readOnlyMode = readOnly;
      },
      setLockedInputStatusShownAt: (value: number) => {
        this.lockedInputStatusShownAt = value;
      },
    };
  }

  private createCaretState() {
    return {
      readOnlyMode: this.readOnlyMode,
      lastKnownTextSelectionForPaste: this.lastKnownTextSelectionForPaste,
      setLastKnownTextSelectionForPaste: (selection: TextSelection | null) => {
        this.lastKnownTextSelectionForPaste = selection;
      },
    };
  }
}
