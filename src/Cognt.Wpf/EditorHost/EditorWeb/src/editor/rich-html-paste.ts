import type { PageEditor } from '@blocksuite/presets';
import { reportDiagnostic } from '../host';
import { readBlobAsBase64 } from './image-data';

const richPasteSaveDelayMs = 2200;
const imageFetchTimeoutMs = 5000;
const maxImageResolutionConcurrency = 4;
const maxInlineImageBytes = 16 * 1024 * 1024;

export type RichHtmlPasteImage = {
  marker: string;
  dataUrl: string;
  fallbackFileName: string;
};

type RichHtmlPasteControllerOptions = {
  editor: PageEditor;
  recordUserEdit(): void;
  scheduleContentChanged(delayMs?: number, refreshMentions?: boolean): void;
  scheduleDocumentChangedNotification(): void;
  showStatus(message: string): void;
  readNativeClipboardImage(): Promise<string | null>;
  readImageSourceAsDataUrl(imageSource: string, fallbackFileName: string): Promise<string | null>;
  insertImages(images: RichHtmlPasteImage[]): Promise<unknown>;
};

export class RichHtmlPasteController {
  private isReplayingPaste = false;

  public constructor(private readonly options: RichHtmlPasteControllerOptions) {}

  public get isReplaying() {
    return this.isReplayingPaste;
  }

  public handlePaste(event: ClipboardEvent) {
    if (this.isReplayingPaste || !shouldNormalizeRichHtmlPaste(event)) {
      return false;
    }

    event.preventDefault();
    event.stopPropagation();
    event.stopImmediatePropagation();

    void this.replayNormalizedPaste(event);
    return true;
  }

  public scheduleSaveAfterPaste() {
    this.options.recordUserEdit();
    this.options.scheduleDocumentChangedNotification();
    this.options.scheduleContentChanged(richPasteSaveDelayMs);
  }

  private async replayNormalizedPaste(event: ClipboardEvent) {
    const clipboard = event.clipboardData;
    const html = clipboard?.getData('text/html') ?? '';
    const plainText = clipboard?.getData('text/plain') ?? '';

    try {
      const clipboardImages = await getClipboardImageDataUrls(clipboard);
      const normalized = await normalizePastedHtmlImages(
        html,
        plainText,
        createLazyNativeClipboardImages(clipboardImages, () => this.tryReadNativeClipboardImage()),
        (imageSource, fallbackFileName) =>
          this.tryReadImageSourceAsDataUrl(imageSource, fallbackFileName),
        { useImageMarkers: true }
      );
      const dataTransfer = new DataTransfer();
      dataTransfer.setData('text/html', normalized.html);
      dataTransfer.setData('text/plain', plainText || getPlainTextFromHtml(normalized.html));

      const replayed = new ClipboardEvent('paste', {
        bubbles: true,
        cancelable: true,
        composed: true,
        clipboardData: dataTransfer,
      });

      this.isReplayingPaste = true;
      try {
        const target = event.composedPath()[0];
        (target instanceof EventTarget ? target : this.options.editor).dispatchEvent(replayed);
      } finally {
        this.isReplayingPaste = false;
      }

      if (normalized.images.length > 0) {
        await this.tryInsertImages(normalized.images);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Rich HTML paste normalization failed: ${message}`);
      this.options.showStatus('Pasted with browser formatting fallback.');
      this.replayOriginalPaste(html, plainText, event);
    }
  }

  private replayOriginalPaste(html: string, plainText: string, event: ClipboardEvent) {
    const dataTransfer = new DataTransfer();
    dataTransfer.setData('text/html', html);
    dataTransfer.setData('text/plain', plainText || getPlainTextFromHtml(html));
    const replayed = new ClipboardEvent('paste', {
      bubbles: true,
      cancelable: true,
      composed: true,
      clipboardData: dataTransfer,
    });

    this.isReplayingPaste = true;
    try {
      const target = event.composedPath()[0];
      (target instanceof EventTarget ? target : this.options.editor).dispatchEvent(replayed);
    } finally {
      this.isReplayingPaste = false;
    }
  }

  private async tryReadNativeClipboardImage() {
    try {
      return await this.options.readNativeClipboardImage();
    } catch (error) {
      reportDiagnostic('warn', `Could not read native clipboard image: ${formatError(error)}`);
      return null;
    }
  }

  private async tryReadImageSourceAsDataUrl(imageSource: string, fallbackFileName: string) {
    try {
      const dataUrl = await this.options.readImageSourceAsDataUrl(imageSource, fallbackFileName);
      return dataUrl && isDataImageUrl(dataUrl) ? dataUrl : null;
    } catch (error) {
      reportDiagnostic('warn', `Could not read pasted image source ${imageSource}: ${formatError(error)}`);
      return null;
    }
  }

  private async tryInsertImages(images: RichHtmlPasteImage[]) {
    try {
      await this.options.insertImages(images);
    } catch (error) {
      reportDiagnostic('warn', `Could not insert pasted rich HTML images: ${formatError(error)}`);
    }
  }
}

type ClipboardImageSet = {
  readonly images: string[];
  ensureFallbackImages(): Promise<string[]>;
};

type ReadImageSourceAsDataUrl = (
  imageSource: string,
  fallbackFileName: string
) => Promise<string | null>;

export type NormalizeRichHtmlForPasteOptions = {
  html: string;
  plainText?: string;
  clipboardImages?: string[];
  readNativeClipboardImage?: () => Promise<string | null>;
  readImageSourceAsDataUrl?: (
    imageSource: string,
    fallbackFileName: string
  ) => Promise<string | null>;
};

export async function normalizeRichHtmlForPaste({
  html,
  plainText = '',
  clipboardImages = [],
  readNativeClipboardImage = () => Promise.resolve(null),
  readImageSourceAsDataUrl = () => Promise.resolve(null),
}: NormalizeRichHtmlForPasteOptions) {
  const normalized = await normalizePastedHtmlImages(
    html,
    plainText,
    createLazyNativeClipboardImages([...clipboardImages], readNativeClipboardImage),
    readImageSourceAsDataUrl
  );
  return normalized.html;
}

export async function normalizeRichHtmlForPasteWithImages({
  html,
  plainText = '',
  clipboardImages = [],
  readNativeClipboardImage = () => Promise.resolve(null),
  readImageSourceAsDataUrl = () => Promise.resolve(null),
}: NormalizeRichHtmlForPasteOptions) {
  return normalizePastedHtmlImages(
    html,
    plainText,
    createLazyNativeClipboardImages([...clipboardImages], readNativeClipboardImage),
    readImageSourceAsDataUrl,
    { useImageMarkers: true }
  );
}

type NormalizePastedHtmlImagesOptions = {
  useImageMarkers?: boolean;
};

function createLazyNativeClipboardImages(
  images: string[],
  readNativeClipboardImage: () => Promise<string | null>
): ClipboardImageSet {
  let didReadFallback = false;

  return {
    images,
    async ensureFallbackImages() {
      if (images.length > 0 || didReadFallback) {
        return images;
      }

      didReadFallback = true;
      const nativeImage = await readNativeClipboardImage();
      if (nativeImage) {
        images.push(nativeImage);
      }

      return images;
    },
  };
}

function shouldNormalizeRichHtmlPaste(event: ClipboardEvent) {
  const clipboard = event.clipboardData;
  const html = clipboard?.getData('text/html') ?? '';
  const plainText = clipboard?.getData('text/plain') ?? '';
  return html.length > 0 || hasClipboardImage(clipboard) && plainText.trim().length > 0;
}

async function normalizePastedHtmlImages(
  rawHtml: string,
  plainText: string,
  clipboardImages: ClipboardImageSet,
  readImageSourceAsDataUrl: ReadImageSourceAsDataUrl,
  options: NormalizePastedHtmlImagesOptions = {}
) {
  const sourceUrl = getClipboardSourceUrl(rawHtml);
  const html = rawHtml ? extractClipboardHtml(rawHtml) : createPlainTextHtml(plainText);
  const document = new DOMParser().parseFromString(html, 'text/html');
  promoteBackgroundImages(document);
  const images = Array.from(document.images);
  const pasteImages: RichHtmlPasteImage[] = [];
  const imageResolver = new PasteImageSourceResolver(sourceUrl, readImageSourceAsDataUrl);
  const imagePlans = images.map(image => createImageNormalizationPlan(image, sourceUrl));
  const imageResolutions = await mapWithConcurrency(
    imagePlans,
    maxImageResolutionConcurrency,
    plan => resolveImageNormalizationPlan(plan, imageResolver)
  );

  for (const resolution of imageResolutions) {
    await applyImageNormalization(
      resolution,
      clipboardImages,
      pasteImages,
      options
    );
  }

  if (images.length === 0) {
    await clipboardImages.ensureFallbackImages();
    if (options.useImageMarkers) {
      appendClipboardImageMarkers(document, clipboardImages.images, pasteImages);
    } else {
      appendClipboardImages(document, clipboardImages.images);
    }
  }

  return {
    html: document.body.innerHTML || html,
    images: pasteImages,
  };
}

type ImageNormalizationPlan = {
  image: HTMLImageElement;
  source: string | null;
  resolvedUrl: string | null;
  fallbackFileName: string;
  removeImage: boolean;
};

type ImageNormalizationResolution = ImageNormalizationPlan & {
  dataUrl: string | null;
};

function createImageNormalizationPlan(
  image: HTMLImageElement,
  sourceUrl: string | null
): ImageNormalizationPlan {
  const source = getBestImageSource(image);
  const resolvedUrl = source && !isDataImageUrl(source)
    ? resolveImageUrl(source, sourceUrl)
    : source;
  const fallbackFileName = getImageFallbackFileName(
    image,
    resolvedUrl ?? source ?? 'pasted-image'
  );

  return {
    image,
    source,
    resolvedUrl,
    fallbackFileName,
    removeImage: Boolean(resolvedUrl && isInvalidPastedUiImageSource(resolvedUrl)),
  };
}

async function resolveImageNormalizationPlan(
  plan: ImageNormalizationPlan,
  imageResolver: PasteImageSourceResolver
): Promise<ImageNormalizationResolution> {
  return {
    ...plan,
    dataUrl: plan.removeImage || !plan.source
      ? null
      : await imageResolver.resolve(plan),
  };
}

async function applyImageNormalization(
  resolution: ImageNormalizationResolution,
  clipboardImages: ClipboardImageSet,
  pasteImages: RichHtmlPasteImage[],
  options: NormalizePastedHtmlImagesOptions
) {
  const {
    image,
    source,
    resolvedUrl,
    fallbackFileName,
    removeImage,
  } = resolution;

  if (removeImage) {
    image.remove();
    return;
  }

  if (!source && clipboardImages.images.length === 0) {
    await clipboardImages.ensureFallbackImages();
  }

  if (!source && clipboardImages.images.length === 0) {
    return;
  }

  if (!resolution.dataUrl && clipboardImages.images.length === 0) {
    await clipboardImages.ensureFallbackImages();
  }

  const replacement = resolution.dataUrl
    ?? (clipboardImages.images.length > 0 ? clipboardImages.images.shift() : null)
    ?? resolvedUrl;
  if (!replacement) {
    return;
  }

  if (options.useImageMarkers && isDataImageUrl(replacement)) {
    replaceImageWithPasteMarker(image, replacement, fallbackFileName, pasteImages);
    return;
  }

  if (replacement === resolvedUrl && !isDataImageUrl(replacement)) {
    reportDiagnostic('warn', `Could not persist pasted image ${replacement}; leaving remote source in pasted HTML.`);
  }

  image.setAttribute('src', replacement);
  image.removeAttribute('srcset');
}

class PasteImageSourceResolver {
  private readonly inlineDataUrlBySource = new Map<string, Promise<string | null>>();
  private readonly hostDataUrlBySource = new Map<string, Promise<string | null>>();

  public constructor(
    private readonly sourceUrl: string | null,
    private readonly readImageSourceAsDataUrl: ReadImageSourceAsDataUrl
  ) {}

  public async resolve(plan: ImageNormalizationPlan) {
    if (plan.source && isDataImageUrl(plan.source)) {
      return plan.source;
    }

    const dataUrl = plan.source
      ? await this.readInlineImageAsDataUrl(plan.source)
      : null;
    if (dataUrl) {
      return dataUrl;
    }

    return plan.resolvedUrl && isHostReadableImageSource(plan.resolvedUrl)
      ? await this.readHostImageAsDataUrl(plan.resolvedUrl, plan.fallbackFileName)
      : null;
  }

  private async readInlineImageAsDataUrl(source: string) {
    const resolvedUrl = resolveImageUrl(source, this.sourceUrl);
    if (!resolvedUrl) {
      return null;
    }

    let dataUrl = this.inlineDataUrlBySource.get(resolvedUrl);
    if (!dataUrl) {
      dataUrl = tryFetchImageAsDataUrl(resolvedUrl);
      this.inlineDataUrlBySource.set(resolvedUrl, dataUrl);
    }

    return await dataUrl;
  }

  private async readHostImageAsDataUrl(imageSource: string, fallbackFileName: string) {
    let dataUrl = this.hostDataUrlBySource.get(imageSource);
    if (!dataUrl) {
      dataUrl = tryReadHostImageAsDataUrl(
        imageSource,
        fallbackFileName,
        this.readImageSourceAsDataUrl
      );
      this.hostDataUrlBySource.set(imageSource, dataUrl);
    }

    return await dataUrl;
  }
}

async function tryReadHostImageAsDataUrl(
  imageSource: string,
  fallbackFileName: string,
  readImageSourceAsDataUrl: ReadImageSourceAsDataUrl
) {
  const dataUrl = await readImageSourceAsDataUrl(imageSource, fallbackFileName);
  return dataUrl && isDataImageUrl(dataUrl) ? dataUrl : null;
}

function getBestImageSource(image: HTMLImageElement) {
  const src = image.getAttribute('src')?.trim() ?? '';
  const lazySource = [
    image.getAttribute('data-src'),
    image.getAttribute('data-original'),
    image.getAttribute('data-lazy-src'),
  ]
    .map(value => value?.trim() ?? '')
    .find(value => value.length > 0) ?? '';
  const candidates = isLikelyLazyImagePlaceholder(src) && lazySource
    ? [
        lazySource,
        pickSrcSetCandidate(image.getAttribute('srcset')),
        src,
      ]
    : [
        src,
        pickSrcSetCandidate(image.getAttribute('srcset')),
        lazySource,
      ];

  return candidates
    .map(value => value?.trim() ?? '')
    .find(value => value.length > 0) ?? null;
}

function isLikelyLazyImagePlaceholder(value: string) {
  return /^data:image\/(?:gif|png);base64,/i.test(value) && value.length < 256;
}

function pickSrcSetCandidate(value: string | null) {
  if (!value) {
    return null;
  }

  return value
    .split(',')
    .map(candidate => candidate.trim().split(/\s+/, 1)[0])
    .find(candidate => candidate.length > 0) ?? null;
}

async function tryFetchImageAsDataUrl(url: string) {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), imageFetchTimeoutMs);
  try {
    const response = await fetch(url, {
      credentials: 'omit',
      signal: controller.signal,
    });
    if (!response.ok) {
      return null;
    }

    const blob = await response.blob();
    if (blob.size <= 0 || blob.size > maxInlineImageBytes) {
      return null;
    }

    const contentType = normalizeImageContentType(
      blob.type || response.headers.get('content-type') || ''
    );
    if (!contentType) {
      return null;
    }

    return `data:${contentType};base64,${await readBlobAsBase64(blob)}`;
  } catch (error) {
    reportDiagnostic('info', `Could not inline pasted image ${url}: ${formatError(error)}`);
    return null;
  } finally {
    window.clearTimeout(timeout);
  }
}

async function mapWithConcurrency<TInput, TOutput>(
  items: TInput[],
  concurrency: number,
  mapItem: (item: TInput, index: number) => Promise<TOutput>
) {
  const results = new Array<TOutput>(items.length);
  let nextIndex = 0;
  const workerCount = Math.max(1, Math.min(concurrency, items.length));
  const workers = Array.from({ length: workerCount }, async () => {
    while (nextIndex < items.length) {
      const index = nextIndex++;
      results[index] = await mapItem(items[index], index);
    }
  });

  await Promise.all(workers);
  return results;
}

async function getClipboardImageDataUrls(clipboard: DataTransfer | null | undefined) {
  if (!clipboard) {
    return [];
  }

  const files = getClipboardImageFiles(clipboard);
  const dataUrls = await mapWithConcurrency(
    files,
    maxImageResolutionConcurrency,
    readClipboardImageFileAsDataUrl
  );
  return dataUrls.filter((dataUrl): dataUrl is string => Boolean(dataUrl));
}

async function readClipboardImageFileAsDataUrl(file: File) {
  const contentType = normalizeImageContentType(file.type);
  if (!contentType || file.size <= 0 || file.size > maxInlineImageBytes) {
    return null;
  }

  try {
    return `data:${contentType};base64,${await readBlobAsBase64(file)}`;
  } catch (error) {
    reportDiagnostic('warn', `Could not read pasted clipboard image: ${formatError(error)}`);
    return null;
  }
}

function getClipboardImageFiles(clipboard: DataTransfer) {
  const files: File[] = [];
  const seen = new Set<string>();
  const addFile = (file: File | null) => {
    if (!file || !normalizeImageContentType(file.type)) {
      return;
    }

    const key = `${file.name}|${file.type}|${file.size}|${file.lastModified}`;
    if (seen.has(key)) {
      return;
    }

    seen.add(key);
    files.push(file);
  };

  for (const item of Array.from(clipboard.items ?? [])) {
    if (item.kind === 'file' && normalizeImageContentType(item.type)) {
      addFile(item.getAsFile());
    }
  }

  for (const file of Array.from(clipboard.files ?? [])) {
    addFile(file);
  }

  return files;
}

function hasClipboardImage(clipboard: DataTransfer | null | undefined) {
  if (!clipboard) {
    return false;
  }

  for (const item of Array.from(clipboard.items ?? [])) {
    if (item.kind === 'file' && normalizeImageContentType(item.type)) {
      return true;
    }
  }

  return Array.from(clipboard.files ?? [])
    .some(file => normalizeImageContentType(file.type));
}

function promoteBackgroundImages(document: Document) {
  for (const element of Array.from(document.body.querySelectorAll<HTMLElement>('[style]'))) {
    const source = extractCssImageUrl(element.style.backgroundImage)
      ?? extractCssImageUrl(element.style.background);
    if (!source) {
      continue;
    }

    const image = document.createElement('img');
    image.setAttribute('src', source);
    element.after(image);
  }
}

function appendClipboardImages(document: Document, clipboardImages: string[]) {
  for (const source of clipboardImages.splice(0)) {
    const figure = document.createElement('figure');
    const image = document.createElement('img');
    image.setAttribute('src', source);
    figure.append(image);
    document.body.append(figure);
  }
}

function appendClipboardImageMarkers(
  document: Document,
  clipboardImages: string[],
  pasteImages: RichHtmlPasteImage[]
) {
  for (const source of clipboardImages.splice(0)) {
    if (!isDataImageUrl(source)) {
      continue;
    }

    const paragraph = document.createElement('p');
    const marker = createPasteImageMarker(pasteImages.length);
    paragraph.textContent = marker;
    document.body.append(paragraph);
    pasteImages.push({
      marker,
      dataUrl: source,
      fallbackFileName: `pasted-image-${pasteImages.length + 1}.png`,
    });
  }
}

function replaceImageWithPasteMarker(
  image: HTMLImageElement,
  dataUrl: string,
  fallbackFileName: string,
  pasteImages: RichHtmlPasteImage[]
) {
  const marker = createPasteImageMarker(pasteImages.length);
  const markerElement = image.ownerDocument.createElement('span');
  markerElement.textContent = marker;
  image.replaceWith(markerElement);
  pasteImages.push({
    marker,
    dataUrl,
    fallbackFileName,
  });
}

function createPasteImageMarker(index: number) {
  const token = globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  return `[[cognt-paste-image:${index}:${token}]]`;
}

function extractCssImageUrl(value: string) {
  const match = /url\((['"]?)(.*?)\1\)/i.exec(value);
  return match?.[2]?.trim() || null;
}

function resolveImageUrl(source: string, sourceUrl: string | null) {
  try {
    if (source.startsWith('//')) {
      const protocol = sourceUrl ? new URL(sourceUrl).protocol : '';
      return `${protocol === 'http:' || protocol === 'https:' ? protocol : 'https:'}${source}`;
    }

    return new URL(source, sourceUrl ?? window.location.href).toString();
  } catch {
    return null;
  }
}

function isHostReadableImageSource(source: string) {
  return /^(?:https?|file):/i.test(source) || /^[A-Za-z]:\//.test(source);
}

function isInvalidPastedUiImageSource(source: string) {
  try {
    const url = new URL(source);
    const host = url.hostname.toLowerCase();
    return (host === '0.0.0.0' || host === '0.0.0.1')
      && /\/icons\/[^/]+\.svg$/i.test(url.pathname);
  } catch {
    return false;
  }
}

function getImageFallbackFileName(image: HTMLImageElement, source: string) {
  const label = image.getAttribute('alt')?.trim()
    || image.getAttribute('title')?.trim()
    || getFileNameFromImageSource(source)
    || 'pasted-image';
  return label.length > 80 ? label.slice(0, 80).trimEnd() : label;
}

function getFileNameFromImageSource(source: string) {
  try {
    const url = new URL(source);
    return decodeURIComponent(url.pathname.split('/').filter(Boolean).at(-1) ?? '');
  } catch {
    return source.split(/[\\/]/).filter(Boolean).at(-1)?.split(/[?#]/, 1)[0] ?? '';
  }
}

function normalizeImageContentType(value: string) {
  const contentType = value.split(';', 1)[0].trim().toLowerCase();
  return contentType.startsWith('image/') ? contentType : null;
}

function isDataImageUrl(value: string) {
  return /^data:image\//i.test(value);
}

function extractClipboardHtml(rawHtml: string) {
  return extractClipboardMarkedFragment(rawHtml)
    ?? extractClipboardOffsetRange(rawHtml, 'StartFragment', 'EndFragment')
    ?? extractClipboardOffsetRange(rawHtml, 'StartHTML', 'EndHTML')
    ?? stripClipboardHtmlHeader(rawHtml);
}

function extractClipboardMarkedFragment(rawHtml: string) {
  const startMarker = '<!--StartFragment-->';
  const endMarker = '<!--EndFragment-->';
  const start = rawHtml.indexOf(startMarker);
  const end = rawHtml.indexOf(endMarker, start + startMarker.length);
  if (start < 0 || end <= start) {
    return null;
  }

  return rawHtml.slice(start + startMarker.length, end);
}

function extractClipboardOffsetRange(rawHtml: string, startKey: string, endKey: string) {
  const start = getClipboardOffset(rawHtml, startKey);
  const end = getClipboardOffset(rawHtml, endKey);
  if (start !== null && end !== null && start >= 0 && end > start && end <= rawHtml.length) {
    return rawHtml.slice(start, end);
  }

  return null;
}

function stripClipboardHtmlHeader(rawHtml: string) {
  if (!/^Version:/i.test(rawHtml)) {
    return rawHtml;
  }

  const match = /<!doctype\s+html\b|<html\b|<body\b|<[a-z][\w:-]*(?:\s|>|\/)/i.exec(rawHtml);
  return match ? rawHtml.slice(match.index) : rawHtml;
}

function getClipboardSourceUrl(rawHtml: string) {
  const match = /^SourceURL:(.+)$/im.exec(rawHtml);
  return match?.[1]?.trim() || null;
}

function getClipboardOffset(rawHtml: string, key: string) {
  const match = new RegExp(`${key}:(\\d+)`, 'i').exec(rawHtml);
  if (!match) {
    return null;
  }

  const value = Number.parseInt(match[1], 10);
  return Number.isFinite(value) ? value : null;
}

function getPlainTextFromHtml(html: string) {
  const document = new DOMParser().parseFromString(extractClipboardHtml(html), 'text/html');
  return document.body.textContent?.trim() ?? '';
}

function createPlainTextHtml(text: string) {
  const document = new DOMParser().parseFromString('', 'text/html');
  for (const line of text.split(/\r?\n/)) {
    const paragraph = document.createElement('p');
    paragraph.textContent = line;
    document.body.append(paragraph);
  }

  return document.body.innerHTML;
}

function formatError(error: unknown) {
  if (error instanceof DOMException && error.name === 'AbortError') {
    return 'timed out';
  }

  return error instanceof Error ? error.message : String(error);
}
