import type { BlockSnapshot } from '@blocksuite/store';

export const cogntBlockSuiteDocumentKind = 'cognt-blocksuite-doc';
export const cogntBlockSuiteDocumentVersion = 1;

export function createBlockSuiteDocumentJsonFromBlockSnapshots(
  children: BlockSnapshot[],
  title: string
) {
  const pageId = createBlockId();
  const surfaceId = createBlockId();
  const noteId = createBlockId();

  return `${JSON.stringify(
    {
      kind: cogntBlockSuiteDocumentKind,
      version: cogntBlockSuiteDocumentVersion,
      snapshot: {
        type: 'page',
        meta: {
          id: pageId,
          title,
          createDate: Date.now(),
          tags: [],
        },
        blocks: {
          type: 'block',
          id: pageId,
          flavour: 'affine:page',
          version: 2,
          props: {
            title: createBlockSuiteTextValue(title),
          },
          children: [
            {
              type: 'block',
              id: surfaceId,
              flavour: 'affine:surface',
              version: 5,
              props: {},
              children: [],
            },
            {
              type: 'block',
              id: noteId,
              flavour: 'affine:note',
              version: 1,
              props: {
                xywh: '[0,0,800,95]',
                background: '--affine-note-background-blue',
                index: 'a0',
                hidden: false,
                displayMode: 'both',
                edgeless: {
                  style: {
                    borderRadius: 8,
                    borderSize: 4,
                    borderStyle: 'none',
                    shadowType: '--affine-note-shadow-box',
                  },
                },
              },
              children,
            },
          ],
        },
      },
    },
    null,
    2
  )}\n`;
}

export function createBlockSuiteDocumentJsonFromPlainText(text: string, title: string) {
  const paragraphId = createBlockId();
  const paragraph: BlockSnapshot = {
    type: 'block',
    id: paragraphId,
    flavour: 'affine:paragraph',
    version: 1,
    props: {
      type: 'text',
      text: createBlockSuiteTextValue(text),
      collapsed: false,
    },
    children: [],
  };

  return createBlockSuiteDocumentJsonFromBlockSnapshots([paragraph], title);
}

export function createBlockSuiteTextValue(value: string) {
  return {
    '$blocksuite:internal:text$': true,
    delta: value.length > 0 ? [{ insert: value }] : [],
  };
}

export function createBlockId() {
  return `cognt-${crypto.randomUUID().replaceAll('-', '').slice(0, 12)}`;
}

export function createTitleFromPlainText(text: string, fallback: string) {
  const title = text.replace(/\s+/g, ' ').trim();
  if (title.length === 0) {
    return fallback;
  }

  return title.length <= 60 ? title : title.slice(0, 60).trimEnd();
}

export function normalizeSelectionText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

export function isBlockSuiteJsonPath(path: string | null | undefined) {
  return typeof path === 'string' && /\.json$/i.test(path);
}

export function getSnapshotTextValue(snapshot: BlockSnapshot) {
  const text = snapshot.props.text;
  return isRecord(text) ? text : null;
}

export function getSnapshotTextDelta(textValue: Record<string, unknown>) {
  return Array.isArray(textValue.delta) ? textValue.delta : [];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
