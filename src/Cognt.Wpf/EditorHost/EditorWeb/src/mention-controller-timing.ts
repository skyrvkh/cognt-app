import { reportDiagnostic } from './host';

export function delayWithAbort(delayMs: number, signal: AbortSignal) {
  if (signal.aborted) {
    reportDiagnostic('info', 'Mention search delay skipped because signal was already aborted.', {
      delayMs,
    });
    return Promise.reject(new Error('Mention search was cancelled.'));
  }

  reportDiagnostic('info', 'Starting mention search delay.', { delayMs });
  return new Promise<void>((resolve, reject) => {
    const timeoutId = window.setTimeout(() => {
      signal.removeEventListener('abort', abort);
      reportDiagnostic('info', 'Mention search delay completed.', { delayMs });
      resolve();
    }, delayMs);

    const abort = () => {
      window.clearTimeout(timeoutId);
      signal.removeEventListener('abort', abort);
      reportDiagnostic('info', 'Mention search delay aborted.', { delayMs });
      reject(new Error('Mention search was cancelled.'));
    };

    signal.addEventListener('abort', abort, { once: true });
  });
}
