import type { Doc } from '@blocksuite/store';
import type { RequestMentionPreviewMessage } from '../mention-controller';
import type { MentionTarget } from '../mentions';
import { normalizeLinkedNotePathKey } from '../links/linked-notes';

const linkedDocPreviewMaxCharacters = 4000;

export type PendingLinkedDocPreview = {
  doc: Doc;
  generation: number;
  relativePath: string;
};

export class LinkedDocPreviewState {
  private readonly pathsByPageId = new Map<string, string>();
  private readonly titlesByPath = new Map<string, string>();
  private readonly requests = new Map<string, PendingLinkedDocPreview>();
  private readonly previewedPageIds = new Set<string>();
  private requestCounter = 0;

  public mergeMentionTargetTitles(targets: MentionTarget[], replace: boolean) {
    if (replace) {
      this.titlesByPath.clear();
    }

    for (const target of targets) {
      const title = target.title.trim();
      if (title.length > 0) {
        this.titlesByPath.set(normalizeLinkedNotePathKey(target.relativePath), title);
      }
    }
  }

  public resolveTitle(relativePath: string) {
    return this.titlesByPath.get(normalizeLinkedNotePathKey(relativePath)) ?? null;
  }

  public trackDoc(doc: Doc, relativePath: string) {
    this.pathsByPageId.set(doc.id, relativePath);
  }

  public trackPath(pageId: string | null | undefined, relativePath: string | null | undefined) {
    if (pageId && relativePath) {
      this.pathsByPageId.set(pageId, relativePath);
    }
  }

  public getReferencePath(pageId: string | null | undefined, fallback?: string | null) {
    return (pageId ? this.pathsByPageId.get(pageId) : undefined) ?? fallback;
  }

  public hasPreviewed(doc: Doc) {
    return this.previewedPageIds.has(doc.id);
  }

  public markPreviewed(doc: Doc) {
    this.previewedPageIds.add(doc.id);
  }

  public createPreviewRequest(
    doc: Doc,
    relativePath: string,
    generation: number,
    sourcePath: string
  ): RequestMentionPreviewMessage | null {
    if (!sourcePath) {
      return null;
    }

    this.markPreviewed(doc);
    const requestId = `linked-doc-${Date.now().toString(36)}-${(
      ++this.requestCounter
    ).toString(36)}`;
    this.requests.set(requestId, { doc, generation, relativePath });

    return {
      type: 'requestMentionPreview',
      requestId,
      sourcePath,
      relativePath,
      mode: 'preview',
      maxCharacters: linkedDocPreviewMaxCharacters,
    };
  }

  public hasPreviewRequest(requestId: string) {
    return this.requests.has(requestId);
  }

  public takePreviewRequest(requestId: string) {
    const request = this.requests.get(requestId) ?? null;
    this.requests.delete(requestId);
    return request;
  }

  public clear() {
    this.pathsByPageId.clear();
    this.requests.clear();
    this.previewedPageIds.clear();
  }
}
