export type BlockUpdatePayload = {
  type?: 'add' | 'delete' | 'update';
  flavour?: string;
  props?: {
    key?: string;
  };
};

export type DocumentChangeBatch = {
  eventCount: number;
  shouldRefreshLinks: boolean;
  shouldRefreshMentions: boolean;
  startedAt: number;
  updateSummary: string;
};

export class DocumentChangeTracker {
  private shouldRefreshLinks = false;
  private shouldRefreshMentions = false;
  private eventCount = 0;
  private startedAt = 0;
  private readonly keys = new Map<string, number>();

  public record(payload?: BlockUpdatePayload) {
    if (this.eventCount === 0) {
      this.startedAt = performance.now();
    }

    this.eventCount++;
    this.track(payload);
    this.shouldRefreshLinks =
      this.shouldRefreshLinks || shouldRefreshLinkedDocsForBlockChange(payload);
    this.shouldRefreshMentions =
      this.shouldRefreshMentions || shouldRefreshMentionsForBlockChange(payload);
  }

  public snapshot(): DocumentChangeBatch {
    return {
      eventCount: this.eventCount,
      shouldRefreshLinks: this.shouldRefreshLinks,
      shouldRefreshMentions: this.shouldRefreshMentions,
      startedAt: this.startedAt,
      updateSummary: this.formatSummary(),
    };
  }

  public reset() {
    this.shouldRefreshLinks = false;
    this.shouldRefreshMentions = false;
    this.eventCount = 0;
    this.startedAt = 0;
    this.keys.clear();
  }

  private track(payload?: BlockUpdatePayload) {
    const type = payload?.type?.toLowerCase() ?? 'unknown';
    const flavour = payload?.flavour?.toLowerCase() ?? 'unknown';
    const key = payload?.props?.key?.trim().toLowerCase() ?? '';
    const label = key ? `${type}:${flavour}:${key}` : `${type}:${flavour}`;
    this.keys.set(label, (this.keys.get(label) ?? 0) + 1);
  }

  private formatSummary() {
    const entries = [...this.keys.entries()]
      .sort((left, right) => right[1] - left[1])
      .slice(0, 6);

    if (entries.length === 0) {
      return 'updates=none';
    }

    return `updates=${entries.map(([label, count]) => `${label}x${count}`).join(',')}`;
  }
}

function shouldRefreshLinkedDocsForBlockChange(payload?: BlockUpdatePayload) {
  if (!payload) {
    return true;
  }

  const flavour = payload.flavour?.toLowerCase() ?? '';
  if (flavour === 'affine:embed-linked-doc') {
    return true;
  }

  if (payload.type !== 'update') {
    return false;
  }

  const key = payload.props?.key?.trim().toLowerCase() ?? '';
  if (!key) {
    return true;
  }

  return key === 'text'
    || key === 'pageid'
    || key === 'params'
    || key === 'title'
    || key === 'description'
    || key.includes('link')
    || key.includes('reference');
}

function shouldRefreshMentionsForBlockChange(payload?: BlockUpdatePayload) {
  if (!payload) {
    return true;
  }

  if (payload.type !== 'update') {
    return false;
  }

  const key = payload.props?.key?.trim().toLowerCase() ?? '';
  return !key || key === 'text';
}
