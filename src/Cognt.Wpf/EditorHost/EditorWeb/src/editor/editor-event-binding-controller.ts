import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { PendingExtract } from '../commands';
import type { LinkedPageReference } from '../links/linked-notes';
import { bindGlobalEditorEvents } from './editor-event-bind-global';
import { bindEditorSurfaceEvents } from './editor-event-bind-surface';
import type { EditorEventBindingOptions } from './editor-event-types';

type EditorEventBindingControllerOptions = {
  editor: PageEditor;
  signal: AbortSignal;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  isInputLocked(): boolean;
  showLockedInputStatus(): void;
  hasDroppedFiles(event: DragEvent): boolean;
  handleFileDragOver(event: DragEvent): void;
  handleDroppedFiles(event: DragEvent): Promise<void>;
  cancelNativeFileEvent(event: Event): void;
  captureNativeTextSelectionForPaste(): void;
  scheduleMentionHighlightUpdate(): void;
  recordUserEdit(): void;
  isLargeDocument(): boolean;
  scheduleDocumentChangedNotification(): void;
  scheduleContentChanged(): void;
  consumeSuppressedNativeCut(): boolean;
  openLinkedDocReference(reference: LinkedPageReference): Promise<void>;
  getSelectedLinkedDocModelForOpenIntent(event: MouseEvent): unknown;
  openLinkedDocFromModel(model: unknown): Promise<void>;
  handleCreateExtractShortcut(
    event: KeyboardEvent,
    sourceAction: PendingExtract['sourceAction']
  ): Promise<void>;
  handleClozeCommand(event: KeyboardEvent): Promise<void>;
  handleImageOcclusionShortcut(event: KeyboardEvent): Promise<void>;
};

export class EditorEventBindingController {
  public constructor(private readonly options: EditorEventBindingControllerOptions) {}

  public bind() {
    const options: EditorEventBindingOptions = {
      editor: this.options.editor,
      signal: this.options.signal,
      getCurrentDoc: () => this.options.getCurrentDoc(),
      getCurrentPath: () => this.options.getCurrentPath(),
      isInputLocked: () => this.options.isInputLocked(),
      showLockedInputStatus: () => this.options.showLockedInputStatus(),
      hasDroppedFiles: event => this.options.hasDroppedFiles(event),
      handleFileDragOver: event => this.options.handleFileDragOver(event),
      handleDroppedFiles: event => this.options.handleDroppedFiles(event),
      isEditorEvent: event => this.isEditorEvent(event),
      cancelNativeFileEvent: event => this.options.cancelNativeFileEvent(event),
      captureNativeTextSelectionForPaste: () => this.options.captureNativeTextSelectionForPaste(),
      scheduleMentionHighlightUpdate: () => this.options.scheduleMentionHighlightUpdate(),
      recordUserEdit: () => this.options.recordUserEdit(),
      isLargeDocument: () => this.options.isLargeDocument(),
      scheduleDocumentChangedNotification: () => this.options.scheduleDocumentChangedNotification(),
      scheduleContentChanged: () => this.options.scheduleContentChanged(),
      consumeSuppressedNativeCut: () => this.options.consumeSuppressedNativeCut(),
      openLinkedDocReference: reference => this.options.openLinkedDocReference(reference),
      getSelectedLinkedDocModelForOpenIntent: event =>
        this.options.getSelectedLinkedDocModelForOpenIntent(event),
      openLinkedDocFromModel: model => this.options.openLinkedDocFromModel(model),
      handleCreateExtractShortcut: (event, sourceAction) =>
        this.options.handleCreateExtractShortcut(event, sourceAction),
      handleClozeCommand: event => this.options.handleClozeCommand(event),
      handleImageOcclusionShortcut: event => this.options.handleImageOcclusionShortcut(event),
    };
    bindGlobalEditorEvents(options);
    bindEditorSurfaceEvents(options);
  }

  private isEditorEvent(event: Event) {
    return event.composedPath().includes(this.options.editor);
  }
}
