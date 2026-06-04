export type TextSelectionPointSnapshot = {
  blockId: string;
  index: number;
  length: number;
};

export type TextSelectionSnapshot = {
  from: TextSelectionPointSnapshot;
  to: TextSelectionPointSnapshot | null;
  start?: TextSelectionPointSnapshot;
  end?: TextSelectionPointSnapshot;
};

export type BlockSelectionSnapshot = {
  blockId: string;
};

export type CurrentSelection = {
  plainText: string;
};

export type TextSelectionExtract = {
  selectedPlainText: string;
  selectedContent: string | null;
  blockIds: string[];
};

export type BlockSelectionExtract = {
  selectedPlainText: string;
  selectedContent: string | null;
};
