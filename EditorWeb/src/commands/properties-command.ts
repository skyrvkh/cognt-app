import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import {
  createDefaultPropertiesRows,
  insertCogntPropertiesBlock,
} from '../blocks/properties';
import { reportDiagnostic } from '../host';

export type PropertiesCommandHost = {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  flushHost(): Promise<void>;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  notifyContentChanged(): Promise<void>;
  showStatus(message: string): void;
};

export async function runInsertPropertiesBlockCommand(
  host: PropertiesCommandHost,
  context: AffineSlashMenuContext
) {
  if (host.isInputLocked()) {
    host.showStatus('Wait for the full note to finish loading before adding properties.');
    return;
  }

  if (!host.getCurrentDoc()) {
    host.showStatus('Open a note before adding properties.');
    return;
  }

  try {
    insertCogntPropertiesBlock(
      context,
      createDefaultPropertiesRows(context.rootComponent.doc)
    );
    await host.flushHost();
    host.cancelPendingContentChanged();
    host.recordUserEdit();
    await host.notifyContentChanged();
    host.showStatus('Properties block added.');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    reportDiagnostic('error', `Could not add properties block: ${message}`);
    host.showStatus(`Add properties failed: ${message}`);
  }
}
