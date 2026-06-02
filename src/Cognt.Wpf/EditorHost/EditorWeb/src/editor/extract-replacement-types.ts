export type ExtractTextReplacementPoint = {
  blockId: string;
  index: number;
  length: number;
};

export type ExtractTextReplacement = {
  kind: 'text';
  blockId: string;
  index: number;
  length: number;
};

export type ExtractTextReplacementRange = ExtractTextReplacement;

export type ExtractBlocksReplacement = {
  kind: 'blocks';
  blockIds: string[];
};

export type ExtractTextRangeReplacement = {
  kind: 'text-range';
  start: ExtractTextReplacementPoint;
  end: ExtractTextReplacementPoint;
  blockIds: string[];
};

export type ExtractReplacement =
  | ExtractTextReplacement
  | ExtractTextRangeReplacement
  | ExtractBlocksReplacement;
