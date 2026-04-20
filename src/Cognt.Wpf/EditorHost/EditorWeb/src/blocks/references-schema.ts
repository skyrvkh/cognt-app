import {
  BlockViewExtension,
  FlavourExtension,
} from '@blocksuite/block-std';
import { literal } from 'lit-html/static.js';
import { createContentBlockSchema, type CogntContentBlockModel } from './block-schema-helpers';

export const cogntReferencesFlavour = 'cognt:references';

export const cogntReferencesTagName = 'cognt-references-block';

export type CogntReferenceDirection = 'incoming' | 'outgoing';

export type CogntReferenceItem = {
  direction: CogntReferenceDirection;
  openRelativePath: string;
  displayTitle: string;
  snippet: string;
  matchedText: string;
};

export type CogntReferencesBlockProps = {
  title: string;
  incoming: CogntReferenceItem[];
  outgoing: CogntReferenceItem[];
};

export type CogntReferencesBlockModel = CogntContentBlockModel<
  CogntReferencesBlockProps,
  typeof cogntReferencesFlavour
>;

export const CogntReferencesBlockSchema = createContentBlockSchema(
  cogntReferencesFlavour,
  () => ({
    title: 'References',
    incoming: [] as CogntReferenceItem[],
    outgoing: [] as CogntReferenceItem[],
  }),
);

export const CogntReferencesBlockSpec = [
  FlavourExtension(cogntReferencesFlavour),
  BlockViewExtension(cogntReferencesFlavour as never, literal`cognt-references-block`),
];
