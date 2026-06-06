import { createAssetHostCallbacks } from './host-asset-callbacks';
import { createDocumentHostCallbacks } from './host-document-callbacks';
import { createLinkHostCallbacks } from './host-link-callbacks';
import type {
  EditorHostCallbackOptions,
  EditorHostCallbacks,
} from './host-callback-types';

export type { DotNetReference } from './host-callback-types';

export function createEditorHostCallbacks(
  options: EditorHostCallbackOptions
): EditorHostCallbacks {
  return {
    ...createDocumentHostCallbacks(options),
    ...createLinkHostCallbacks(options),
    ...createAssetHostCallbacks(options),
  };
}
