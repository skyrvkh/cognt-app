import { reportDiagnostic } from '../host';
import {
  normalizeCreateLinkedNoteResult,
  normalizeMentionTargets,
} from './host-callback-normalizers';
import type {
  EditorHostCallbackOptions,
  EditorHostCallbacks,
} from './host-callback-types';
import { invokeLoggedHostRequest } from './host-request-logging';

export function createLinkHostCallbacks({
  dotNet,
  instanceId,
  invokeHost,
}: EditorHostCallbackOptions): Pick<
  EditorHostCallbacks,
  | 'onSearchMentionTargets'
  | 'onRequestMentionPreview'
  | 'onOpenMention'
  | 'onCreateLinkedNote'
> {
  return {
    onSearchMentionTargets: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: search mention targets (instance=${instanceId}, queryLength=${message.query.length}, limit=${message.limit}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleSearchMentionTargets',
            message.sourcePath,
            message.query,
            message.limit
          ),
        normalize: normalizeMentionTargets,
        completeMessage: targets =>
          `Bridge event completed: search mention targets (instance=${instanceId}, resultCount=${targets.length}).`,
      });
    },
    onRequestMentionPreview: message => {
      reportDiagnostic(
        'info',
        `Bridge event: request mention preview (instance=${instanceId}, request=${message.requestId}, path=${message.relativePath}, mode=${message.mode}).`
      );
      void invokeHost(
        dotNet,
        'HandleRequestMentionPreview',
        message.requestId,
        message.sourcePath,
        message.relativePath,
        message.mode,
        message.maxCharacters
      );
    },
    onOpenMention: message => {
      reportDiagnostic(
        'info',
        `Bridge event: open mention (instance=${instanceId}, path=${message.relativePath}, content=${message.content.length}).`
      );
      void invokeHost(
        dotNet,
        'HandleOpenMention',
        message.sourcePath,
        message.relativePath,
        message.content
      );
    },
    onCreateLinkedNote: async message => {
      return invokeLoggedHostRequest({
        startMessage: `Bridge event: create linked note (instance=${instanceId}, viewMode=${message.viewMode}).`,
        invoke: () =>
          invokeHost(
            dotNet,
            'HandleCreateLinkedNote',
            message.sourcePath,
            message.viewMode
          ),
        normalize: normalizeCreateLinkedNoteResult,
        level: result => (result.ok ? 'info' : 'warn'),
        completeMessage: result =>
          `Bridge event completed: create linked note (instance=${instanceId}, ok=${result.ok}, path=${result.relativePath ?? ''}).`,
      });
    },
  };
}
