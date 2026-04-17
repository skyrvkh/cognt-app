export type MentionTarget = {
  relativePath: string;
  title: string;
  fileName: string;
  recentRank?: number | null;
};

export type MentionViewMode = 'preview' | 'full';

export type MentionPreviewResult = {
  requestId: string;
  ok: boolean;
  relativePath: string;
  mode?: MentionViewMode;
  title?: string;
  documentContent?: string;
  error?: string;
};

export function filterMentionTargets(targets: MentionTarget[], query: string, limit?: number) {
  const normalizedQuery = query.trim().toLowerCase();

  if (normalizedQuery.length === 0) {
    return limitMentionTargets([...targets].sort(compareMentionTargetsByRecentThenPath), limit);
  }

  const matches = [...targets]
    .filter(target => {
      const relativePath = target.relativePath.toLowerCase();
      const title = target.title.toLowerCase();
      const fileName = target.fileName.toLowerCase();
      return (
        title.includes(normalizedQuery) ||
        relativePath.includes(normalizedQuery) ||
        fileName.includes(normalizedQuery)
      );
    })
    .sort(compareMentionTargetsByTitleThenPath);

  return limitMentionTargets(matches, limit);
}

export function parseNoteMentionTarget(value: string) {
  const [relativePath] = value.split('|');
  const normalizedPath = relativePath.trim();

  if (!normalizedPath) {
    return null;
  }

  return {
    relativePath: normalizedPath,
  };
}

export function buildMentionPreviewHtml(result: MentionPreviewResult) {
  if (!result.ok) {
    return `<div class="mention-preview-body mention-preview-body--static"><p class="mention-preview-error">${escapeHtml(
      result.error ?? 'Preview unavailable.'
    )}</p></div>`;
  }

  const body = renderPreviewDocument(result.documentContent ?? '');
  return `<div class="mention-preview-body" tabindex="0">${body}</div>`;
}

export function escapeHtml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function compareMentionTargetsByRecentThenPath(left: MentionTarget, right: MentionTarget) {
  const leftRank = typeof left.recentRank === 'number' ? left.recentRank : Number.POSITIVE_INFINITY;
  const rightRank = typeof right.recentRank === 'number' ? right.recentRank : Number.POSITIVE_INFINITY;

  if (leftRank !== rightRank) {
    return leftRank - rightRank;
  }

  return compareMentionTargetsByTitleThenPath(left, right);
}

function compareMentionTargetsByTitleThenPath(left: MentionTarget, right: MentionTarget) {
  const titleCompare = left.title.localeCompare(right.title);
  return titleCompare !== 0 ? titleCompare : left.relativePath.localeCompare(right.relativePath);
}

function limitMentionTargets(targets: MentionTarget[], limit: number | undefined) {
  return typeof limit === 'number' ? targets.slice(0, limit) : targets;
}

function renderPreviewDocument(content: string) {
  const lines = extractBlockSuitePreviewLines(content);

  if (lines.length === 0) {
    return '<p class="mention-preview-empty">Empty note.</p>';
  }

  return lines
    .slice(0, 12)
    .map(line => `<p>${escapeHtml(line)}</p>`)
    .join('');
}

function extractBlockSuitePreviewLines(content: string) {
  try {
    const parsed = JSON.parse(content) as unknown;
    const snapshot = getSnapshotRoot(parsed);
    const lines: string[] = [];

    if (snapshot) {
      collectSnapshotText(snapshot.blocks, lines);
    }

    return lines
      .map(line => line.replace(/\s+/g, ' ').trim())
      .filter(Boolean);
  } catch {
    return content
      .replace(/\r\n?/g, '\n')
      .split('\n')
      .map(line => line.replace(/\s+/g, ' ').trim())
      .filter(Boolean);
  }
}

function getSnapshotRoot(value: unknown) {
  if (!isRecord(value)) {
    return null;
  }

  const maybeSnapshot = isRecord(value.snapshot) ? value.snapshot : value;
  return isRecord(maybeSnapshot) && isRecord(maybeSnapshot.blocks)
    ? (maybeSnapshot as { blocks: Record<string, unknown> })
    : null;
}

function collectSnapshotText(block: Record<string, unknown>, lines: string[]) {
  const props = isRecord(block.props) ? block.props : null;
  const text = props
    ? extractTextValue(props.text) || extractTextValue(props.title)
    : '';

  if (text.trim().length > 0) {
    lines.push(text);
  }

  const children = Array.isArray(block.children) ? block.children : [];
  for (const child of children) {
    if (isRecord(child)) {
      collectSnapshotText(child, lines);
    }
  }
}

function extractTextValue(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }

  if (Array.isArray(value)) {
    return value.map(extractTextValue).join('');
  }

  if (!isRecord(value)) {
    return '';
  }

  if ('insert' in value) {
    return extractTextValue(value.insert);
  }

  if ('delta' in value) {
    return extractTextValue(value.delta);
  }

  return '';
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
