import '@toeverything/theme/fonts.css';
import '@toeverything/theme/style.css';
import './styles.css';
import { MarkdownBlockSuiteEditor } from './blocksuite-editor';
import {
  addHostMessageListener,
  installHostDiagnostics,
  isHostedInWebView,
  postHost,
  reportDiagnostic,
  waitForAnimationFrames,
} from './host';

installHostDiagnostics();

const editorHost = document.querySelector<HTMLElement>('#editor-host');
const editorStatusElement = document.querySelector<HTMLElement>('#editor-status');
const mentionPreviewElement = document.querySelector<HTMLElement>('#mention-preview');

if (!editorHost || !editorStatusElement || !mentionPreviewElement) {
  throw new Error('Editor host elements are missing.');
}

const editor = new MarkdownBlockSuiteEditor({
  hostElement: editorHost,
  statusElement: editorStatusElement,
  mentionPreviewElement,
  onContentChanged: content => postHost({ type: 'contentChanged', content }),
  onCreateExtract: message => postHost(message),
  onSaveCloze: message => postHost(message),
  onRequestMentionPreview: message => postHost(message),
  onOpenMention: message => postHost(message),
});

addHostMessageListener(message => {
  if (message.type === 'load') {
    void editor.load(message).finally(async () => {
      await waitForAnimationFrames(2);

      postHost({ type: 'loaded', loadId: message.loadId });
    });
    return;
  }

  if (message.type === 'saveResult') {
    editor.showStatus(message.ok ? 'Saved.' : `Save failed: ${message.error ?? 'Unknown error'}`);
    return;
  }

  if (message.type === 'setMentionTargets') {
    editor.setMentionTargets(message.files);
    return;
  }

  if (message.type === 'mentionPreviewResult') {
    editor.handleMentionPreviewResult(message);
    return;
  }

  if (message.type === 'mentionOpenResult') {
    editor.handleMentionOpenResult(message.ok, message.error);
    return;
  }

  if (message.type === 'extractResult') {
    editor.showStatus(
      message.ok
        ? `Created ${message.createdPath?.split(/[\\/]/).pop() ?? 'extract file'}.`
        : `Extract failed: ${message.error ?? 'Unknown error'}`
    );
  }
});

postHost({ type: 'ready' });
reportDiagnostic('info', 'BlockSuite web script ready.');

if (!isHostedInWebView()) {
  void editor.load({
    title: 'Preview',
    content: '# BlockSuite preview\n\nThis preview is loaded outside the WPF host.\n\n- **Bold text**\n- *Italic text*\n',
  });
}
