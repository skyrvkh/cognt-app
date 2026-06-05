import {
  AffineSlashMenuWidget,
  type AffineSlashMenuActionItem,
  type AffineSlashMenuContext,
  type AffineSlashMenuGroupDivider,
  type AffineSlashMenuItem,
  type AffineSlashMenuItemGenerator,
  type AffineSlashSubMenu,
} from '@blocksuite/blocks';
import type { Doc } from '@blocksuite/store';
import type { CogntSlashMenuRootComponent } from '../blocksuite-compat';
import { reportDiagnostic } from '../host';

const hiddenSlashMenuSections = new Set(['Style', 'Date', 'Actions']);
const hiddenSlashMenuItems = new Set(['Figma', 'GitHub', 'Github', 'Loom', 'Linked Doc', 'Embed Link', 'Image']);

export type LinkedNoteViewMode = 'inline' | 'embed' | 'native';

export type CogntSlashMenuEditor = {
  createLinkedNoteFromSlashMenu(
    viewMode: LinkedNoteViewMode,
    context: AffineSlashMenuContext
  ): Promise<void>;
  insertImageFromSlashMenu(context: AffineSlashMenuContext): Promise<void>;
};

type ConfigureCogntSlashMenuOptions = {
  getEditor(doc: Doc): CogntSlashMenuEditor | undefined;
};

type SlashMenuStaticItem =
  | AffineSlashMenuActionItem
  | AffineSlashMenuGroupDivider
  | AffineSlashSubMenu;

export function configureCogntSlashMenu(options: ConfigureCogntSlashMenuOptions) {
  const config = AffineSlashMenuWidget.DEFAULT_CONFIG;

  const runCreateLinkedNoteAction = async (
    viewMode: LinkedNoteViewMode,
    context: AffineSlashMenuContext
  ) => {
    const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
    const editor = options.getEditor(rootComponent.doc);

    if (!editor) {
      reportDiagnostic('error', 'Could not create linked note: no editor is registered for this document.');
      return;
    }

    await editor.createLinkedNoteFromSlashMenu(viewMode, context);
  };

  const runInsertImageAction = async (context: AffineSlashMenuContext) => {
    const rootComponent = context.rootComponent as unknown as CogntSlashMenuRootComponent;
    const editor = options.getEditor(rootComponent.doc);

    if (!editor) {
      reportDiagnostic('error', 'Could not add image: no editor is registered for this document.');
      return;
    }

    await editor.insertImageFromSlashMenu(context);
  };

  const filterSlashMenuStaticItem = (item: SlashMenuStaticItem): SlashMenuStaticItem | null => {
    if (isNamedSlashMenuItem(item) && hiddenSlashMenuItems.has(item.name)) {
      return null;
    }

    if (isNamedSlashMenuItem(item) && item.name === 'New Doc' && !isSlashSubMenu(item)) {
      return {
        ...item,
        description: 'Start a new note.',
        action: context => {
          void runCreateLinkedNoteAction('inline', context);
        },
      } as AffineSlashMenuActionItem;
    }

    if (isSlashSubMenu(item)) {
      const subMenu = filterSlashMenuStaticItems(item.subMenu, filterSlashMenuStaticItem);

      if (subMenu.length === 0) {
        return null;
      }

      return {
        ...item,
        subMenu,
      };
    }

    return item;
  };

  AffineSlashMenuWidget.DEFAULT_CONFIG = {
    ...config,
    items: [
      { groupName: 'Cognt' },
      {
        name: 'Create Note',
        description: 'Create a note link.',
        alias: ['note', 'linked note', 'new note'],
        subMenu: [
          { groupName: 'Create Note' },
          {
            name: 'Inline Link',
            description: 'Insert a link to a new note.',
            alias: ['inline note', 'link note'],
            action: context => {
              void runCreateLinkedNoteAction('inline', context);
            },
          },
          {
            name: 'Embed Link',
            description: 'Insert an embedded reference to a new note.',
            alias: ['embed note', 'note embed', 'embed view'],
            action: context => {
              void runCreateLinkedNoteAction('embed', context);
            },
          },
        ],
      },
      {
        name: 'Image',
        description: 'Insert an image block.',
        alias: ['picture', 'photo', 'attachment'],
        action: context => {
          void runInsertImageAction(context);
        },
      },
      ...filterSlashMenuItems(config.items, filterSlashMenuStaticItem),
    ],
  };
}

function filterSlashMenuItems(
  items: AffineSlashMenuItem[],
  filterStaticItem: (item: SlashMenuStaticItem) => SlashMenuStaticItem | null
) {
  const filtered: AffineSlashMenuItem[] = [];
  let isSkippingSection = false;

  for (const item of items) {
    if (isSlashMenuGenerator(item)) {
      if (!isSkippingSection) {
        filtered.push(context => filterSlashMenuStaticItems(item(context), filterStaticItem));
      }

      continue;
    }

    if (isSlashMenuGroupDivider(item)) {
      isSkippingSection = hiddenSlashMenuSections.has(item.groupName);

      if (!isSkippingSection) {
        filtered.push(item);
      }

      continue;
    }

    if (isSkippingSection) {
      continue;
    }

    const nextItem = filterStaticItem(item);
    if (nextItem) {
      filtered.push(nextItem);
    }
  }

  return filtered;
}

function filterSlashMenuStaticItems(
  items: SlashMenuStaticItem[],
  filterStaticItem: (item: SlashMenuStaticItem) => SlashMenuStaticItem | null
) {
  const filtered: SlashMenuStaticItem[] = [];
  let isSkippingSection = false;

  for (const item of items) {
    if (isSlashMenuGroupDivider(item)) {
      isSkippingSection = hiddenSlashMenuSections.has(item.groupName);

      if (!isSkippingSection) {
        filtered.push(item);
      }

      continue;
    }

    if (isSkippingSection) {
      continue;
    }

    const nextItem = filterStaticItem(item);
    if (nextItem) {
      filtered.push(nextItem);
    }
  }

  return filtered;
}

function isSlashMenuGenerator(item: AffineSlashMenuItem): item is AffineSlashMenuItemGenerator {
  return typeof item === 'function';
}

function isSlashMenuGroupDivider(item: SlashMenuStaticItem): item is AffineSlashMenuGroupDivider {
  return 'groupName' in item;
}

function isSlashSubMenu(item: SlashMenuStaticItem): item is AffineSlashSubMenu {
  return 'subMenu' in item;
}

function isNamedSlashMenuItem(
  item: SlashMenuStaticItem
): item is AffineSlashMenuActionItem | AffineSlashSubMenu {
  return 'name' in item;
}
