import { DocumentLoadWorkflow } from './document-load-workflow';
import type { CreateDocumentLoadWorkflowOptions } from './editor-bootstrap-document-types';

export function createDocumentLoadWorkflow({
  config,
  environment,
  foundation,
  selection,
  linkedDocs,
  documentLinks,
  contentSave,
  docChanges,
}: CreateDocumentLoadWorkflowOptions) {
  const { options } = config;
  const {
    blobSource,
    editor,
    interactions,
    runtime,
    mentions,
  } = foundation;

  return new DocumentLoadWorkflow({
    editor,
    blobSource,
    contentSave,
    docChanges,
    documentLinks,
    interactions,
    linkedDocs,
    mentions,
    getCurrentDoc: environment.getCurrentDoc,
    getCurrentPath: environment.getCurrentPath,
    setCurrentPath: path => config.setCurrentPath(path),
    getCurrentCollectionRoot: environment.getCurrentCollectionRoot,
    setCurrentCollectionRoot: collectionRoot => config.setCurrentCollectionRoot(collectionRoot),
    getLoadGeneration: environment.getLoadGeneration,
    nextLoadGeneration: () => config.nextLoadGeneration(),
    setLoadingDocument: loading => config.setLoadingDocument(loading),
    clearOutlineCache: () => selection.clearOutlineCache(),
    trackCurrentDoc: doc => config.trackCurrentDoc(doc),
    flushHost: () => runtime.flushHost(),
    showStatus: message => runtime.showStatus(message),
    readAttachment: message => options.onReadAttachment(message),
  });
}
