import { AffineSchemas } from '@blocksuite/blocks/schemas';
import {
  BlockViewExtension,
  FlavourExtension,
} from '@blocksuite/block-std';
import { literal } from 'lit-html/static.js';
import { createContentBlockSchema, type CogntContentBlockModel } from './block-schema-helpers';
import {
  CogntReferencesBlockSchema,
  cogntReferencesFlavour,
} from './references-schema';

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

export type CogntPropertiesBlockModel = CogntContentBlockModel<
  CogntPropertiesBlockProps,
  typeof cogntPropertiesFlavour
>;

const CogntPropertiesBlockSchema = createContentBlockSchema(
  cogntPropertiesFlavour,
  () => ({
    title: 'Properties',
    properties: [] as CogntPropertyRow[],
  }),
);

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
            cogntReferencesFlavour,
          ],
        },
      };
    }),
    CogntPropertiesBlockSchema,
    CogntReferencesBlockSchema,
  ];
}
