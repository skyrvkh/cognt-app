import type {
  AttachFileResult,
  AttachImageSourceMessage,
  ReadAttachmentMessage,
  ReadAttachmentResult,
} from './contracts';

export type RichHtmlPasteHostSourceOptions = {
  getCurrentPath(): string;
  attachImageSource(message: AttachImageSourceMessage): Promise<AttachFileResult>;
  readAttachment(message: ReadAttachmentMessage): Promise<ReadAttachmentResult>;
};

export async function readHostImageSourceAsDataUrl(
  options: RichHtmlPasteHostSourceOptions,
  imageSource: string,
  fallbackFileName: string
) {
  const sourcePath = options.getCurrentPath();
  if (!sourcePath) {
    return null;
  }

  const attached = await options.attachImageSource({
    type: 'attachImageSource',
    sourcePath,
    imageSource,
    fallbackFileName,
  });
  const relativePath = getAttachedRelativePath(attached);
  if (!relativePath) {
    return null;
  }

  const read = await options.readAttachment({
    type: 'readAttachment',
    sourcePath,
    imageSource: relativePath,
  });
  if (!read.ok || !read.contentBase64) {
    return null;
  }

  const contentType = read.contentType?.trim().toLowerCase().startsWith('image/')
    ? read.contentType.trim()
    : 'image/png';
  return `data:${contentType};base64,${read.contentBase64}`;
}

function getAttachedRelativePath(result: AttachFileResult) {
  if (!result.ok || typeof result.relativePath !== 'string') {
    return null;
  }

  const relativePath = result.relativePath.trim();
  return relativePath.length > 0 ? relativePath : null;
}
