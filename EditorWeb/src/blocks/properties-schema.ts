import { AffineSchemas } from '@blocksuite/blocks/schemas';
import {
  BlockViewExtension,
  FlavourExtension,
} from '@blocksuite/block-std';
import {
  BlockModel,
  defineBlockSchema,
} from '@blocksuite/store';
import { literal } from 'lit/static-html.js';

export const cogntPropertiesFlavour = 'cognt:properties';

export const cogntPropertiesTagName = 'cognt-properties-block';

export type CogntPropertyRow = {
  id: string;
  name: string;
  value: string;
};

export type CogntPropertiesBlockProps = {
  title: string;
  properties: CogntPropertyRow[];
};

export type CogntPropertiesBlockModel = BlockModel<CogntPropertiesBlockProps>
  & CogntPropertiesBlockProps
  & { flavour: typeof cogntPropertiesFlavour };

const CogntPropertiesBlockSchema = defineBlockSchema({
  flavour: cogntPropertiesFlavour,
  metadata: {
    version: 1,
    role: 'content',
    parent: ['affine:note'],
    children: [],
  },
  props: () => ({
    title: 'Properties',
    properties: [] as CogntPropertyRow[],
  }),
  toModel: () => new BlockModel<CogntPropertiesBlockProps>() as CogntPropertiesBlockModel,
});

export const CogntPropertiesBlockSpec = [
  FlavourExtension(cogntPropertiesFlavour),
  BlockViewExtension(cogntPropertiesFlavour as never, literal`cognt-properties-block`),
];

export function createCogntSchemas() {
  return [
    ...AffineSchemas.map(schema => {
      if (schema.model.flavour !== 'affine:note') {
        return schema;
      }

      return {
        ...schema,
        model: {
          ...schema.model,
          children: [
            ...(schema.model.children ?? []),
            cogntPropertiesFlavour,
          ],
        },
      };
    }),
    CogntPropertiesBlockSchema,
  ];
}
