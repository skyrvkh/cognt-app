import type { AffineSlashMenuContext } from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import {
  createDefaultPropertiesRows,
  insertCogntPropertiesBlock,
} from '../blocks/properties';
import type { CogntSlashMenuRootComponent } from '../blocksuite-compat';
import { persistMutation, type MutationPersistenceHost } from '../editor/host-operations';
import { reportDiagnostic } from '../host';

export type PropertiesCommandHost = MutationPersistenceHost & {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
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
    const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
    insertCogntPropertiesBlock(
      context,
      createDefaultPropertiesRows(rootComponent.doc)
    );
    await persistMutation(host);
    host.showStatus('Properties block added.');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    reportDiagnostic('error', `Could not add properties block: ${message}`);
    host.showStatus(`Add properties failed: ${message}`);
  }
}
