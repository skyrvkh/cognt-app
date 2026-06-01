import { inferImageContentType } from './image-data';

export function normalizeImageSourceForHost(sourceId: string, collectionRoot?: string | null) {
  const normalizedSource = sourceId.replace(/\\/g, '/').trim();
  if (normalizedSource.length === 0) {
    return null;
  }

  const filePath = tryGetFileUriPath(normalizedSource);
  if (filePath && collectionRoot) {
    const relativePath = tryGetCollectionRelativePath(filePath, collectionRoot);
    if (relativePath) {
      return relativePath;
    }
  }

  const localPath = tryGetLocalPath(normalizedSource);
  if (localPath && collectionRoot) {
    const relativePath = tryGetCollectionRelativePath(localPath, collectionRoot);
    if (relativePath) {
      return relativePath;
    }
  }

  return isCollectionImageReference(normalizedSource)
    ? normalizedSource.replace(/^\/+/, '')
    : null;
}

export function normalizeLocalPath(path: string) {
  return path.replace(/\\/g, '/').trim().replace(/\/+$/, '');
}

export function sanitizeAttachmentTarget(value: string) {
  return value
    .replace(/\\/g, '/')
    .replace(/[\r\n]+/g, '')
    .trim() || 'Attachment';
}

function tryGetFileUriPath(source: string) {
  try {
    const uri = new URL(source);
    if (uri.protocol !== 'file:') {
      return null;
    }

    return decodeURIComponent(uri.pathname)
      .replace(/^\/([A-Za-z]:\/)/, '$1')
      .replace(/\\/g, '/');
  } catch {
    return null;
  }
}

function tryGetLocalPath(source: string) {
  if (!/^[A-Za-z]:\//.test(source) && !source.startsWith('//')) {
    return null;
  }

  return source.replace(/^\/([A-Za-z]:\/)/, '$1');
}

function tryGetCollectionRelativePath(filePath: string, collectionRoot: string) {
  const root = collectionRoot.replace(/\\/g, '/').replace(/\/+$/, '');
  const fullPath = filePath.replace(/\\/g, '/');
  const rootPrefix = `${root}/`;
  if (root.length === 0 || !fullPath.toLowerCase().startsWith(rootPrefix.toLowerCase())) {
    return null;
  }

  return fullPath.slice(rootPrefix.length).replace(/^\/+/, '');
}

function isCollectionImageReference(source: string) {
  if (/^[a-z][a-z0-9+.-]*:/i.test(source)) {
    return false;
  }

  const path = source.replace(/^\/+/, '').split(/[?#]/, 1)[0];
  if (!inferImageContentType(path)) {
    return false;
  }

  return !/^[A-Za-z]:\//.test(path)
    && !path.startsWith('//')
    && !path.includes('..');
}
