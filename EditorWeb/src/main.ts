import { MarkdownBlockSuiteEditor } from './blocksuite-editor';
import { addHostMessageListener, installHostDiagnostics, isHostedInWebView, postHost, reportDiagnostic } from './host';
import './styles.css';

installHostDiagnostics();

const editorHost = document.querySelector<HTMLElement>('#editor-host');
const editorStatusElement = document.querySelector<HTMLElement>('#editor-status');

if (!editorHost || !editorStatusElement) {
  throw new Error('Editor host elements are missing.');
}

const editor = new MarkdownBlockSuiteEditor({
  hostElement: editorHost,
  statusElement: editorStatusElement,
  onContentChanged: content => postHost({ type: 'contentChanged', content }),
});

addHostMessageListener(message => {
  if (message.type === 'load') {
    void editor.load(message).finally(() => {
      postHost({ type: 'loaded', loadId: message.loadId });
    });
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
