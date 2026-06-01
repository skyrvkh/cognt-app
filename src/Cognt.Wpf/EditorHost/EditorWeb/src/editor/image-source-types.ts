import type { BlockModel } from '@blocksuite/store';

export type ImageBlockModel = BlockModel & {
  id: string;
  sourceId?: unknown;
  props?: {
    sourceId?: unknown;
  };
};

export type ImageHydrationTarget = {
  sourceId: string;
  imageSource: string;
  models: ImageBlockModel[];
};
