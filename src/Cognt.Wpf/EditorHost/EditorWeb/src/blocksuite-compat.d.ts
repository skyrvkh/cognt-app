import type { BlockService, EditorHost } from '@blocksuite/block-std';
import type { PageEditor as BlocksuitePageEditor } from '@blocksuite/presets';
import type { BlockModel, Doc, DraftModel } from '@blocksuite/store';

declare module '@blocksuite/block-std' {
  interface BlockComponent<
    Model extends BlockModel = BlockModel,
    Service extends BlockService = BlockService,
    WidgetName extends string = string,
  > extends HTMLElement {
    contentEditable: string;
    readonly updateComplete: Promise<unknown>;
    requestUpdate(name?: string, oldValue?: unknown): void;
  }
}

declare module '@blocksuite/presets' {
  interface PageEditor extends HTMLElement {
    id: string;
    shadowRoot: ShadowRoot | null;
    readonly updateComplete: Promise<unknown>;
  }
}

declare module '@blocksuite/store' {
  interface Doc {
    addBlock(
      flavour: string,
      blockProps?: Record<string, unknown>,
      parent?: string | DraftModel | null,
      parentIndex?: number,
    ): string;
  }
}

export type CogntSlashMenuRootComponent = import('@blocksuite/block-std').BlockComponent & {
  doc: Doc;
  host: EditorHost;
};

export type CogntPageEditor = BlocksuitePageEditor & HTMLElement;
