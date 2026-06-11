import { normalizeRichHtmlForPaste } from '../src/editor/rich-html-paste';
import { FetchUtils } from '@blocksuite/affine-shared/adapters';

const inlinedPngDataUrl = 'data:image/png;base64,UE5HREFUQQ==';
const nativePngDataUrl = 'data:image/png;base64,TkFUSVZF';
const hostPngDataUrl = 'data:image/png;base64,SE9TVA==';

const requests: string[] = [];
let activeSlowFetches = 0;
let maxActiveSlowFetches = 0;

globalThis.fetch = async input => {
  const url = typeof input === 'string' ? input : input.url;
  requests.push(url);

  if (url.includes('slow')) {
    activeSlowFetches++;
    maxActiveSlowFetches = Math.max(maxActiveSlowFetches, activeSlowFetches);
    await new Promise(resolve => setTimeout(resolve, 10));
    activeSlowFetches--;
  }

  if (url.includes('missing')) {
    return new Response('', { status: 404 });
  }

  return new Response(new Blob(['PNGDATA'], { type: 'image/png' }), {
    status: 200,
    headers: { 'content-type': 'image/png' },
  });
};

await runTests();

async function runTests() {
  const wikiHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<h1>SAP HANA Cloud</h1><p><strong>Bold</strong> text</p>' +
        '<img src="//upload.wikimedia.org/example.png" ' +
        'srcset="//upload.wikimedia.org/example-small.png 1x, ' +
        '//upload.wikimedia.org/example-large.png 2x">',
      'https://en.wikipedia.org/wiki/SAP_HANA'
    ),
    plainText: 'SAP HANA Cloud\nBold text',
    readNativeClipboardImage: failIfCalled,
  });

  assert(wikiHtml.includes('<strong>Bold</strong>'), 'preserves rich text formatting');
  assert(wikiHtml.includes(`src="${inlinedPngDataUrl}"`), 'inlines Wikipedia-style image URLs');
  assert(FetchUtils.fetchable(inlinedPngDataUrl), 'BlockSuite treats inlined images as fetchable');
  assert(
    requests.includes('https://upload.wikimedia.org/example.png'),
    'resolves protocol-relative image URLs'
  );

  const nonAsciiHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<p><strong>Today’s Enterprise</strong> café</p>',
      'https://example.test/non-ascii'
    ),
    plainText: 'Today’s Enterprise café',
    readNativeClipboardImage: async () => null,
  });

  assert(!nonAsciiHtml.includes('Version:0.9'), 'strips CF_HTML header for non-ASCII HTML');
  assert(
    nonAsciiHtml.includes('<strong>Today’s Enterprise</strong>'),
    'preserves non-ASCII formatted CF_HTML fragment'
  );

  const backgroundHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<div style="background-image: url(\'/images/slide.png\')">Strategic Direction</div>',
      'https://example.test/article/page'
    ),
    plainText: 'Strategic Direction',
    readNativeClipboardImage: failIfCalled,
  });

  assert(backgroundHtml.includes('<img'), 'promotes CSS background image to an image element');
  assert(backgroundHtml.includes(`src="${inlinedPngDataUrl}"`), 'inlines promoted background image');
  assert(
    requests.includes('https://example.test/images/slide.png'),
    'resolves promoted background image against SourceURL'
  );

  const duplicateRemoteHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<p>Duplicate image</p><img src="/shared.png"><img src="https://example.test/shared.png">',
      'https://example.test/article/page'
    ),
    plainText: 'Duplicate image',
    readNativeClipboardImage: failIfCalled,
  });

  assert(
    requests.filter(url => url === 'https://example.test/shared.png').length === 1,
    'caches duplicate remote image fetches during one paste'
  );
  assert(
    countOccurrences(duplicateRemoteHtml, `src="${inlinedPngDataUrl}"`) === 2,
    'uses cached remote image data for duplicate image tags'
  );

  activeSlowFetches = 0;
  maxActiveSlowFetches = 0;
  const parallelHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      Array.from({ length: 6 }, (_, index) => `<img src="/slow-${index}.png">`).join(''),
      'https://example.test/article/page'
    ),
    plainText: 'Parallel images',
    readNativeClipboardImage: failIfCalled,
  });

  assert(maxActiveSlowFetches > 1, 'resolves pasted image sources in parallel');
  assert(maxActiveSlowFetches <= 4, 'bounds parallel pasted image source reads');
  assert(
    countOccurrences(parallelHtml, `src="${inlinedPngDataUrl}"`) === 6,
    'inlines every image resolved through the parallel path'
  );

  const uiIconHtml = await normalizeRichHtmlForPaste({
    html: '<p>Image chrome</p><img src="https://0.0.0.1/icons/expand-image.svg">',
    plainText: 'Image chrome',
    readImageSourceAsDataUrl: failHostIfCalled,
    readNativeClipboardImage: async () => null,
  });

  assert(!uiIconHtml.includes('expand-image.svg'), 'removes invalid copied UI image icons');

  let nativeFallbackCalls = 0;
  const fallbackHtml = await normalizeRichHtmlForPaste({
    html: '<p><strong>No image tag</strong></p>',
    plainText: 'No image tag',
    readNativeClipboardImage: async () => {
      nativeFallbackCalls++;
      return nativePngDataUrl;
    },
  });

  assert(nativeFallbackCalls === 1, 'reads native image when HTML has no accessible image');
  assert(fallbackHtml.includes('<strong>No image tag</strong>'), 'keeps formatted fallback text');
  assert(fallbackHtml.includes(`src="${nativePngDataUrl}"`), 'appends native clipboard image');

  let brokenFallbackCalls = 0;
  let hostFallbackCalls = 0;
  const brokenImageHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml('<p>Broken remote image</p><img src="/missing.png">', 'https://example.test/a'),
    plainText: 'Broken remote image',
    readImageSourceAsDataUrl: async (imageSource, fallbackFileName) => {
      hostFallbackCalls++;
      assert(imageSource === 'https://example.test/missing.png', 'passes resolved image URL to host fallback');
      assert(fallbackFileName === 'missing.png', 'passes image filename to host fallback');
      return hostPngDataUrl;
    },
    readNativeClipboardImage: async () => {
      brokenFallbackCalls++;
      return nativePngDataUrl;
    },
  });

  assert(hostFallbackCalls === 1, 'reads host image source when remote image cannot be inlined');
  assert(brokenFallbackCalls === 0, 'does not read native image when host source fallback succeeds');
  assert(
    brokenImageHtml.includes(`src="${hostPngDataUrl}"`),
    'uses host image source data for broken remote image'
  );

  let duplicateHostFallbackCalls = 0;
  const duplicateHostFallbackHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<p>Duplicate host fallback</p><img src="/missing-duplicate.png"><img src="/missing-duplicate.png">',
      'https://example.test/a'
    ),
    plainText: 'Duplicate host fallback',
    readImageSourceAsDataUrl: async imageSource => {
      duplicateHostFallbackCalls++;
      assert(
        imageSource === 'https://example.test/missing-duplicate.png',
        'passes resolved duplicate image URL to host fallback'
      );
      return hostPngDataUrl;
    },
    readNativeClipboardImage: failIfCalled,
  });

  assert(duplicateHostFallbackCalls === 1, 'caches duplicate host image fallback reads');
  assert(
    countOccurrences(duplicateHostFallbackHtml, `src="${hostPngDataUrl}"`) === 2,
    'uses cached host image data for duplicate fallback image tags'
  );

  let nativeRemoteFallbackCalls = 0;
  const nativeRemoteFallbackHtml = await normalizeRichHtmlForPaste({
    html: createCfHtml(
      '<p>Broken remote image with no host fallback</p><img src="/missing-again.png">',
      'https://example.test/a'
    ),
    plainText: 'Broken remote image with no host fallback',
    readImageSourceAsDataUrl: async () => null,
    readNativeClipboardImage: async () => {
      nativeRemoteFallbackCalls++;
      return nativePngDataUrl;
    },
  });

  assert(nativeRemoteFallbackCalls === 1, 'reads native image when remote and host fallback fail');
  assert(
    nativeRemoteFallbackHtml.includes(`src="${nativePngDataUrl}"`),
    'uses native clipboard image when remote and host fallback fail'
  );
}

async function failIfCalled() {
  throw new Error('native clipboard fallback should not be read for fetchable images');
}

async function failHostIfCalled() {
  throw new Error('host image fallback should not be read for invalid UI icons');
}

function createCfHtml(fragment: string, sourceUrl: string) {
  const body = `<!DOCTYPE html><html><body><!--StartFragment-->${fragment}<!--EndFragment--></body></html>`;
  const headerTemplate =
    'Version:0.9\r\n' +
    'StartHTML:0000000000\r\n' +
    'EndHTML:0000000000\r\n' +
    'StartFragment:0000000000\r\n' +
    'EndFragment:0000000000\r\n' +
    'SourceURL:' + sourceUrl + '\r\n';

  const startHtml = getUtf8Length(headerTemplate);
  const endHtml = startHtml + getUtf8Length(body);
  const startFragment = startHtml + getUtf8Length('<!DOCTYPE html><html><body><!--StartFragment-->');
  const endFragment = startFragment + getUtf8Length(fragment);

  return headerTemplate
    .replace('StartHTML:0000000000', `StartHTML:${String(startHtml).padStart(10, '0')}`)
    .replace('EndHTML:0000000000', `EndHTML:${String(endHtml).padStart(10, '0')}`)
    .replace(
      'StartFragment:0000000000',
      `StartFragment:${String(startFragment).padStart(10, '0')}`
    )
    .replace('EndFragment:0000000000', `EndFragment:${String(endFragment).padStart(10, '0')}`) + body;
}

function getUtf8Length(value: string) {
  return new TextEncoder().encode(value).length;
}

function countOccurrences(value: string, needle: string) {
  return value.split(needle).length - 1;
}

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}
