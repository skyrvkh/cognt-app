import type { DocumentLinksController } from '../document-links';
import type { AttachmentSubsystem } from './editor-bootstrap-attachments';
import type {
  EditorBootstrapConfig,
  EditorBootstrapEnvironment,
} from './editor-bootstrap-types';
import type { ContentSaveCoordinator } from './content-save-coordinator';
import type { DocumentChangeCoordinator } from './document-change-coordinator';
import type { EditorFoundation } from './editor-foundation';
import type { EditorSelectionController } from './editor-selection-controller';
import type { LinkedDocWorkflow } from './linked-doc-workflow';

export type CreateContentSaveCoordinatorOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  attachments: AttachmentSubsystem;
};

export type CreateDocumentChangeCoordinatorOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  selection: EditorSelectionController;
  runtime: EditorFoundation['runtime'];
  linkedDocs: LinkedDocWorkflow;
  documentLinks: DocumentLinksController;
  contentSave: ContentSaveCoordinator;
};

export type CreateExtractWorkflowOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  linkedDocs: LinkedDocWorkflow;
  contentSave: ContentSaveCoordinator;
};

export type CreateDocumentLoadWorkflowOptions = {
  config: EditorBootstrapConfig;
  environment: EditorBootstrapEnvironment;
  foundation: EditorFoundation;
  selection: EditorSelectionController;
  linkedDocs: LinkedDocWorkflow;
  documentLinks: DocumentLinksController;
  contentSave: ContentSaveCoordinator;
  docChanges: DocumentChangeCoordinator;
};
