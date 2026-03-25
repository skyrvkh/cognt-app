export type HostMessage =
  | {
      type: 'load';
      loadId?: number;
      path?: string;
      title?: string;
      content?: string;
    }
  | {
      type: 'saveResult';
      path?: string;
      ok: boolean;
      error?: string;
    }
  | {
      type: 'extractResult';
      path?: string;
      ok: boolean;
      createdPath?: string;
      error?: string;
    };

type WebViewHost = {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: { data: HostMessage }) => void): void;
};

declare global {
  interface Window {
    chrome?: {
      webview?: WebViewHost;
    };
  }
}

export function postHost(message: unknown) {
  window.chrome?.webview?.postMessage(message);
}

export function addHostMessageListener(handler: (message: HostMessage) => void) {
  window.chrome?.webview?.addEventListener('message', event => {
    handler(event.data);
  });
}

export function reportDiagnostic(level: 'info' | 'warn' | 'error', message: string) {
  postHost({ type: 'diagnostic', level, message });
}

export function installHostDiagnostics() {
  window.addEventListener('error', event => {
    reportDiagnostic('error', errorDetail(event.error, event.message || 'Unhandled editor script error.'));
  });

  window.addEventListener('unhandledrejection', event => {
    reportDiagnostic('error', errorDetail(event.reason, 'Unhandled editor promise rejection.'));
  });
}

export function isHostedInWebView() {
  return Boolean(window.chrome?.webview);
}

function errorDetail(error: unknown, fallback: string) {
  if (error instanceof Error) {
    return error.stack || error.message || fallback;
  }

  return error ? String(error) : fallback;
}
