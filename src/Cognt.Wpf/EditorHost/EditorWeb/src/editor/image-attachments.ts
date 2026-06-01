import type { Doc } from '@blocksuite/store';
import type {
  AttachFileResult,
} from './contracts';
import {
  base64ToUint8Array,
  inferFileExtension,
  inferImageContentType,
  inferImageFileExtension,
  isSupportedImageExtension,
  normalizeSupportedImageContentType,
  sniffImageContentType,
} from './image-data';
import {
  sanitizeAttachmentLabel,
} from './image-source-blobs';
import {
  sanitizeAttachmentTarget,
} from './image-source-paths';

export type ImageAttachmentPayload = {
  result: AttachFileResult;
  fallbackName: string;
  sourceFile: File;
  contentBase64: string;
};

export function isImageAttachmentResult(
  result: AttachFileResult
): result is AttachFileResult & { relativePath: string } {
  return result.kind?.toLowerCase() === 'image'
    && typeof result.relativePath === 'string'
    && result.relativePath.trim().length > 0;
}

export async function createLocalImageAttachmentPayload(
  sourceFile: File,
  fallbackName: string,
  contentBase64: string
): Promise<(ImageAttachmentPayload & {
  result: AttachFileResult & { relativePath: string };
}) | null> {
  const bytes = base64ToUint8Array(contentBase64);
  const contentType = sniffImageContentType(bytes)
    ?? normalizeSupportedImageContentType(sourceFile.type)
    ?? inferImageContentType(fallbackName);
  if (!contentType) {
    return null;
  }

  const extension = inferImageFileExtension(fallbackName)
    ?? inferFileExtension(contentType);
  if (!isSupportedImageExtension(extension)) {
    return null;
  }

  const relativePath = await createPreviewImageRelativePath(bytes, extension);
  return {
    result: {
      ok: true,
      displayName: sanitizeAttachmentLabel(fallbackName),
      relativePath,
      kind: 'Image',
    },
    fallbackName,
    sourceFile,
    contentBase64,
  };
}

export async function registerImageAttachmentBlob(
  doc: Doc,
  result: AttachFileResult & { relativePath: string },
  fallbackName: string,
  sourceFile: File,
  contentBase64: string
) {
  const relativePath = sanitizeAttachmentTarget(result.relativePath ?? fallbackName);
  const blob = createBlobForImageAttachment(contentBase64, sourceFile, result, fallbackName);
  await doc.blobSync.set(relativePath, blob);
  return relativePath;
}

export function createAttachmentReferenceText(result: AttachFileResult, fallbackName: string) {
  const displayName = sanitizeAttachmentLabel(result.displayName ?? fallbackName);
  const relativePath = sanitizeAttachmentTarget(result.relativePath ?? fallbackName);
  const target = relativePath.includes(' ') || /[()<>]/.test(relativePath)
    ? `<${relativePath.replace(/>/g, '%3E')}>`
    : relativePath;

  return result.kind?.toLowerCase() === 'image'
    ? `![${displayName}](${target})`
    : `[${displayName}](${target})`;
}

export function pickImageFiles() {
  return new Promise<File[]>(resolve => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg,image/webp,image/gif';
    input.multiple = true;
    input.style.display = 'none';

    const complete = () => {
      const files = Array.from(input.files ?? []).filter(file => file.size > 0);
      input.remove();
      resolve(files);
    };

    input.addEventListener('change', complete, { once: true });
    input.addEventListener('cancel', complete, { once: true });
    document.body.append(input);
    input.click();
  });
}

export function getSafeAttachmentFileName(file: File) {
  const trimmedName = file.name.trim();
  const inferredExtension = inferFileExtension(file.type);
  if (trimmedName.length > 0) {
    return hasFileExtension(trimmedName) || inferredExtension === '.bin'
      ? trimmedName
      : `${trimmedName}${inferredExtension}`;
  }

  return `clipboard-image${inferredExtension}`;
}

export function isBrowserZoomShortcut(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.altKey) {
    return false;
  }

  return event.key === '+'
    || event.key === '='
    || event.key === '-'
    || event.key === '_'
    || event.key === '0';
}

export function isBrowserZoomWheel(event: WheelEvent) {
  return event.ctrlKey || event.metaKey;
}

function createBlobForImageAttachment(
  contentBase64: string,
  sourceFile: File,
  result: AttachFileResult,
  fallbackName: string
) {
  const bytes = base64ToUint8Array(contentBase64);
  const contentType = sniffImageContentType(bytes)
    ?? normalizeSupportedImageContentType(sourceFile.type)
    ?? inferImageContentType(result.relativePath ?? result.displayName ?? fallbackName)
    ?? inferImageContentType(fallbackName)
    ?? 'application/octet-stream';

  return new File(
    [bytes],
    sanitizeAttachmentLabel(result.displayName ?? fallbackName),
    { type: contentType });
}

async function createPreviewImageRelativePath(bytes: Uint8Array, extension: string) {
  const sha256 = await computeSha256Hex(bytes);
  const fileName = sha256
    ? `${sha256}${extension}`
    : `pending-${createPreviewToken()}${extension}`;

  return `attachments/images/${fileName}`;
}

async function computeSha256Hex(bytes: Uint8Array) {
  if (!globalThis.crypto?.subtle) {
    return null;
  }

  const copy = new Uint8Array(bytes.byteLength);
  copy.set(bytes);
  const digest = await globalThis.crypto.subtle.digest('SHA-256', copy.buffer);
  return Array.from(new Uint8Array(digest))
    .map(byte => byte.toString(16).padStart(2, '0'))
    .join('');
}

function createPreviewToken() {
  return globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function hasFileExtension(value: string) {
  return /(?:^|[/\\])[^/\\]+\.[^./\\]+$/.test(value);
}
