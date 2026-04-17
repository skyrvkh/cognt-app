import type { PageEditor } from '@blocksuite/presets';
import type {
  MentionTarget,
  MentionViewMode,
} from './mentions';

export type RequestMentionPreviewMessage = {
  type: 'requestMentionPreview';
  requestId: string;
  sourcePath: string;
  relativePath: string;
  mode: MentionViewMode;
  maxCharacters: number;
};

export type OpenMentionMessage = {
  type: 'openMention';
  sourcePath: string;
  relativePath: string;
  content: string;
};

export type SearchMentionTargetsMessage = {
  type: 'searchMentionTargets';
  sourcePath: string;
  query: string;
  limit: number;
};

export type NativeInlineRange = {
  index: number;
  length: number;
};

export type NativeInlineEditor = {
  getInlineRange(): NativeInlineRange | null;
  insertText(range: NativeInlineRange, text: string, attributes?: unknown): void;
  setInlineRange(range: NativeInlineRange | null): void;
};

type NativeLinkedMenuItem = {
  key: string;
  name: string;
  icon: unknown;
  suffix?: string;
  action: () => void | Promise<void>;
};

export type NativeLinkedMenuGroup = {
  name: string;
  items: NativeLinkedMenuItem[];
  maxDisplay?: number;
  overflowText?: string;
};

export type MentionControllerOptions = {
  editor: PageEditor;
  previewElement: HTMLElement;
  getCurrentPath(): string;
  exportContent(): Promise<string>;
  flushHost(): Promise<void>;
  scheduleContentChanged(): void;
  showStatus(message: string): void;
  onSearchTargets(message: SearchMentionTargetsMessage): Promise<MentionTarget[]>;
  onRequestPreview(message: RequestMentionPreviewMessage): void;
  onOpenMention(message: OpenMentionMessage): void;
};
