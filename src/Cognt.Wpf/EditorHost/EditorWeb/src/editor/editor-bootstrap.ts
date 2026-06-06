import { createEditorSpecs } from './specs';
import { createEditorFoundation } from './editor-foundation';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
  EditorBootstrapResult,
  EditorWorkflowRefs,
} from './editor-bootstrap-types';
import { createEditorWorkflowBundle } from './editor-bootstrap-workflows';

export function createEditorBootstrap(config: EditorBootstrapConfig): EditorBootstrapResult {
  const { options } = config;
  const environment = createEditorBootstrapEnvironment(config);
  const refs: EditorWorkflowRefs = {
    linkedDocs: null,
    documentLinks: null,
  };

  const {
    blobSource,
    editor,
    selection,
    interactions,
    runtime,
    mentions,
  } = createEditorFoundation({
    options,
    getCurrentDoc: environment.getCurrentDoc,
    trackCurrentDoc: doc => config.trackCurrentDoc(doc),
    getCurrentPath: environment.getCurrentPath,
    getCurrentCollectionRoot: environment.getCurrentCollectionRoot,
    scheduleContentChanged: environment.scheduleContentChanged,
    ensureReferencesBlock: () => {
      refs.documentLinks?.ensureReferencesBlock();
    },
    shouldPatchLinkedDocBlocks: () => refs.linkedDocs?.hasEmbeddedBlocks() ?? false,
  });
  const workflows = createEditorWorkflowBundle({
    config,
    environment,
    foundation: {
      blobSource,
      editor,
      selection,
      interactions,
      runtime,
      mentions,
    },
    refs,
  });
  const {
    events,
    ...workflowResult
  } = workflows;

  editor.specs = createEditorSpecs((query, abort, inlineEditor, abortSignal) =>
    mentions.getNativeMentionMenus(query, abort, inlineEditor, abortSignal)
  );
  events.bind();

  return {
    editor,
    runtime,
    selection,
    interactions,
    mentions,
    ...workflowResult,
  };
}

function createEditorBootstrapEnvironment(
  config: EditorBootstrapConfig
): EditorBootstrapEnvironment {
  return {
    getCurrentDoc: () => config.getCurrentDoc(),
    getCurrentPath: () => config.getCurrentPath(),
    getCurrentCollectionRoot: () => config.getCurrentCollectionRoot(),
    getLoadGeneration: () => config.getLoadGeneration(),
    isInputLocked: () => config.isInputLocked(),
    showLockedInputStatus: () => config.showLockedInputStatus(),
    scheduleContentChanged: (delayMs?: number, refreshMentions?: boolean) =>
      config.scheduleContentChanged(delayMs, refreshMentions),
    cancelPendingContentChanged: () => config.cancelPendingContentChanged(),
    recordUserEdit: () => config.incrementUserEditGeneration(),
    isLargeDocument: () => config.isLargeDocument(),
  };
}
