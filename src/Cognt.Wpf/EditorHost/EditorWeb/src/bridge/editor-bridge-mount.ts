import { MarkdownBlockSuiteEditor } from '../blocksuite-editor';
import { reportDiagnostic } from '../host';
import {
  allocateEditorInstanceId,
  disposeEditorInstance,
  getActiveEditorInstanceCount,
  registerEditorInstance,
} from './editor-bridge-runtime';
import {
  createEditorHostCallbacks,
  type DotNetReference,
} from './host-callbacks';
import { invokeLoggedHost } from './editor-instance-registry';

export function mountBlockSuite(
  hostElement: HTMLElement,
  documentLinksElement: HTMLElement,
  statusElement: HTMLElement,
  dotNet: DotNetReference
) {
  reportDiagnostic(
    'info',
    `Bridge mount requested (hostChildren=${hostElement.childElementCount}, linksChildren=${documentLinksElement.childElementCount}).`
  );
  const instanceId = allocateEditorInstanceId();

  window.__cogntPostHost = message => {
    void dotNet.invokeMethodAsync('HandleHostMessage', message);
  };

  const editor = new MarkdownBlockSuiteEditor({
    hostElement,
    documentLinksElement,
    statusElement,
    ...createEditorHostCallbacks({
      dotNet,
      instanceId,
      invokeHost: invokeLoggedHost,
    }),
  });

  registerEditorInstance(instanceId, editor);
  reportDiagnostic(
    'info',
    `Bridge mount completed (instance=${instanceId}, activeInstances=${getActiveEditorInstanceCount()}).`
  );
  return instanceId;
}

export function disposeBlockSuite(instanceId: number) {
  reportDiagnostic('info', `Bridge dispose requested (instance=${instanceId}).`);
  const activeInstances = disposeEditorInstance(instanceId);

  if (activeInstances === 0) {
    window.__cogntPostHost = undefined;
  }

  reportDiagnostic(
    'info',
    `Bridge dispose completed (instance=${instanceId}, activeInstances=${activeInstances}).`
  );
}
