export function delayWithAbort(delayMs: number, signal: AbortSignal) {
  if (signal.aborted) {
    return Promise.reject(new Error('Mention search was cancelled.'));
  }

  return new Promise<void>((resolve, reject) => {
    const timeoutId = window.setTimeout(() => {
      signal.removeEventListener('abort', abort);
      resolve();
    }, delayMs);

    const abort = () => {
      window.clearTimeout(timeoutId);
      signal.removeEventListener('abort', abort);
      reject(new Error('Mention search was cancelled.'));
    };

    signal.addEventListener('abort', abort, { once: true });
  });
}
