import { reportDiagnostic } from '../host';
import type {
  EditorHostCallbackOptions,
  EditorHostCallbacks,
} from './host-callback-types';

export function createDocumentHostCallbacks({
  dotNet,
  instanceId,
  invokeHost,
}: EditorHostCallbackOptions): Pick<
  EditorHostCallbacks,
  | 'onDocumentChanged'
  | 'onContentChanged'
  | 'onCurrentOutlineBlockChanged'
  | 'onCreateExtract'
  | 'onSaveCloze'
  | 'onOpenImageOcclusion'
> {
  return {
    onDocumentChanged: () => {
      reportDiagnostic('info', `Bridge event: document changed (instance=${instanceId}).`);
      void invokeHost(dotNet, 'HandleDocumentChanged');
    },
    onContentChanged: content => {
      reportDiagnostic(
        'info',
        `Bridge event: content changed (instance=${instanceId}, length=${content.length}).`
      );
      void invokeHost(dotNet, 'HandleContentChanged', content);
    },
    onCurrentOutlineBlockChanged: blockId => {
      void invokeHost(dotNet, 'HandleCurrentOutlineBlockChanged', blockId);
    },
    onCreateExtract: message => {
      reportDiagnostic(
        'info',
        `Bridge event: create extract (instance=${instanceId}, selectedText=${message.selectedPlainText.length}, selectedContent=${message.selectedContent.length}).`
      );
      void invokeHost(
        dotNet,
        'HandleCreateExtract',
        message.sourcePath,
        message.selectedContent,
        message.selectedPlainText
      );
    },
    onSaveCloze: message => {
      reportDiagnostic(
        'info',
        `Bridge event: save cloze (instance=${instanceId}, block=${message.blockId}, text=${message.selectedPlainText.length}, content=${message.content.length}).`
      );
      void invokeHost(
        dotNet,
        'HandleSaveCloze',
        message.sourcePath,
        message.blockId,
        message.selectedPlainText,
        message.clozeText,
        message.content,
        message.cardContextId ?? null,
        message.slotId ?? null,
        message.displayClozeIndex ?? null
      );
    },
    onOpenImageOcclusion: message => {
      reportDiagnostic(
        'info',
        `Bridge event: open image occlusion (instance=${instanceId}, block=${message.imageBlockId}, image=${message.imageRelPath}, content=${message.content.length}).`
      );
      void invokeHost(
        dotNet,
        'HandleOpenImageOcclusion',
        message.sourcePath,
        message.imageBlockId,
        message.imageRelPath,
        message.content
      );
    },
  };
}
