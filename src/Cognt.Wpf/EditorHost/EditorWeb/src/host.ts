declare global {
  interface Window {
    __cogntPostHost?: (message: unknown) => void;
    __cogntEnableInfoDiagnostics?: boolean;
  }
}

export function postHost(message: unknown) {
  window.__cogntPostHost?.(message);
}

export function reportDiagnostic(
  level: 'info' | 'warn' | 'error',
  message: string,
  details?: Record<string, unknown>
) {
  if (level === 'info' && window.__cogntEnableInfoDiagnostics !== true) {
    return;
  }

  postHost({
    type: 'diagnostic',
    level,
    message: details ? `${message} ${formatDiagnosticDetails(details)}` : message,
  });
}

export function installHostDiagnostics() {
  window.addEventListener('error', event => {
    reportDiagnostic('error', errorDetail(event.error, event.message || 'Unhandled editor script error.'));
  });

  window.addEventListener('unhandledrejection', event => {
    reportDiagnostic('error', errorDetail(event.reason, 'Unhandled editor promise rejection.'));
  });
}

export async function waitForAnimationFrames(count: number) {
  for (let frame = 0; frame < count; frame++) {
    await new Promise<void>(resolve => {
      requestAnimationFrame(() => resolve());
    });
  }
}

function errorDetail(error: unknown, fallback: string) {
  if (error instanceof Error) {
    return error.stack || error.message || fallback;
  }

  return error ? String(error) : fallback;
}

function formatDiagnosticDetails(details: Record<string, unknown>) {
  try {
    return JSON.stringify(details);
  } catch (error) {
    return `[details unavailable: ${errorDetail(error, 'serialization failed')}]`;
  }
}
