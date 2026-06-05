import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import type { TextSelection } from '../cloze';
import type { ExtractReplacement } from '../editor/extract-replacement-types';

export type {
  ExtractBlocksReplacement,
  ExtractReplacement,
  ExtractTextReplacement,
  ExtractTextReplacementPoint,
  ExtractTextReplacementRange,
  ExtractTextRangeReplacement,
} from '../editor/extract-replacement-types';

export type ExtractSourceAction = 'copy' | 'replace';

export type PendingExtract = {
  generation: number;
  sourcePath: string;
  replacement: ExtractReplacement;
  sourceAction: ExtractSourceAction;
};

export type CreateExtractCommandMessage = {
  type: 'createExtract';
  sourcePath: string;
  selectedContent: string;
  selectedPlainText: string;
};

export type CreateExtractCommandHost = {
  isInputLocked(): boolean;
  getCurrentDoc(): Doc | null;
  getCurrentPath(): string;
  getLoadGeneration(): number;
  getEditor(): PageEditor;
  getTextSelection(): TextSelection | null | undefined;
  getBlockSelectionBlockIds(): string[];
  createExtract(message: CreateExtractCommandMessage): void;
  showStatus(message: string): void;
};
