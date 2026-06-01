import type { SelectedImageBlock } from './contracts';
import { normalizeImageSourceForHost } from './image-source-paths';
import type { ImageBlockModel } from './image-source-types';

export function createSelectedImageBlock(
  value: unknown,
  collectionRoot?: string | null
): SelectedImageBlock | null {
  if (!isImageBlockModel(value)) {
    return null;
  }

  const sourceId = getImageSourceId(value);
  const imageRelPath = sourceId
    ? normalizeImageSourceForHost(sourceId, collectionRoot)
    : null;
  if (!imageRelPath) {
    return null;
  }

  return {
    imageBlockId: value.id,
    imageRelPath,
  };
}

export function isImageBlockModel(value: unknown): value is ImageBlockModel {
  return typeof value === 'object'
    && value !== null
    && (value as { flavour?: unknown }).flavour === 'affine:image'
    && typeof (value as { id?: unknown }).id === 'string';
}

export function getImageSourceId(value: {
  sourceId?: unknown;
  props?: {
    sourceId?: unknown;
  };
}) {
  const candidate = typeof value.sourceId === 'string'
    ? value.sourceId
    : typeof value.props?.sourceId === 'string'
      ? value.props.sourceId
      : '';

  return candidate.replace(/\\/g, '/').trim() || null;
}

export function setImageSourceId(
  value: {
    sourceId?: unknown;
    props?: {
      sourceId?: unknown;
    };
  },
  sourceId: string
) {
  if (typeof value.sourceId === 'string') {
    value.sourceId = sourceId;
    return;
  }

  if (value.props) {
    value.props.sourceId = sourceId;
    return;
  }

  value.sourceId = sourceId;
}
