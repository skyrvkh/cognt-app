import type { MentionTarget } from './mentions';
import {
  getNotePathFileName,
  normalizeNotePathKey,
} from './links/note-path-utils';

export function normalizeMentionPathKey(relativePath: string) {
  return normalizeNotePathKey(relativePath);
}

export function getMentionDisplayTitle(target: MentionTarget) {
  const title = target.title.trim()
    || getNotePathFileName(target.relativePath).replace(/\.(json|md|markdown)$/i, '').trim();
  return title.length > 0 ? title : target.relativePath;
}

export function getMentionMenuSuffix(target: MentionTarget) {
  const parts = target.relativePath.split(/[\\/]/).filter(Boolean);
  if (parts.length <= 1) {
    return undefined;
  }

  const first = parts[0]?.toLowerCase();
  if (first === 'files') {
    return undefined;
  }

  parts.pop();
  return parts.length > 0 ? parts.join('/') : undefined;
}
