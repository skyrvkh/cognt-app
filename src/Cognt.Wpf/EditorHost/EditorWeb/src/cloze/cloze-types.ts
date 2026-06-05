import type { Doc } from '@blocksuite/store';

type TextSelectionPoint = {
  blockId: string;
  index: number;
  length: number;
};

export type TextSelection = {
  from: TextSelectionPoint;
  to: TextSelectionPoint | null;
  start?: TextSelectionPoint;
  end?: TextSelectionPoint;
  isCollapsed(): boolean;
  isInSameBlock(): boolean;
};

export type ClozeContext = {
  doc: Doc | null;
  textSelection: TextSelection | null | undefined;
  editorRoot?: Node | null;
};

type ReplaceableText = {
  replace(index: number, length: number, content: string): void;
};

export type TextReplacementModel = {
  id?: string;
  flavour?: string;
  text?: ReplaceableText;
};

export type LatexBlockModel = {
  id?: string;
  flavour?: string;
  latex?: string;
  latex$?: {
    value?: string;
  };
};

export type InlineLatexNode = Element & {
  delta?: {
    attributes?: {
      latex?: string;
    };
  };
  deltaLatex?: string;
  editor?: {
    insertText(range: { index: number; length: number }, content: string): void;
  };
  startOffset?: number;
  endOffset?: number;
};

export type NativeClozeSelection = {
  blockId: string;
  plainText: string;
};

export type TextSelectionRange = {
  blockId: string;
  index: number;
  length: number;
};

export type InlineLatexSelectionCandidate = {
  node: InlineLatexNode;
  blockId: string;
  latex: string;
  startOffset: number;
  length: number;
};

export type LatexBlockSelectionCandidate = {
  doc: Doc;
  element: Element;
  model: LatexBlockModel;
  blockId: string;
  latex: string;
};

export type NativeCodeBlockSelectionCandidate = {
  model: TextReplacementModel;
  range: {
    index: number;
    length: number;
  };
};
