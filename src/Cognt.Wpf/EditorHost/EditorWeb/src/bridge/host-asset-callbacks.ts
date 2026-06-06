import {
  normalizeAttachFileResult,
  normalizeEditorBlobListResult,
  normalizeEditorBlobReadResult,
  normalizeEditorBlobResult,
  normalizeReadAttachmentResult,
} from './host-callback-normalizers';
import type {
  EditorHostCallbackOptions,
  EditorHostCallbacks,
} from './host-callback-types';
import { invokeLoggedHostRequest } from './host-request-logging';

export function createAssetHostCallbacks({
  dotNet,
  instanceId,
  invokeHost,
}: EditorHostCallbackOptions): Pick<
  EditorHostCallbacks,
  | 'onAttachFile'
  | 'onAttachImageSource'
  | 'onReadAttachment'
  | 'onStoreEditorBlob'
  | 'onReadEditorBlob'
  | 'onListEditorBlobs'
> {
  return {
    onAttachFile: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: attach file (instance=${instanceId}, file=${message.fileName}, base64=${message.contentBase64.length}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleAttachFile',
            message.sourcePath,
            message.fileName,
            message.contentBase64
          ),
        normalize: normalizeAttachFileResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: attach file (instance=${instanceId}, ok=${result.ok}, relativePath=${result.relativePath ?? ''}).`,
      });
    },
    onAttachImageSource: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: attach image source (instance=${instanceId}, sourceLength=${message.imageSource.length}, fallback=${message.fallbackFileName}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleAttachImageSource',
            message.sourcePath,
            message.imageSource,
            message.fallbackFileName
          ),
        normalize: normalizeAttachFileResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: attach image source (instance=${instanceId}, ok=${result.ok}, relativePath=${result.relativePath ?? ''}).`,
      });
    },
    onReadAttachment: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: read attachment (instance=${instanceId}, source=${message.imageSource}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleReadAttachment',
            message.sourcePath,
            message.imageSource
          ),
        normalize: normalizeReadAttachmentResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: read attachment (instance=${instanceId}, ok=${result.ok}, relativePath=${result.relativePath ?? ''}, base64=${result.contentBase64?.length ?? 0}).`,
      });
    },
    onStoreEditorBlob: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: store editor blob (instance=${instanceId}, blob=${message.blobId}, base64=${message.contentBase64.length}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleStoreEditorBlob',
            message.sourcePath,
            message.blobId,
            message.contentType,
            message.contentBase64
          ),
        normalize: normalizeEditorBlobResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: store editor blob (instance=${instanceId}, ok=${result.ok}, blob=${message.blobId}).`,
      });
    },
    onReadEditorBlob: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: read editor blob (instance=${instanceId}, blob=${message.blobId}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleReadEditorBlob',
            message.sourcePath,
            message.blobId
          ),
        normalize: normalizeEditorBlobReadResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: read editor blob (instance=${instanceId}, ok=${result.ok}, blob=${message.blobId}, base64=${result.contentBase64?.length ?? 0}).`,
      });
    },
    onListEditorBlobs: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: list editor blobs (instance=${instanceId}).`,
        invoke: () => invokeHost(dotNet, 'HandleListEditorBlobs', message.sourcePath),
        normalize: normalizeEditorBlobListResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: list editor blobs (instance=${instanceId}, ok=${result.ok}, count=${result.blobIds?.length ?? 0}).`,
      });
    },
  };
}
