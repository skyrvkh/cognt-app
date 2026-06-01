export function createBlobFromBase64(
  contentBase64: string,
  contentType = 'application/octet-stream'
) {
  return new Blob([base64ToUint8Array(contentBase64)], { type: contentType });
}

export function base64ToUint8Array(value: string) {
  const binary = globalThis.atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

export function readFileAsBase64(file: File) {
  return readBlobAsBase64(file, 'The dropped file could not be read.');
}

export function readBlobAsBase64(blob: Blob, errorMessage = 'Blob read failed.') {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener('load', () => {
      const result = typeof reader.result === 'string' ? reader.result : '';
      const commaIndex = result.indexOf(',');
      resolve(commaIndex >= 0 ? result.slice(commaIndex + 1) : result);
    });
    reader.addEventListener('error', () => {
      reject(reader.error ?? new Error(errorMessage));
    });
    reader.readAsDataURL(blob);
  });
}

export function normalizeImageContentType(value: string) {
  const contentType = value.trim().toLowerCase();
  return contentType.startsWith('image/') ? contentType : null;
}

export function normalizeSupportedImageContentType(value: string) {
  const contentType = normalizeImageContentType(value);
  return contentType && isSupportedImageExtension(inferFileExtension(contentType))
    ? contentType
    : null;
}

export function inferImageContentType(value: string) {
  const extension = value
    .split(/[?#]/, 1)[0]
    .split('.')
    .pop()
    ?.trim()
    .toLowerCase();

  return extension === 'png'
    ? 'image/png'
    : extension === 'jpg' || extension === 'jpeg'
      ? 'image/jpeg'
      : extension === 'webp'
        ? 'image/webp'
        : extension === 'gif'
          ? 'image/gif'
          : null;
}

export function inferImageFileExtension(value: string) {
  const extension = value
    .split(/[?#]/, 1)[0]
    .split('.')
    .pop()
    ?.trim()
    .toLowerCase();

  if (!extension) {
    return null;
  }

  const normalizedExtension = `.${extension}`;
  return isSupportedImageExtension(normalizedExtension)
    ? normalizedExtension
    : null;
}

export function isSupportedImageExtension(value: string | null | undefined) {
  return value === '.png'
    || value === '.jpg'
    || value === '.jpeg'
    || value === '.webp'
    || value === '.gif';
}

export function inferFileExtension(contentType: string) {
  const normalized = contentType.trim().toLowerCase();
  return normalized === 'image/png'
    ? '.png'
    : normalized === 'image/jpeg'
      ? '.jpg'
      : normalized === 'image/webp'
        ? '.webp'
        : normalized === 'image/gif'
          ? '.gif'
          : '.bin';
}

export function sniffImageContentType(bytes: Uint8Array) {
  if (bytes.length >= 8
    && bytes[0] === 0x89
    && bytes[1] === 0x50
    && bytes[2] === 0x4e
    && bytes[3] === 0x47
    && bytes[4] === 0x0d
    && bytes[5] === 0x0a
    && bytes[6] === 0x1a
    && bytes[7] === 0x0a) {
    return 'image/png';
  }

  if (bytes.length >= 3
    && bytes[0] === 0xff
    && bytes[1] === 0xd8
    && bytes[2] === 0xff) {
    return 'image/jpeg';
  }

  if (bytes.length >= 6
    && bytes[0] === 0x47
    && bytes[1] === 0x49
    && bytes[2] === 0x46
    && bytes[3] === 0x38
    && (bytes[4] === 0x37 || bytes[4] === 0x39)
    && bytes[5] === 0x61) {
    return 'image/gif';
  }

  if (bytes.length >= 12
    && bytes[0] === 0x52
    && bytes[1] === 0x49
    && bytes[2] === 0x46
    && bytes[3] === 0x46
    && bytes[8] === 0x57
    && bytes[9] === 0x45
    && bytes[10] === 0x42
    && bytes[11] === 0x50) {
    return 'image/webp';
  }

  return null;
}
