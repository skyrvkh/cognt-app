import type { PageEditor } from '@blocksuite/presets';
import type { BlockModel, Doc } from '@blocksuite/store';
import { reportDiagnostic, waitForAnimationFrames } from '../host';
import {
  appendImageAttachmentBlock,
  insertImageAttachmentBlock,
} from './attachment-insertion';
import { createLocalImageAttachmentPayload } from './image-attachments';
import {
  base64ToUint8Array,
  inferFileExtension,
  normalizeSupportedImageContentType,
  sniffImageContentType,
} from './image-data';
import type { RichHtmlPasteImage } from './rich-html-paste';

type RichHtmlPasteImageInsertionOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  persistAttachmentMutation(): Promise<void>;
};

type MarkerTarget = {
  model: BlockModel;
  markerIndex: number;
  markerOnly: boolean;
};

type EditableTextModel = BlockModel & {
  text?: {
    toString?: () => string;
    replace?: (index: number, length: number, content: string) => void;
  };
};

export async function insertRichHtmlPasteImages(
  options: RichHtmlPasteImageInsertionOptions,
  images: RichHtmlPasteImage[]
) {
  if (images.length === 0) {
    return 0;
  }

  await Promise.resolve();
  await waitForAnimationFrames(2);

  const doc = options.getCurrentDoc();
  if (!doc) {
    reportDiagnostic('warn', 'Rich HTML paste image insertion skipped: no active document.');
    return 0;
  }

  let inserted = 0;
  for (const image of images) {
    try {
      const payload = await createPasteImagePayload(image);
      if (!payload) {
        reportDiagnostic('warn', `Rich HTML paste image skipped: unsupported image data for ${image.fallbackFileName}.`);
        continue;
      }

      const target = findImageMarker(doc, image.marker);
      const blockId = target
        ? await insertImageAtMarker(doc, options.editor, target, image.marker, payload)
        : await appendImageAttachmentBlock(doc, options.editor, payload, () => false);
      if (blockId) {
        inserted++;
      }
    } catch (error) {
      reportDiagnostic('warn', `Rich HTML paste image insert failed: ${formatError(error)}`);
    }
  }

  if (inserted === 0) {
    reportDiagnostic('warn', `Rich HTML paste inserted 0 of ${images.length} resolved image(s).`);
    return 0;
  }

  reportDiagnostic('info', `Rich HTML paste inserted ${inserted} resolved image(s).`);
  await options.persistAttachmentMutation();
  return inserted;
}

async function createPasteImagePayload(image: RichHtmlPasteImage) {
  const parsed = parseDataImageUrl(image.dataUrl);
  if (!parsed) {
    return null;
  }

  const bytes = base64ToUint8Array(parsed.contentBase64);
  const contentType = sniffImageContentType(bytes)
    ?? normalizeSupportedImageContentType(parsed.contentType)
    ?? parsed.contentType;
  const fileName = ensureImageFileName(image.fallbackFileName, contentType);
  const sourceFile = new File([bytes], fileName, { type: contentType });
  return createLocalImageAttachmentPayload(sourceFile, fileName, parsed.contentBase64);
}

async function insertImageAtMarker(
  doc: Doc,
  editor: PageEditor,
  target: MarkerTarget,
  marker: string,
  payload: NonNullable<Awaited<ReturnType<typeof createPasteImagePayload>>>
) {
  const parent = doc.getParent(target.model);
  if (!parent) {
    return await appendImageAttachmentBlock(doc, editor, payload, () => false);
  }

  const modelIndex = parent.children.indexOf(target.model);
  if (modelIndex < 0) {
    return await appendImageAttachmentBlock(doc, editor, payload, () => false);
  }

  let insertIndex = modelIndex + 1;
  if (target.markerOnly) {
    insertIndex = modelIndex;
    doc.deleteBlock(target.model);
  } else {
    replaceTextMarker(target.model, target.markerIndex, marker);
  }

  return await insertImageAttachmentBlock(
    doc,
    editor,
    parent,
    Math.max(0, insertIndex),
    payload,
    () => false
  );
}

function findImageMarker(doc: Doc, marker: string): MarkerTarget | null {
  for (const model of getDocumentBlockModels(doc)) {
    const text = getEditableText(model);
    if (!text) {
      continue;
    }

    const plainText = text.toString?.() ?? '';
    const markerIndex = plainText.indexOf(marker);
    if (markerIndex < 0) {
      continue;
    }

    return {
      model,
      markerIndex,
      markerOnly: plainText.trim() === marker,
    };
  }

  return null;
}

function getDocumentBlockModels(doc: Doc) {
  const getBlocks = (doc as { getBlocks?: () => unknown[] }).getBlocks;
  if (typeof getBlocks !== 'function') {
    return [];
  }

  return getBlocks.call(doc)
    .map(block => (block as { model?: unknown }).model ?? block)
    .filter((model): model is BlockModel => typeof model === 'object' && model !== null);
}

function getEditableText(model: BlockModel) {
  const text = (model as EditableTextModel).text;
  return typeof text?.replace === 'function' && typeof text.toString === 'function'
    ? text
    : null;
}

function replaceTextMarker(model: BlockModel, markerIndex: number, marker: string) {
  const text = getEditableText(model);
  text?.replace?.(markerIndex, marker.length, '');
}

function parseDataImageUrl(dataUrl: string) {
  const match = /^data:(image\/[^;,]+)(?:;[^,]*)?;base64,(.*)$/is.exec(dataUrl);
  if (!match) {
    return null;
  }

  return {
    contentType: match[1].toLowerCase(),
    contentBase64: match[2],
  };
}

function ensureImageFileName(fallbackFileName: string, contentType: string) {
  const baseName = fallbackFileName
    .replace(/[\\/:*?"<>|]+/g, '-')
    .trim()
    .slice(0, 120)
    || 'pasted-image';
  if (/\.(?:png|jpe?g|webp|gif)$/i.test(baseName)) {
    return baseName;
  }

  return `${baseName}${inferFileExtension(contentType)}`;
}

function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
