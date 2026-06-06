import type { MarkdownEditorOptions } from '../editor/contracts';

export type DotNetReference = {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
};

export type InvokeHost = (
  dotNet: DotNetReference,
  method: string,
  ...args: unknown[]
) => Promise<unknown>;

export type EditorHostCallbacks = Omit<
  MarkdownEditorOptions,
  'hostElement' | 'documentLinksElement' | 'statusElement'
>;

export type EditorHostCallbackOptions = {
  dotNet: DotNetReference;
  instanceId: number;
  invokeHost: InvokeHost;
};
