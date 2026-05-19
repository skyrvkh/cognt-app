import { focusTextModel } from '@blocksuite/affine-components/rich-text';
import type { EditorHost } from '@blocksuite/block-std';
import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { patchRenderedEmbedLinkedDocBlocks } from '../links/linked-notes';
import {
  findEditableElement,
  findElementsDeep,
} from './dom';

export type MutationPersistenceHost = {
  flushHost(): Promise<void>;
  cancelPendingContentChanged(): void;
  recordUserEdit(): void;
  notifyContentChanged(): Promise<void>;
};

export type FlushEditorHostOptions = {
  patchLinkedDocBlocks?: boolean;
};

export function setDocReadonly(doc: Doc | null, readonly: boolean) {
  doc?.awarenessStore.setReadonly(doc.blockCollection, readonly);
}

export function focusFirstTextBlock(editor: PageEditor, doc: Doc | null) {
  if (!doc || doc.readonly) {
    return;
  }

  const textBlock = doc
    .getBlocks()
    .find(block => block.role === 'content' && block.text);

  if (textBlock) {
    focusTextModel(editor.std, textBlock.id, textBlock.text?.length ?? 0);
  }
}

export async function forceEditableRichText(editor: PageEditor) {
  for (const richText of findElementsDeep(editor, 'rich-text')) {
    const editable = richText as HTMLElement & {
      readonly?: boolean;
      inlineEditor?: { setReadonly?: (readonly: boolean) => void };
      requestUpdate?: (name?: string, oldValue?: unknown) => void;
      updateComplete?: Promise<unknown>;
    };

    editable.readonly = false;
    editable.inlineEditor?.setReadonly?.(false);
    editable.requestUpdate?.('readonly', true);
    await editable.updateComplete;

    const inlineEditor = findEditableElement(editable.shadowRoot) ??
      editable.shadowRoot?.querySelector<HTMLElement>('.inline-editor');

    inlineEditor?.classList.remove('readonly');
    inlineEditor?.setAttribute('contenteditable', 'true');
  }
}

export async function forceReadonlyRichText(editor: PageEditor) {
  for (const richText of findElementsDeep(editor, 'rich-text')) {
    const editable = richText as HTMLElement & {
      readonly?: boolean;
      inlineEditor?: { setReadonly?: (readonly: boolean) => void };
      requestUpdate?: (name?: string, oldValue?: unknown) => void;
      updateComplete?: Promise<unknown>;
    };

    editable.readonly = true;
    editable.inlineEditor?.setReadonly?.(true);
    editable.requestUpdate?.('readonly', false);
    await editable.updateComplete;

    const inlineEditor = findEditableElement(editable.shadowRoot) ??
      editable.shadowRoot?.querySelector<HTMLElement>('.inline-editor');

    inlineEditor?.classList.add('readonly');
    inlineEditor?.setAttribute('contenteditable', 'false');
  }
}

export async function flushEditorHost(
  editor: PageEditor,
  options: FlushEditorHostOptions = {}
) {
  await editor.updateComplete;

  const host = editor.host as (EditorHost & {
    requestUpdate?: () => void;
    updateComplete?: Promise<unknown>;
  }) | null;

  if (!host || !editor.std || !editor.doc) {
    return;
  }

  host.std = editor.std;
  host.doc = editor.doc;
  host.requestUpdate?.();
  await host.updateComplete;
  if (options.patchLinkedDocBlocks === false) {
    return;
  }

  patchRenderedEmbedLinkedDocBlocks(editor);
  window.requestAnimationFrame(() => patchRenderedEmbedLinkedDocBlocks(editor));
  window.setTimeout(() => patchRenderedEmbedLinkedDocBlocks(editor), 80);
}

export async function persistMutation(host: MutationPersistenceHost) {
  await host.flushHost();
  host.cancelPendingContentChanged();
  host.recordUserEdit();
  await host.notifyContentChanged();
}
