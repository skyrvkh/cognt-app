import type { ReadAttachmentResult } from './contracts';
import {
  base64ToUint8Array,
  inferImageContentType,
  normalizeImageContentType,
} from './image-data';

export function createBlobFromReadAttachmentResult(
  result: ReadAttachmentResult,
  fallbackName: string
) {
  const contentBase64 = result.contentBase64?.trim();
  if (!contentBase64) {
    return null;
  }

  const bytes = base64ToUint8Array(contentBase64);
  const contentType = normalizeImageContentType(result.contentType ?? '')
    ?? inferImageContentType(result.relativePath ?? result.fileName ?? fallbackName)
    ?? 'application/octet-stream';

  return new File(
    [bytes],
    sanitizeAttachmentLabel(result.fileName ?? fallbackName),
    { type: contentType });
}

export function sanitizeAttachmentLabel(value: string) {
  return value
    .replace(/[\]\[\r\n]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim() || 'Attachment';
}
