import { AffineSchemas } from '@blocksuite/blocks/schemas';
import {
  CogntReferencesBlockSchema,
  cogntReferencesFlavour,
} from './references-schema';

export const cogntPropertiesFlavour = 'cognt:properties';

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
            cogntReferencesFlavour,
          ],
        },
      };
    }),
    CogntReferencesBlockSchema,
  ];
}
