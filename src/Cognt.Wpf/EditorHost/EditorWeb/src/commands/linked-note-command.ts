import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import { importBlockSuiteJsonIntoExistingDoc } from '../documents/io';
import { persistMutation, type MutationPersistenceHost } from '../editor/host-operations';
import { reportDiagnostic } from '../host';
import {
  getLinkedNotePathFromResult,
  getLinkedNoteTitleFromResult,
  insertCogntLinkedNoteEmbed,
  insertCogntLinkedNoteInline,
  type CogntLinkedDoc,
} from '../links/linked-notes';
import type { LinkedNoteViewMode } from '../editor/slash-menu';

export type CreateLinkedNoteCommandMessage = {
  type: 'createLinkedNote';
  sourcePath: string;
  viewMode: LinkedNoteViewMode;
};

type CreateLinkedNoteCommandResult = {
  ok: boolean;
  relativePath?: string;
  title?: string;
  documentContent?: string;
  error?: string;
};

export type LinkedNoteCommandHost = MutationPersistenceHost & {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  createLinkedNote(message: CreateLinkedNoteCommandMessage): Promise<CreateLinkedNoteCommandResult>;
  trackLinkedDoc(doc: Doc, relativePath: string): void;
  registerNestedLinkedDoc(linkedDoc: CogntLinkedDoc, generation: number): void;
  markLinkedDocPreviewed(doc: Doc): void;
  requestLinkedDocPreview(doc: Doc, relativePath: string, generation: number): void;
  refreshMentionHighlights(): void;
  showStatus(message: string): void;
};

export async function runCreateLinkedNoteCommand(
  host: LinkedNoteCommandHost,
  viewMode: LinkedNoteViewMode,
  context: AffineSlashMenuContext
) {
  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before creating linked notes.');
    return;
  }

  const currentPath = host.getCurrentPath();
  if (!host.getCurrentDoc() || !currentPath) {
    host.showStatus('Open a note before creating a linked note.');
    return;
  }

  try {
    const result = await host.createLinkedNote({
      type: 'createLinkedNote',
      sourcePath: currentPath,
      viewMode,
    });

    if (!result.ok) {
      throw new Error(result.error ?? 'The linked note could not be created.');
    }

    const linkedNotePath = getLinkedNotePathFromResult(result);
    if (!linkedNotePath) {
      throw new Error('The linked note path was not returned.');
    }

    const generation = host.getLoadGeneration();
    if (viewMode === 'embed') {
      const linkedDoc = insertCogntLinkedNoteEmbed(
        context,
        linkedNotePath,
        getLinkedNoteTitleFromResult(result)
      );
      host.trackLinkedDoc(linkedDoc, linkedNotePath);

      if (typeof result.documentContent === 'string') {
        await importBlockSuiteJsonIntoExistingDoc(
          linkedDoc,
          result.documentContent,
          nestedLinkedDoc => host.registerNestedLinkedDoc(nestedLinkedDoc, generation)
        );
        host.markLinkedDocPreviewed(linkedDoc);
      } else {
        host.requestLinkedDocPreview(linkedDoc, linkedNotePath, generation);
      }
    } else {
      const linkedDoc = insertCogntLinkedNoteInline(
        context,
        linkedNotePath,
        getLinkedNoteTitleFromResult(result)
      );
      host.trackLinkedDoc(linkedDoc, linkedNotePath);
    }

    await persistMutation(host);
    host.refreshMentionHighlights();
    host.showStatus(`Created ${result.relativePath ?? 'linked note'}.`);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    reportDiagnostic('error', `Could not create linked note: ${message}`);
    host.showStatus(`Create note failed: ${message}`);
  }
}
