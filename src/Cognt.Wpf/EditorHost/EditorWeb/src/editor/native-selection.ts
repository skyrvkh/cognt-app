import type { TextSelection } from '../cloze';

type NativeSelectionEditor = {
  contains(node: Node): boolean;
  std?: {
    range?: {
      rangeToTextSelection?(range: Range, reverse?: boolean): unknown;
    };
  };
};

export function createTextSelectionFromNativeRange(
  editor: NativeSelectionEditor
): TextSelection | null {
  const range = getNativeSelectionRange();
  if (!range || !isNodeInsideEditor(editor, range.commonAncestorContainer)) {
    return null;
  }

  return (editor.std?.range?.rangeToTextSelection?.(
    range,
    isNativeSelectionReversed()
  ) as TextSelection | null | undefined) ?? null;
}

function getNativeSelectionRange() {
  const selection = document.getSelection();
  return selection && selection.rangeCount > 0
    ? selection.getRangeAt(0)
    : null;
}

function isNativeSelectionReversed() {
  const selection = document.getSelection();
  if (!selection?.anchorNode || !selection.focusNode) {
    return false;
  }

  if (selection.anchorNode === selection.focusNode) {
    return selection.anchorOffset > selection.focusOffset;
  }

  return Boolean(
    selection.anchorNode.compareDocumentPosition(selection.focusNode)
    & Node.DOCUMENT_POSITION_PRECEDING
  );
}

function isNodeInsideEditor(editor: NativeSelectionEditor, node: Node) {
  let current: Node | null = node;
  while (current) {
    if (editor.contains(current)) {
      return true;
    }

    const root = current.getRootNode();
    if (root instanceof ShadowRoot) {
      current = root.host;
      continue;
    }

    return false;
  }

  return false;
}
