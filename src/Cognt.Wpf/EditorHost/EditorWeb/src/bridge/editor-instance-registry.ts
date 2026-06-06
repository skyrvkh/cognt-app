import type { MarkdownBlockSuiteEditor } from '../blocksuite-editor';
import { reportDiagnostic } from '../host';
import type { DotNetReference } from './host-callbacks';

type BlockSuiteInstance = {
  editor: MarkdownBlockSuiteEditor;
};

export class BlockSuiteInstanceRegistry {
  private readonly instances = new Map<number, BlockSuiteInstance>();
  private nextInstanceId = 1;

  public get size() {
    return this.instances.size;
  }

  public allocateId() {
    return this.nextInstanceId++;
  }

  public register(instanceId: number, editor: MarkdownBlockSuiteEditor) {
    this.instances.set(instanceId, { editor });
    return instanceId;
  }

  public dispose(instanceId: number) {
    const instance = this.instances.get(instanceId);
    instance?.editor.dispose();
    this.instances.delete(instanceId);
    return this.instances.size;
  }

  public async run<T>(
    instanceId: number,
    operation: string,
    action: (instance: BlockSuiteInstance) => Promise<T> | T
  ): Promise<T> {
    const startedAt = performance.now();
    reportDiagnostic('info', `Bridge editor operation started (instance=${instanceId}, ${operation}).`);
    try {
      const result = await action(this.get(instanceId));
      reportDiagnostic(
        'info',
        `Bridge editor operation completed (instance=${instanceId}, ${operation}, ${formatDuration(startedAt)}).`
      );
      return result;
    } catch (error) {
      reportDiagnostic(
        'error',
        `Bridge editor operation failed (instance=${instanceId}, ${operation}): ${errorMessage(error)}.`
      );
      throw error;
    }
  }

  public runSync<T>(
    instanceId: number,
    operation: string,
    action: (instance: BlockSuiteInstance) => T
  ): T {
    const startedAt = performance.now();
    reportDiagnostic('info', `Bridge editor operation started (instance=${instanceId}, ${operation}).`);
    try {
      const result = action(this.get(instanceId));
      reportDiagnostic(
        'info',
        `Bridge editor operation completed (instance=${instanceId}, ${operation}, ${formatDuration(startedAt)}).`
      );
      return result;
    } catch (error) {
      reportDiagnostic(
        'error',
        `Bridge editor operation failed (instance=${instanceId}, ${operation}): ${errorMessage(error)}.`
      );
      throw error;
    }
  }

  private get(instanceId: number) {
    const instance = this.instances.get(instanceId);
    if (!instance) {
      reportDiagnostic('error', `Bridge operation failed: instance ${instanceId} is not mounted.`);
      throw new Error(`BlockSuite instance ${instanceId} is not mounted.`);
    }

    return instance;
  }
}

export async function invokeLoggedHost(
  dotNet: DotNetReference,
  method: string,
  ...args: unknown[]
) {
  const startedAt = performance.now();
  reportDiagnostic('info', `Bridge host call started (${method}, args=${args.length}).`);
  try {
    const result = await dotNet.invokeMethodAsync(method, ...args);
    reportDiagnostic('info', `Bridge host call completed (${method}, ${formatDuration(startedAt)}).`);
    return result;
  } catch (error) {
    reportDiagnostic('error', `Bridge host call failed (${method}): ${errorMessage(error)}.`);
    throw error;
  }
}

function formatDuration(startedAt: number) {
  return `${Math.max(0, Math.round(performance.now() - startedAt))}ms`;
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
