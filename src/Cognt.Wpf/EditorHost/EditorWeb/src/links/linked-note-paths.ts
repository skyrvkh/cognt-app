import {
  cogntEmbedStyleAliases,
  cogntLinkedNoteDisplayProp,
  cogntLinkedNotePathProp,
  iconOnlyCogntLinkedNoteDisplay,
  type LinkedNoteResult,
  type LinkedPageReference,
} from './linked-note-types';
import { reportDiagnostic } from '../host';
import { parseNoteMentionTarget } from '../mentions';
import { isPlainRecord } from '../documents/block-suite-document';
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

  reportDiagnostic('warn', 'Linked note result did not include a relative path.', {
    hasTitle: Boolean(result.title?.trim()),
  });
  return null;
}

export function getLinkedNoteTitleFromResult(result: LinkedNoteResult) {
  return normalizeLinkedNoteTitle(result.title);
}

export function normalizeCreatedExtractPath(path: string | null | undefined) {
  const value = path?.trim().replace(/\\/g, '/');
  if (!value) {
    reportDiagnostic('warn', 'Created extract path was empty after normalization.', {
      hadInput: path !== null && path !== undefined,
    });
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

  const title = separatorIndex >= 0 && separatorIndex < stem.length - 1
    ? stem.slice(separatorIndex + 1)
    : stem;
  return title;
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
  const relativePath = getCogntLinkedNotePath(record)
    ?? getTrimmedString(record.relativePath)
    ?? getTrimmedString(record.description);
  if (!relativePath) {
    reportDiagnostic('warn', 'Linked page reference did not include a note path.', {
      hasReferencePageId: typeof record.pageId === 'string',
    });
  }

  return relativePath;
}

export function getLinkedPageReferenceTitle(reference: LinkedPageReference | Record<string, unknown>) {
  return getTrimmedString((reference as Record<string, unknown>).title);
}

export function isIconOnlyLinkedPageReference(reference: LinkedPageReference | Record<string, unknown>) {
  const record = reference as Record<string, unknown>;
  const params = getRecordProperty(record, 'params');
  const display = getTrimmedString(record[cogntLinkedNoteDisplayProp])
    ?? (params ? getTrimmedString(params[cogntLinkedNoteDisplayProp]) : null);

  const iconOnly = display === iconOnlyCogntLinkedNoteDisplay;
  return iconOnly;
}

export function extractCogntEmbedTarget(markdown: string | undefined) {
  const match = markdown?.trim().match(/^!\[\[([^\]\n]+)\]\]$/);
  const target = parseCogntLinkTarget(match?.[1]);
  if (!target) {
    reportDiagnostic('warn', 'Could not extract Cognt embed target from markdown.', {
      markdownLength: markdown?.length ?? 0,
      matched: Boolean(match),
    });
  }

  return target;
}

export function parseCogntLinkTarget(value: string | undefined) {
  if (!value) {
    reportDiagnostic('warn', 'Skipped Cognt link target parsing because value was empty.');
    return null;
  }

  const [relativePath, ...parts] = value.split('|');
  const normalizedPath = relativePath.trim();
  if (!normalizedPath) {
    reportDiagnostic('warn', 'Skipped Cognt link target parsing because path was empty.', {
      partCount: parts.length,
    });
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

  const target = {
    relativePath: normalizedPath,
    title: normalizeLinkedNoteTitle(titleParts.join('|')),
    style,
  };
  return target;
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
    reportDiagnostic('warn', 'Embed style normalization received a non-string value.', {
      valueType: typeof style,
    });
    return null;
  }

  const normalized = cogntEmbedStyleAliases.get(style.trim().toLowerCase()) ?? null;
  if (!normalized) {
    reportDiagnostic('warn', 'Embed style normalization rejected an unknown style.', { style });
  }

  return normalized;
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

  const legacyPath = getCogntLinkedNoteLegacyPath(props.relativePath)
    ?? getCogntLinkedNoteLegacyPath(props.description);
  return legacyPath;
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

export function createIconOnlyCogntLinkedNoteParams(relativePath: string) {
  return {
    [cogntLinkedNotePathProp]: relativePath,
    [cogntLinkedNoteDisplayProp]: iconOnlyCogntLinkedNoteDisplay,
  };
}

export function mergeCogntLinkedNoteParams(params: unknown, relativePath: string) {
  return {
    ...(isPlainRecord(params) ? params : {}),
    [cogntLinkedNotePathProp]: relativePath,
  };
}

export function hasCogntLinkedNotePath(params: unknown, relativePath: string) {
  return isPlainRecord(params)
    && getTrimmedString(params[cogntLinkedNotePathProp]) === relativePath;
}

export function getRecordProperty(record: Record<string, unknown>, key: string) {
  const value = record[key];
  return isPlainRecord(value) ? value : null;
}

export function getCogntLinkedNoteTitle(props: Record<string, unknown>) {
  const value = props.title;
  return typeof value === 'string' ? normalizeLinkedNoteTitle(value) : null;
}
