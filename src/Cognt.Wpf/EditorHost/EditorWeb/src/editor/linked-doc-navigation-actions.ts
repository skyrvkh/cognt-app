import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';
import type { OpenMentionMessage } from '../mention-controller';
import {
  attachCogntPathToInlineReference,
  getLinkedNotePathFromResult,
  getLinkedNoteTitleFromResult,
  type LinkedPageReference,
} from '../links/linked-notes';
import type { CreateLinkedNoteMessage, CreateLinkedNoteResult } from './contracts';

type MaterializeNativeLinkedDocReferenceOptions = {
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  trackReferencePath(pageId: string | null | undefined, relativePath: string | null | undefined): void;
  createLinkedNote(message: CreateLinkedNoteMessage): Promise<CreateLinkedNoteResult>;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  exportContent(): Promise<string>;
  notifyContentChanged(content: string): void;
  showStatus(message: string): void;
};

type OpenLinkedDocRelativePathOptions = {
  getCurrentPath(): string;
  exportContent(): Promise<string>;
  openMention(message: OpenMentionMessage): void;
  showStatus(message: string): void;
};

export async function materializeNativeLinkedDocReference(
  options: MaterializeNativeLinkedDocReferenceOptions,
  reference: LinkedPageReference
) {
  const pageId = reference.pageId;
  const currentDoc = options.getCurrentDoc();
  const currentPath = options.getCurrentPath();
  if (!pageId || !currentDoc || !currentPath) {
    return null;
  }

  const result = await options.createLinkedNote({
    type: 'createLinkedNote',
    sourcePath: currentPath,
    viewMode: 'native',
  });

  if (!result.ok) {
    throw new Error(result.error ?? 'The new document could not be created.');
  }

  const relativePath = getLinkedNotePathFromResult(result);
  if (!relativePath) {
    throw new Error('The new document path was not returned.');
  }

  const title = getLinkedNoteTitleFromResult(result);
  options.trackReferencePath(pageId, relativePath);
  const attached = attachCogntPathToInlineReference(currentDoc, pageId, relativePath, title);
  if (!attached) {
    reportDiagnostic(
      'warn',
      `Created New Doc ${relativePath}, but could not persist its inline reference path.`
    );
  }

  options.cancelPendingContentChanged();
  options.recordUserEdit();
  options.notifyContentChanged(await options.exportContent());
  options.showStatus(`Created ${relativePath}.`);

  return relativePath;
}

export async function openLinkedDocRelativePath(
  options: OpenLinkedDocRelativePathOptions,
  relativePath: string,
  statusLabel: string
) {
  const currentPath = options.getCurrentPath();
  if (!currentPath || !relativePath) {
    return;
  }

  options.openMention({
    type: 'openMention',
    sourcePath: currentPath,
    relativePath,
    content: await options.exportContent(),
  });
  options.showStatus(`Opening ${statusLabel}...`);
}
