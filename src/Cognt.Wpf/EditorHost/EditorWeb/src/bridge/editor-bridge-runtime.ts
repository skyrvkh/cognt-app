import type { MarkdownBlockSuiteEditor } from '../blocksuite-editor';
import { BlockSuiteInstanceRegistry } from './editor-instance-registry';

type EditorBridgeInstance = {
  editor: MarkdownBlockSuiteEditor;
};

const registry = new BlockSuiteInstanceRegistry();

export function allocateEditorInstanceId() {
  return registry.allocateId();
}

export function registerEditorInstance(instanceId: number, editor: MarkdownBlockSuiteEditor) {
  return registry.register(instanceId, editor);
}

export function disposeEditorInstance(instanceId: number) {
  return registry.dispose(instanceId);
}

export function getActiveEditorInstanceCount() {
  return registry.size;
}

export async function runEditorOperation<T>(
  instanceId: number,
  operation: string,
  action: (instance: EditorBridgeInstance) => Promise<T> | T
): Promise<T> {
  return registry.run(instanceId, operation, action);
}

export function runEditorOperationSync<T>(
  instanceId: number,
  operation: string,
  action: (instance: EditorBridgeInstance) => T
): T {
  return registry.runSync(instanceId, operation, action);
}

export function normalizeCaretState(
  value?:
    | {
        blockId?: string;
        BlockId?: string;
        index?: number;
        Index?: number;
        length?: number;
        Length?: number;
      }
    | null
) {
  const blockId = value?.blockId ?? value?.BlockId ?? '';
  const index = value?.index ?? value?.Index ?? 0;
  const length = value?.length ?? value?.Length ?? 0;
  return {
    blockId,
    index,
    length,
  };
}
