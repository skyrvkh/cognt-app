import './app-fonts.css';
import '@toeverything/theme/style.css';
import './styles.css';
import { installHostDiagnostics } from './host';

export { mountBlockSuite, disposeBlockSuite } from './bridge/editor-bridge-mount';
export {
  activateEditor,
  applyTheme,
  createClozeFromSelection,
  exportContent,
  focusBlock,
  focusEditor,
  getCaretState,
  getSelectedCardContextBlocks,
  getSelectedImageBlock,
  handleMentionOpenResult,
  handleMentionPreviewResult,
  loadNote,
  pasteRichHtmlForAutomation,
  restoreCaretState,
  setDocumentLinks,
  setMentionTargets,
  setReadOnlyMode,
  showExtractResult,
  showSaveResult,
} from './bridge/editor-bridge-operations';

installHostDiagnostics();
