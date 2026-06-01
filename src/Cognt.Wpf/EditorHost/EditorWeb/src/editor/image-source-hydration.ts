import type { Doc } from '@blocksuite/store';
import { reportDiagnostic } from '../host';
import type { ReadAttachmentResult } from './contracts';
import { createBlobFromReadAttachmentResult } from './image-source-blobs';
import {
  getImageSourceId,
  isImageBlockModel,
  setImageSourceId,
} from './image-source-models';
import {
  normalizeImageSourceForHost,
  sanitizeAttachmentTarget,
} from './image-source-paths';
import type { ImageHydrationTarget } from './image-source-types';

export async function hydrateAttachmentImageBlocks(
  doc: Doc,
  sourcePath: string,
  collectionRoot: string,
  readAttachment: (message: {
    type: 'readAttachment';
    sourcePath: string;
    imageSource: string;
  }) => Promise<ReadAttachmentResult>
) {
  if (!sourcePath) {
    return;
  }

  for (const target of collectImageHydrationTargets(doc, collectionRoot)) {
    if (await doc.blobSync.get(target.sourceId)) {
      continue;
    }

    try {
      const result = await readAttachment({
        type: 'readAttachment',
        sourcePath,
        imageSource: target.imageSource,
      });
      if (!result.ok) {
        const message = result.error ?? 'The image attachment could not be loaded.';
        reportDiagnostic('warn', `Image hydration skipped for ${target.imageSource}: ${message}`);
        continue;
      }

      const blob = createBlobFromReadAttachmentResult(result, target.imageSource);
      if (!blob) {
        reportDiagnostic(
          'warn',
          `Image hydration skipped for ${target.imageSource}: missing image content.`
        );
        continue;
      }

      const hydratedSourceId = sanitizeAttachmentTarget(
        result.relativePath ?? target.imageSource
      );
      await doc.blobSync.set(hydratedSourceId, blob);
      applyHydratedImageSource(target, hydratedSourceId);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      reportDiagnostic('warn', `Image hydration failed for ${target.imageSource}: ${message}`);
    }
  }
}

function collectImageHydrationTargets(doc: Doc, collectionRoot: string) {
  const targets = new Map<string, ImageHydrationTarget>();

  for (const block of doc.getBlocks()) {
    const model = (block as { model?: unknown }).model ?? block;
    if (!isImageBlockModel(model)) {
      continue;
    }

    const sourceId = getImageSourceId(model);
    if (!sourceId) {
      continue;
    }

    const target = targets.get(sourceId);
    if (target) {
      target.models.push(model);
      continue;
    }

    targets.set(sourceId, {
      sourceId,
      imageSource: normalizeImageSourceForHost(sourceId, collectionRoot) ?? sourceId,
      models: [model],
    });
  }

  return targets.values();
}

function applyHydratedImageSource(target: ImageHydrationTarget, hydratedSourceId: string) {
  if (hydratedSourceId === target.sourceId) {
    return;
  }

  for (const model of target.models) {
    setImageSourceId(model, hydratedSourceId);
  }
}
