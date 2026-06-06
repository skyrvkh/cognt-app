import { reportDiagnostic } from '../host';

type LoggedHostRequestOptions<TResult> = {
  startMessage: string;
  invoke(): Promise<unknown>;
  normalize(value: unknown): TResult;
  completeMessage(result: TResult): string;
  level?(result: TResult): 'info' | 'warn';
};

export async function invokeLoggedHostRequest<TResult>({
  startMessage,
  invoke,
  normalize,
  completeMessage,
  level,
}: LoggedHostRequestOptions<TResult>) {
  reportDiagnostic('info', startMessage);
  const result = normalize(await invoke());
  reportDiagnostic(level?.(result) ?? 'info', completeMessage(result));
  return result;
}
