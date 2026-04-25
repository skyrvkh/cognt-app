import type {
  Doc,
  DraftModel,
} from '@blocksuite/store';
import {
  cogntReferencesFlavour,
  type CogntReferenceItem,
  type CogntReferencesBlockModel,
  type CogntReferencesBlockProps,
} from './blocks/references';

const openReferenceDocumentEvent = 'cognt-open-reference-document';
const requiredTrailingEditableBlocks = 2;

export type DocumentLinkItem = {
  openRelativePath: string;
  displayTitle: string;
  snippet: string;
  matchedText: string;
  updatedAt?: number | null;
};

export type DocumentLinksPayload = {
  incoming?: DocumentLinkItem[] | null;
  outgoing?: DocumentLinkItem[] | null;
};

export type DocumentLinksControllerOptions = {
  container?: HTMLElement | null;
  getDoc(): Doc | null;
  openDocument(relativePath: string): void | Promise<void>;
};

type NoteBlockModel = DraftModel & {
  id: string;
  flavour: string;
  children: DraftModel[];
};

export class DocumentLinksController {
  private links: DocumentLinksPayload = { incoming: [], outgoing: [] };

  public constructor(private readonly options: DocumentLinksControllerOptions) {
    if (this.options.container) {
      this.options.container.replaceChildren();
      this.options.container.style.display = 'none';
    }

    window.addEventListener(openReferenceDocumentEvent, this.handleOpenReferenceDocument);
  }

  public setLinks(payload: DocumentLinksPayload) {
    this.links = {
      incoming: payload.incoming ?? [],
      outgoing: payload.outgoing ?? [],
    };
    this.ensureReferencesBlock();
  }

  public ensureReferencesBlock(doc = this.options.getDoc()) {
    if (!doc) {
      return false;
    }

    const note = getNoteBlock(doc);
    if (!note) {
      return false;
    }

    const props = this.createBlockProps();
    const referencesBlocks = note.children.filter(isReferencesBlock);
    const finalChild = note.children.at(-1) ?? null;
    const primaryBlock = referencesBlocks.length === 1 && referencesBlocks[0] === finalChild
      ? referencesBlocks[0] as CogntReferencesBlockModel
      : null;
    const trailingEditableBlocks = countTrailingEditableBlocks(
      note.children.filter(child => child.flavour !== cogntReferencesFlavour)
    );
    const needsRebuild =
      !primaryBlock
      || referencesBlocks.length !== 1
      || trailingEditableBlocks < requiredTrailingEditableBlocks;

    if (needsRebuild) {
      for (const block of referencesBlocks) {
        doc.deleteBlock(block as DraftModel, { deleteChildren: true });
      }

      ensureTrailingEditableBlocks(doc, note);
      const blockId = doc.addBlock(
        cogntReferencesFlavour as never,
        props as never,
        note.id
      );
      return Boolean(blockId);
    }

    if (!hasSameReferences(primaryBlock, props)) {
      doc.updateBlock(primaryBlock, props as never);
      return true;
    }

    return false;
  }

  private createBlockProps(): CogntReferencesBlockProps {
    return {
      title: 'References',
      incoming: normalizeLinkItems(this.links.incoming, 'incoming'),
      outgoing: normalizeLinkItems(this.links.outgoing, 'outgoing'),
    };
  }

  private readonly handleOpenReferenceDocument = (event: Event) => {
    const detail = (event as CustomEvent<{ relativePath?: unknown }>).detail;
    const relativePath = typeof detail?.relativePath === 'string'
      ? detail.relativePath.trim()
      : '';

    if (relativePath.length === 0) {
      return;
    }

    void this.options.openDocument(relativePath);
  };
}

function normalizeLinkItems(
  items: DocumentLinkItem[] | null | undefined,
  fallbackDirection: CogntReferenceItem['direction']
) {
  return (items ?? [])
    .map(item => normalizeLinkItem(item, fallbackDirection))
    .filter((item): item is CogntReferenceItem => item !== null);
}

function normalizeLinkItem(
  item: DocumentLinkItem,
  fallbackDirection: CogntReferenceItem['direction']
): CogntReferenceItem | null {
  const direction = fallbackDirection;
  const openRelativePath = cleanString(item.openRelativePath);
  const displayTitle = cleanString(item.displayTitle) ?? openRelativePath;

  if (!openRelativePath || !displayTitle) {
    return null;
  }

  return {
    direction,
    openRelativePath,
    displayTitle,
    snippet: cleanString(item.snippet) ?? '',
    matchedText: cleanString(item.matchedText) ?? displayTitle,
  };
}

function getNoteBlock(doc: Doc): NoteBlockModel | null {
  const note = doc.root?.children.find(child => child.flavour === 'affine:note') ?? null;
  return note ? note as NoteBlockModel : null;
}

function isReferencesBlock(model: DraftModel) {
  return model.flavour === cogntReferencesFlavour;
}

function ensureTrailingEditableBlocks(doc: Doc, note: NoteBlockModel) {
  let trailingEditableBlocks = countTrailingEditableBlocks(note.children);

  while (trailingEditableBlocks < requiredTrailingEditableBlocks) {
    doc.addBlock('affine:paragraph' as never, {}, note.id);
    trailingEditableBlocks++;
  }
}

function countTrailingEditableBlocks(children: DraftModel[]) {
  let count = 0;

  for (let index = children.length - 1; index >= 0; index--) {
    if (!isEmptyParagraph(children[index])) {
      break;
    }

    count++;
  }

  return count;
}

function isEmptyParagraph(model: DraftModel) {
  if (model.flavour !== 'affine:paragraph') {
    return false;
  }

  if (model.children.length > 0) {
    return false;
  }

  const text = (model as {
    text?: {
      length?: number;
      toDelta?: () => Array<{ insert?: unknown }>;
    };
  }).text;

  if (!text) {
    return true;
  }

  if (typeof text.length === 'number') {
    return text.length === 0;
  }

  if (typeof text.toDelta !== 'function') {
    return true;
  }

  return !text.toDelta().some(delta => {
    if (typeof delta.insert === 'string') {
      return delta.insert.trim().length > 0;
    }

    return delta.insert != null;
  });
}

function hasSameReferences(
  block: CogntReferencesBlockModel,
  props: CogntReferencesBlockProps
) {
  return block.title === props.title
    && JSON.stringify(block.incoming ?? []) === JSON.stringify(props.incoming)
    && JSON.stringify(block.outgoing ?? []) === JSON.stringify(props.outgoing);
}

function cleanString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0
    ? value.trim()
    : null;
}
