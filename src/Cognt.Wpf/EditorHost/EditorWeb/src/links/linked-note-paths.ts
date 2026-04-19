import {
  cogntEmbedStyleAliases,
  cogntLinkedNotePathProp,
  type LinkedNoteResult,
  type LinkedPageReference,
} from './linked-note-types';
import { parseNoteMentionTarget } from '../mentions';
import {
  getNotePathFileName,
  normalizeNotePathKey,
} from './note-path-utils';

export type RawCogntInlineMentionMatch = {
  index: number;
  length: number;
  relativePath: string;
};

export function getLinkedNotePathFromResult(result: LinkedNoteResult) {
  if (result.relativePath?.trim()) {
    return result.relativePath.trim();
  }

  return null;
}

export function getLinkedNoteTitleFromResult(result: LinkedNoteResult) {
  return normalizeLinkedNoteTitle(result.title);
}

export function normalizeCreatedExtractPath(path: string | null | undefined) {
  const value = path?.trim().replace(/\\/g, '/');
  if (!value) {
    return null;
  }

  const filesIndex = value.toLowerCase().lastIndexOf('/files/');
  if (filesIndex >= 0) {
    return value.slice(filesIndex + 1);
  }

  return value;
}

export function getExtractTitleFromRelPath(relativePath: string) {
  const fileName = getNotePathFileName(relativePath);
  const stem = fileName.replace(/\.(json|md|markdown)$/i, '').trim();
  const separatorIndex = stem.lastIndexOf('.');

  return separatorIndex >= 0 && separatorIndex < stem.length - 1
    ? stem.slice(separatorIndex + 1)
    : stem;
}

export function getLinkedNoteTitle(relativePath: string, title?: string | null) {
  const normalizedTitle = normalizeLinkedNoteTitle(title);
  if (normalizedTitle
    && normalizedTitle !== relativePath
    && !isCogntNoteRelativePath(normalizedTitle)) {
    return normalizedTitle;
  }

  const fileName = getNotePathFileName(relativePath);
  const fallbackTitle = fileName.replace(/\.(json|md|markdown)$/i, '').trim();
  return fallbackTitle || 'Untitled';
}

export function normalizeLinkedNotePathKey(relativePath: string) {
  return normalizeNotePathKey(relativePath);
}

export function getLinkedPageReferencePath(reference: LinkedPageReference | Record<string, unknown>) {
  const record = reference as Record<string, unknown>;
  return getCogntLinkedNotePath(record)
    ?? getTrimmedString(record.relativePath)
    ?? getTrimmedString(record.description);
}

export function getLinkedPageReferenceTitle(reference: LinkedPageReference | Record<string, unknown>) {
  return getTrimmedString((reference as Record<string, unknown>).title);
}

export function extractCogntEmbedTarget(markdown: string | undefined) {
  const match = markdown?.trim().match(/^!\[\[([^\]\n]+)\]\]$/);
  return parseCogntLinkTarget(match?.[1]);
}

export function parseCogntLinkTarget(value: string | undefined) {
  if (!value) {
    return null;
  }

  const [relativePath, ...parts] = value.split('|');
  const normalizedPath = relativePath.trim();
  if (!normalizedPath) {
    return null;
  }

  const titleParts: string[] = [];
  let style: string | null = null;

  for (const part of parts) {
    const optionMatch = part.trim().match(/^style\s*=\s*(.+)$/i);
    if (optionMatch) {
      style = normalizeCogntEmbedStyle(optionMatch[1]) ?? style;
      continue;
    }

    titleParts.push(part);
  }

  return {
    relativePath: normalizedPath,
    title: normalizeLinkedNoteTitle(titleParts.join('|')),
    style,
  };
}

export function findRawCogntInlineMentionMatches(text: string): RawCogntInlineMentionMatch[] {
  const matches: RawCogntInlineMentionMatch[] = [];
  const expression = /\[\[([^\]\[\n]+)\]\]/g;
  let match: RegExpExecArray | null;

  while ((match = expression.exec(text)) !== null) {
    const target = parseNoteMentionTarget(match[1]);
    if (!target || !isCogntNoteRelativePath(target.relativePath)) {
      continue;
    }

    matches.push({
      index: match.index,
      length: match[0].length,
      relativePath: target.relativePath,
    });
  }

  return matches;
}

export function normalizeLinkedNoteTitle(title: string | null | undefined) {
  return title?.replace(/[|\]\r\n]+/g, ' ').replace(/\s+/g, ' ').trim() || null;
}

export function normalizeCogntEmbedStyle(style: unknown) {
  if (typeof style !== 'string') {
    return null;
  }

  return cogntEmbedStyleAliases.get(style.trim().toLowerCase()) ?? null;
}

export function isCogntNoteRelativePath(relativePath: string) {
  return /\.(json|md|markdown)$/i.test(relativePath);
}

export function getCogntLinkedNotePath(props: Record<string, unknown>) {
  const value = props[cogntLinkedNotePathProp];
  const directPath = typeof value === 'string' && value.trim().length > 0
    ? value.trim()
    : null;
  if (directPath) {
    return directPath;
  }

  const params = getRecordProperty(props, 'params');
  const paramPath = params
    ? getTrimmedString(params[cogntLinkedNotePathProp])
      ?? getTrimmedString(params.relativePath)
    : null;
  if (paramPath) {
    return paramPath;
  }

  return getCogntLinkedNoteLegacyPath(props.relativePath)
    ?? getCogntLinkedNoteLegacyPath(props.description);
}

export function getTrimmedString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

export function getCogntLinkedNoteLegacyPath(value: unknown) {
  const path = getTrimmedString(value);
  return path && isCogntNoteRelativePath(path) ? path : null;
}

export function createCogntLinkedNoteParams(relativePath: string) {
  return {
    [cogntLinkedNotePathProp]: relativePath,
  };
}

export function mergeCogntLinkedNoteParams(params: unknown, relativePath: string) {
  return {
    ...(isRecord(params) ? params : {}),
    [cogntLinkedNotePathProp]: relativePath,
  };
}

export function hasCogntLinkedNotePath(params: unknown, relativePath: string) {
  return isRecord(params)
    && getTrimmedString(params[cogntLinkedNotePathProp]) === relativePath;
}

export function getRecordProperty(record: Record<string, unknown>, key: string) {
  const value = record[key];
  return isRecord(value) ? value : null;
}

export function getCogntLinkedNoteTitle(props: Record<string, unknown>) {
  const value = props.title;
  return typeof value === 'string' ? normalizeLinkedNoteTitle(value) : null;
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
