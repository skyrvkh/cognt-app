import { effects as blockEffects } from '@blocksuite/blocks/effects';
import { effects as presetEffects } from '@blocksuite/presets/effects';
import type { Doc } from '@blocksuite/store';
import { defineCogntReferencesBlockElement } from '../blocks/references';
import { configureCogntSlashMenu, type CogntSlashMenuEditor } from './slash-menu';
import { patchBlockStdRender } from './specs';

const editorsByDoc = new WeakMap<Doc, CogntSlashMenuEditor>();
let isRegistered = false;

export function ensureMarkdownEditorRegistration() {
  if (isRegistered) {
    return;
  }

  defineCogntReferencesBlockElement();
  configureCogntSlashMenu({
    getEditor: doc => editorsByDoc.get(doc),
  });
  blockEffects();
  presetEffects();
  patchBlockStdRender();
  isRegistered = true;
}

export function trackMarkdownEditorDoc(doc: Doc, editor: CogntSlashMenuEditor) {
  editorsByDoc.set(doc, editor);
}

export function untrackMarkdownEditorDoc(doc: Doc) {
  editorsByDoc.delete(doc);
}
