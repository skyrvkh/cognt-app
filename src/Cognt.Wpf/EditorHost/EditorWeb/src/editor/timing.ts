import { reportDiagnostic } from '../host';

export function formatElapsed(startedAt: number) {
  return `${Math.round(performance.now() - startedAt)}ms`;
}

export function logEditorTiming(operation: string, startedAt: number, detail?: string) {
  const suffix = detail ? ` [${detail}]` : '';
  reportDiagnostic('info', `BlockSuite editor ${operation}: ${formatElapsed(startedAt)}${suffix}`);
}
