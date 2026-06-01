import type { BlockModel } from '@blocksuite/store';
import { normalizeSelectionText } from '../documents/block-suite-document';

export function getEventBlockModel(event: Event) {
  for (const target of event.composedPath()) {
    const candidate = target as { model?: unknown; focusModel?: unknown } | null;
    const model = candidate?.model ?? candidate?.focusModel;
    if (isTextBlockModel(model)) {
      return model;
    }
  }

  return null;
}

export function isTextBlockModel(value: unknown): value is BlockModel {
  return typeof value === 'object'
    && value !== null
    && Boolean((value as { text?: unknown }).text);
}

export function getTextBlockLength(value: unknown) {
  if (!isTextBlockModel(value)) {
    return 0;
  }

  const text = (value as {
    text?: {
      length?: unknown;
      toString?: () => string;
    };
  }).text;
  if (typeof text?.length === 'number' && Number.isFinite(text.length)) {
    return Math.max(0, text.length);
  }

  const plainText = typeof text?.toString === 'function' ? text.toString() : '';
  return plainText === '[object Object]' ? 0 : plainText.length;
}

export function isHeadingBlockModel(value: unknown): value is BlockModel & { id: string } {
  const candidate = value as {
    id?: unknown;
    flavour?: unknown;
    text?: unknown;
    type?: unknown;
    props?: {
      type?: unknown;
    };
  } | null;
  const headingType = typeof candidate?.type === 'string'
    ? candidate.type
    : typeof candidate?.props?.type === 'string'
      ? candidate.props.type
      : '';

  return typeof candidate?.id === 'string'
    && candidate.flavour === 'affine:paragraph'
    && Boolean(candidate.text)
    && /^h[1-6]$/i.test(headingType);
}

export function isEmptyTextBlockModel(value: unknown) {
  if (!isTextBlockModel(value)) {
    return false;
  }

  const candidate = value as {
    text?: { length?: number };
    children?: unknown[];
  };

  return (candidate.text?.length ?? 0) === 0
    && Array.isArray(candidate.children)
    && candidate.children.length === 0;
}

export function getBlockSelectionId(value: unknown) {
  const blockId = typeof (value as { blockId?: unknown } | null)?.blockId === 'string'
    ? (value as { blockId: string }).blockId.trim()
    : '';

  return blockId.length > 0 ? [blockId] : [];
}

export function uniqueBlockIds(values: string[]) {
  const ids: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    const blockId = value.trim();
    if (!blockId || seen.has(blockId)) {
      continue;
    }

    seen.add(blockId);
    ids.push(blockId);
  }

  return ids;
}

export function getBlockPlainText(value: unknown) {
  const text = (value as { text?: unknown } | null)?.text;
  if (typeof text === 'string') {
    return normalizeSelectionText(text);
  }

  if (text && typeof (text as { toString?: unknown }).toString === 'function') {
    const result = (text as { toString(): string }).toString();
    if (result && result !== '[object Object]') {
      return normalizeSelectionText(result);
    }
  }

  return '';
}
