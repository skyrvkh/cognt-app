import { normalizeLocalPath } from './image-source-paths';
import type { DocumentLoadOptions, DocumentLoadWorkflowOptions } from './document-load-types';

type DocumentLoadPreparationOptions = Pick<
  DocumentLoadWorkflowOptions,
  | 'setLoadingDocument'
  | 'interactions'
  | 'contentSave'
  | 'mentions'
  | 'clearOutlineCache'
  | 'setCurrentPath'
  | 'setCurrentCollectionRoot'
  | 'linkedDocs'
>;

export function prepareDocumentLoad(
  options: DocumentLoadPreparationOptions,
  loadOptions: DocumentLoadOptions
) {
  options.setLoadingDocument(true);
  options.interactions.enableEditorMode();
  options.contentSave.cancelAllScheduledSaves();
  options.mentions.cancelScheduledHighlightUpdate();
  options.clearOutlineCache();
  options.setCurrentPath(loadOptions.path ?? '');
  options.setCurrentCollectionRoot(normalizeLocalPath(loadOptions.collectionRoot ?? ''));
  options.interactions.clearCachedTextSelection();
  options.linkedDocs.clear();
}
