import { focusTextModel } from '@blocksuite/affine-components/rich-text';
import type { EditorHost } from '@blocksuite/block-std';
import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import {
  patchRenderedEmbedLinkedDocBlocks,
  patchRenderedInlineLinkedNoteReferences,
} from '../links/linked-notes';
import {
  findEditableElement,
  findElementsDeep,
} from './dom';
import { reportDiagnostic } from '../host';

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
  reportDiagnostic('info', `Editor readonly state requested (hasDoc=${Boolean(doc)}, readonly=${readonly}).`);
  doc?.awarenessStore.setReadonly(doc.blockCollection, readonly);
}

export function focusFirstTextBlock(editor: PageEditor, doc: Doc | null) {
  if (!doc || doc.readonly) {
    reportDiagnostic(
      'warn',
      `Focus first text block skipped (hasDoc=${Boolean(doc)}, readonly=${doc?.readonly ?? false}).`
    );
    return;
  }

  const textBlock = doc
    .getBlocks()
    .find(block => block.role === 'content' && block.text);

  if (textBlock) {
    focusTextModel(editor.std, textBlock.id, textBlock.text?.length ?? 0);
    reportDiagnostic('info', `Focused first text block (blockId=${textBlock.id}).`);
  } else {
    reportDiagnostic('info', 'Focus first text block skipped: no content text block found.');
  }
}

export async function forceEditableRichText(editor: PageEditor) {
  let count = 0;
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
    count++;
  }

  reportDiagnostic('info', `Forced rich text editable state (count=${count}).`);
}

export async function forceReadonlyRichText(editor: PageEditor) {
  let count = 0;
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
    count++;
  }

  reportDiagnostic('info', `Forced rich text readonly state (count=${count}).`);
}

export async function flushEditorHost(
  editor: PageEditor,
  options: FlushEditorHostOptions = {}
) {
  reportDiagnostic(
    'info',
    `Editor host flush started (patchLinkedDocBlocks=${options.patchLinkedDocBlocks !== false}).`
  );
  await editor.updateComplete;

  const host = editor.host as (EditorHost & {
    requestUpdate?: () => void;
    updateComplete?: Promise<unknown>;
  }) | null;

  if (!host || !editor.std || !editor.doc) {
    reportDiagnostic(
      'warn',
      `Editor host flush skipped (hasHost=${Boolean(host)}, hasStd=${Boolean(editor.std)}, hasDoc=${Boolean(editor.doc)}).`
    );
    return;
  }

  host.std = editor.std;
  host.doc = editor.doc;
  host.requestUpdate?.();
  await host.updateComplete;
  if (options.patchLinkedDocBlocks === false) {
    reportDiagnostic('info', 'Editor host flush completed without linked-note patching.');
    return;
  }

  patchRenderedEmbedLinkedDocBlocks(editor);
  patchRenderedInlineLinkedNoteReferences(editor);
  reportDiagnostic('info', 'Editor host flush applied immediate linked-note render patches.');
  window.requestAnimationFrame(() => {
    patchRenderedEmbedLinkedDocBlocks(editor);
    patchRenderedInlineLinkedNoteReferences(editor);
    reportDiagnostic('info', 'Editor host flush applied animation-frame linked-note render patches.');
  });
  window.setTimeout(() => {
    patchRenderedEmbedLinkedDocBlocks(editor);
    patchRenderedInlineLinkedNoteReferences(editor);
    reportDiagnostic('info', 'Editor host flush applied delayed linked-note render patches.');
  }, 80);
}

export async function persistMutation(host: MutationPersistenceHost) {
  reportDiagnostic('info', 'Editor mutation persistence started.');
  await host.flushHost();
  host.cancelPendingContentChanged();
  host.recordUserEdit();
  await host.notifyContentChanged();
  reportDiagnostic('info', 'Editor mutation persistence completed.');
}
