export function normalizeNotePathKey(relativePath: string) {
  return relativePath
    .replace(/\\/g, '/')
    .trim()
    .replace(/^\/+|\/+$/g, '')
    .toLowerCase();
}

export function getNotePathFileName(relativePath: string) {
  const normalized = relativePath.replace(/\\/g, '/').trim();
  return normalized.split('/').filter(Boolean).pop() ?? normalized;
}
