import type {
  AttachFileResult,
  CreateLinkedNoteResult,
  ReadAttachmentResult,
} from '../editor/contracts';
import type {
  EditorBlobListResult,
  EditorBlobReadResult,
  EditorBlobResult,
} from '../editor/blob-source';
import type { MentionTarget } from '../mentions';

type HostRecord = Record<string, unknown>;

export function normalizeCreateLinkedNoteResult(value: unknown): CreateLinkedNoteResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    relativePath: readString(result, 'relativePath', 'RelativePath'),
    title: readString(result, 'title', 'Title'),
    documentContent: readString(result, 'documentContent', 'DocumentContent'),
    error: readString(result, 'error', 'Error'),
  };
}

export function normalizeMentionTargets(value: unknown): MentionTarget[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap(item => {
    const target = asHostRecord(item);
    const relativePath = readString(target, 'relativePath', 'RelativePath');
    if (!relativePath) {
      return [];
    }

    return [{
      relativePath,
      title: readString(target, 'title', 'Title') ?? '',
    }];
  });
}

export function normalizeAttachFileResult(value: unknown): AttachFileResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    displayName: readString(result, 'displayName', 'DisplayName'),
    relativePath: readString(result, 'relativePath', 'RelativePath'),
    kind: readString(result, 'kind', 'Kind'),
    error: readString(result, 'error', 'Error'),
  };
}

export function normalizeReadAttachmentResult(value: unknown): ReadAttachmentResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    fileName: readString(result, 'fileName', 'FileName'),
    relativePath: readString(result, 'relativePath', 'RelativePath'),
    contentType: readString(result, 'contentType', 'ContentType'),
    contentBase64: readString(result, 'contentBase64', 'ContentBase64'),
    error: readString(result, 'error', 'Error'),
  };
}

export function normalizeEditorBlobResult(value: unknown): EditorBlobResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    error: readString(result, 'error', 'Error'),
  };
}

export function normalizeEditorBlobReadResult(value: unknown): EditorBlobReadResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    contentType: readString(result, 'contentType', 'ContentType'),
    contentBase64: readString(result, 'contentBase64', 'ContentBase64'),
    error: readString(result, 'error', 'Error'),
  };
}

export function normalizeEditorBlobListResult(value: unknown): EditorBlobListResult {
  const result = asHostRecord(value);
  return {
    ok: readBoolean(result, 'ok', 'Ok') ?? false,
    blobIds: readStringArray(result, 'blobIds', 'BlobIds') ?? [],
    error: readString(result, 'error', 'Error'),
  };
}

function asHostRecord(value: unknown): HostRecord | null {
  return typeof value === 'object' && value !== null
    ? value as HostRecord
    : null;
}

function readBoolean(record: HostRecord | null, ...keys: string[]) {
  for (const key of keys) {
    const value = record?.[key];
    if (typeof value === 'boolean') {
      return value;
    }
  }

  return undefined;
}

function readString(record: HostRecord | null, ...keys: string[]) {
  for (const key of keys) {
    const value = record?.[key];
    if (typeof value === 'string') {
      return value;
    }
  }

  return undefined;
}

function readStringArray(record: HostRecord | null, ...keys: string[]) {
  for (const key of keys) {
    const value = record?.[key];
    if (Array.isArray(value)) {
      return value.filter((item): item is string => typeof item === 'string');
    }
  }

  return undefined;
}
