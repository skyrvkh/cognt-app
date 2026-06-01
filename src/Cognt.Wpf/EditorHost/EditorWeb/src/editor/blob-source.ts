import { reportDiagnostic } from '../host';
import {
  createBlobFromBase64,
  readBlobAsBase64,
} from './image-data';

export type BlobSource = {
  name: string;
  readonly: boolean;
  get(key: string): Promise<Blob | null>;
  set(key: string, value: Blob): Promise<string>;
  delete(key: string): Promise<void>;
  list(): Promise<string[]>;
};

export type StoreEditorBlobMessage = {
  type: 'storeEditorBlob';
  sourcePath: string;
  blobId: string;
  contentType: string;
  contentBase64: string;
};

export type ReadEditorBlobMessage = {
  type: 'readEditorBlob';
  sourcePath: string;
  blobId: string;
};

export type ListEditorBlobsMessage = {
  type: 'listEditorBlobs';
  sourcePath: string;
};

export type EditorBlobResult = {
  ok: boolean;
  error?: string;
};

export type EditorBlobReadResult = {
  ok: boolean;
  contentType?: string;
  contentBase64?: string;
  error?: string;
};

export type EditorBlobListResult = {
  ok: boolean;
  blobIds?: string[];
  error?: string;
};

export type CogntBlobSourceOptions = {
  getSourcePath(): string;
  storeBlob(message: StoreEditorBlobMessage): Promise<EditorBlobResult>;
  readBlob(message: ReadEditorBlobMessage): Promise<EditorBlobReadResult>;
  listBlobs(message: ListEditorBlobsMessage): Promise<EditorBlobListResult>;
};

export function createCogntBlobSource(options: CogntBlobSourceOptions): BlobSource {
  return {
    name: 'cognt-editor-assets',
    readonly: false,
    async get(blobId) {
      const sourcePath = options.getSourcePath();
      if (!sourcePath || !blobId) {
        return null;
      }

      try {
        const result = await options.readBlob({
          type: 'readEditorBlob',
          sourcePath,
          blobId,
        });
        if (!result.ok || !result.contentBase64) {
          return null;
        }

        return createBlobFromBase64(result.contentBase64, result.contentType || 'application/octet-stream');
      } catch (error) {
        reportDiagnostic('warn', `Editor blob read failed (${blobId}): ${formatError(error)}`);
        return null;
      }
    },
    async set(blobId, blob) {
      const sourcePath = options.getSourcePath();
      if (!sourcePath) {
        throw new Error('Open a note before storing editor blobs.');
      }

      const contentBase64 = await readBlobAsBase64(blob);
      const result = await options.storeBlob({
        type: 'storeEditorBlob',
        sourcePath,
        blobId,
        contentType: blob.type || 'application/octet-stream',
        contentBase64,
      });
      if (!result.ok) {
        throw new Error(result.error ?? 'The editor blob could not be stored.');
      }

      return blobId;
    },
    async delete() {
      return;
    },
    async list() {
      const sourcePath = options.getSourcePath();
      if (!sourcePath) {
        return [];
      }

      try {
        const result = await options.listBlobs({
          type: 'listEditorBlobs',
          sourcePath,
        });
        return result.ok ? result.blobIds ?? [] : [];
      } catch (error) {
        reportDiagnostic('warn', `Editor blob list failed: ${formatError(error)}`);
        return [];
      }
    },
  };
}

function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
