import type { Doc } from '@blocksuite/store';
import { createBlockId } from '../documents/block-suite-document';
import type { CogntPropertyRow } from './properties-schema';

export function createDefaultPropertiesRows(doc: Doc): CogntPropertyRow[] {
  const title = doc.collection.meta.getDocMeta(doc.id)?.title ?? '';
  return [
    { id: createBlockId(), name: 'title', value: title },
    { id: createBlockId(), name: 'module', value: '' },
    { id: createBlockId(), name: 'day', value: '' },
    { id: createBlockId(), name: 'artifact', value: '' },
    { id: createBlockId(), name: 'tags', value: '' },
  ];
}

export function normalizeCogntProperties(value: unknown): CogntPropertyRow[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map(row => {
      if (!isRecord(row)) {
        return null;
      }

      const name = typeof row.name === 'string' ? row.name.trim() : '';
      const value = typeof row.value === 'string' ? row.value : '';
      const id = typeof row.id === 'string' && row.id.trim().length > 0
        ? row.id.trim()
        : createBlockId();

      if (!name && !value) {
        return null;
      }

      return {
        id,
        name: name || 'property',
        value,
      };
    })
    .filter((row): row is CogntPropertyRow => row !== null);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
