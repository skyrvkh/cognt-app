import '@toeverything/theme/fonts.css';
import '@toeverything/theme/style.css';
import './review.css';
import { MarkdownBlockSuiteRenderer } from './blocksuite-renderer';
import {
  addHostMessageListener,
  installHostDiagnostics,
  isHostedInWebView,
  postHost,
  reportDiagnostic,
  waitForAnimationFrames,
} from './host';

installHostDiagnostics();

const cardHost = document.querySelector<HTMLElement>('#review-card-host');
const statusElement = document.querySelector<HTMLElement>('#review-status');

if (!cardHost || !statusElement) {
  throw new Error('Review host elements are missing.');
}

const renderer = new MarkdownBlockSuiteRenderer(cardHost);

addHostMessageListener(message => {
  if (message.type !== 'loadCard') {
    return;
  }

  void renderer.load(message.markdown ?? '').then(
    async () => {
      await waitForAnimationFrames(2);
      statusElement.dataset.visible = 'false';
      postHost({ type: 'loaded', loadId: message.loadId, side: message.side });
    },
    error => {
      const detail = error instanceof Error ? error.message : String(error);
      statusElement.textContent = detail;
      statusElement.dataset.visible = 'true';
      postHost({ type: 'loaded', loadId: message.loadId, side: message.side, error: detail });
    }
  );
});

postHost({ type: 'ready' });
reportDiagnostic('info', 'BlockSuite review script ready.');

if (!isHostedInWebView()) {
  void renderer.load('What is the powerhouse of the cell?');
}
