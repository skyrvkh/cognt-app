import {
  BlockModel,
  defineBlockSchema,
} from '@blocksuite/store';

export type CogntContentBlockModel<TProps extends object, TFlavour extends string> = BlockModel<TProps>
  & TProps
  & { flavour: TFlavour };

export function createContentBlockSchema<TProps extends object, TFlavour extends string>(
  flavour: TFlavour,
  createProps: () => TProps
) {
  return defineBlockSchema({
    flavour,
    metadata: {
      version: 1,
      role: 'content',
      parent: ['affine:note'],
      children: [],
    },
    props: () => createProps(),
    toModel: () => new BlockModel<TProps>(),
  });
}
