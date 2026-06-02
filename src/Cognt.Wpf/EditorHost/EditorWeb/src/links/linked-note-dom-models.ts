import { reportDiagnostic } from '../host';
import {
  getCogntLinkedNotePath,
  getCogntLinkedNoteTitle,
} from './linked-note-paths';

export function getLinkedDocReferenceFromModel(model: unknown) {
  if (!isCogntLinkedDocModel(model)) {
    reportDiagnostic('warn', 'Cannot create linked doc reference from non-linked-doc model.', {
      modelType: typeof model,
    });
    return null;
  }

  const linkedDocModel = model as Record<string, unknown> & { pageId: string };
  const relativePath = getCogntLinkedNotePathFromModel(linkedDocModel) ?? undefined;

  return {
    pageId: linkedDocModel.pageId,
    relativePath,
  };
}

export function isCogntLinkedDocModel(model: unknown) {
  return typeof model === 'object'
    && model !== null
    && (model as { flavour?: unknown }).flavour === 'affine:embed-linked-doc'
    && typeof (model as { pageId?: unknown }).pageId === 'string';
}

export function getCogntLinkedNotePathFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    reportDiagnostic('warn', 'Cannot resolve Cognt linked note path from non-object model.', {
      modelType: typeof model,
    });
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  const relativePath = getCogntLinkedNotePath(record) ?? getCogntLinkedNotePath(props);
  if (!relativePath) {
    reportDiagnostic('warn', 'Could not resolve Cognt linked note path from model.', {
      hasProps: Object.keys(props).length > 0,
    });
  }

  return relativePath;
}

export function getLinkedNoteTitleFromModel(model: unknown) {
  if (typeof model !== 'object' || model === null) {
    reportDiagnostic('warn', 'Cannot resolve linked note title from non-object model.', {
      modelType: typeof model,
    });
    return null;
  }

  const record = model as Record<string, unknown>;
  const props = typeof record.props === 'object' && record.props !== null
    ? record.props as Record<string, unknown>
    : {};

  return getCogntLinkedNoteTitle(record) ?? getCogntLinkedNoteTitle(props);
}
