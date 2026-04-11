import { PageEditorBlockSpecs } from '@blocksuite/blocks';
import {
  BlockStdScope,
  ConfigExtension,
  type EditorHost,
} from '@blocksuite/block-std';
import type {
  NativeInlineEditor,
  NativeLinkedMenuGroup,
} from '../mention-controller-types';
import { CogntPropertiesBlockSpec } from '../blocks/properties';
import { CogntReferencesBlockSpec } from '../blocks/references';

export function createEditorSpecs(
  getMenus: (
    query: string,
    abort: () => void,
    inlineEditor: NativeInlineEditor,
    abortSignal: AbortSignal
  ) => Promise<NativeLinkedMenuGroup[]> | NativeLinkedMenuGroup[]
) {
  return [
    ...PageEditorBlockSpecs,
    ...CogntPropertiesBlockSpec,
    ...CogntReferencesBlockSpec,
    ConfigExtension('affine:page' as never, {
      linkedWidget: {
        triggerKeys: ['@'],
        convertTriggerKey: false,
        getMenus: (
          query: string,
          abort: () => void,
          _editorHost: EditorHost,
          inlineEditor: NativeInlineEditor,
          abortSignal: AbortSignal
        ) => getMenus(query, abort, inlineEditor, abortSignal),
      },
    }),
  ];
}

export function patchBlockStdRender() {
  const prototype = BlockStdScope.prototype as unknown as {
    __cogntRenderPatched?: boolean;
    render: () => EditorHost;
  };

  if (prototype.__cogntRenderPatched) {
    return;
  }

  prototype.render = function renderWithCreateElement(this: BlockStdScope) {
    const scope = this as unknown as {
      doc: unknown;
      _getHost: () => EditorHost;
      _lifeCycleWatchers: Array<{ rendered: { call(watcher: unknown): void } }>;
    };
    const element = document.createElement('editor-host') as EditorHost;
    element.std = this;
    element.doc = scope.doc as never;
    scope._getHost = () => element;
    scope._lifeCycleWatchers.forEach(watcher => {
      watcher.rendered.call(watcher);
    });
    return element;
  };
  prototype.__cogntRenderPatched = true;
}
