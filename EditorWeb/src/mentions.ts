export type MentionTarget = {
  path: string;
  relativePath: string;
  fileName: string;
  title: string;
  recentRank?: number | null;
};

export type MentionTrigger = {
  query: string;
  startIndex: number;
};

export type MentionViewMode = 'preview' | 'full';

export type MentionPreviewResult = {
  requestId: string;
  ok: boolean;
  relativePath: string;
  mode?: MentionViewMode;
  title?: string;
  markdownExcerpt?: string;
  error?: string;
};

export function getMentionTrigger(textBeforeCaret: string): MentionTrigger | null {
  const match = /(?:^|[\s([{])\[\[([^\]\[\n]{0,80})$/.exec(textBeforeCaret);
  if (!match) {
    return null;
  }

  return {
    query: match[1],
    startIndex: match.index + match[0].lastIndexOf('[['),
  };
}

export function filterMentionTargets(targets: MentionTarget[], query: string, limit = 8) {
  const normalizedQuery = query.trim().toLowerCase();

  if (normalizedQuery.length === 0) {
    return [...targets].sort(compareMentionTargetsByRecentThenPath).slice(0, limit);
  }

  return [...targets]
    .filter(target => {
      const relativePath = target.relativePath.toLowerCase();
      const fileName = target.fileName.toLowerCase();
      const title = target.title.toLowerCase();
      return (
        relativePath.includes(normalizedQuery) ||
        fileName.includes(normalizedQuery) ||
        title.includes(normalizedQuery)
      );
    })
    .sort((left, right) => left.relativePath.localeCompare(right.relativePath))
    .slice(0, limit);
}

export function findMentionAtOffset(text: string, offset: number) {
  const expression = /\[\[([^\]\[\n]+)\]\]/g;
  let match: RegExpExecArray | null;

  while ((match = expression.exec(text)) !== null) {
    const start = match.index;
    const end = start + match[0].length;

    if (offset >= start && offset <= end) {
      return {
        relativePath: match[1],
        start,
        end,
      };
    }
  }

  return null;
}

export function buildMentionPreviewHtml(result: MentionPreviewResult) {
  if (!result.ok) {
    return `<div class="mention-preview-error">${escapeHtml(
      result.error ?? 'Preview unavailable.'
    )}</div>`;
  }

  const title = escapeHtml(result.title || result.relativePath);
  const path = escapeHtml(result.relativePath);
  const body = renderPreviewMarkdown(result.markdownExcerpt ?? '');
  return `<div class="mention-preview-title">${title}</div><div class="mention-preview-path">${path}</div><div class="mention-preview-body">${body}</div>`;
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

  return left.relativePath.localeCompare(right.relativePath);
}

function renderPreviewMarkdown(markdown: string) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const html: string[] = [];
  let listItems: string[] = [];

  const flushList = () => {
    if (listItems.length === 0) {
      return;
    }

    html.push(`<ul>${listItems.map(item => `<li>${renderInlineMarkdown(item)}</li>`).join('')}</ul>`);
    listItems = [];
  };

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();

    if (line.trim().length === 0) {
      flushList();
      continue;
    }

    const heading = /^(#{1,6})\s+(.+)$/.exec(line);
    if (heading) {
      flushList();
      const level = Math.min(heading[1].length, 4);
      html.push(`<h${level}>${renderInlineMarkdown(heading[2])}</h${level}>`);
      continue;
    }

    const unordered = /^\s*[-*+]\s+(.+)$/.exec(line);
    if (unordered) {
      listItems.push(unordered[1]);
      continue;
    }

    flushList();
    html.push(`<p>${renderInlineMarkdown(line)}</p>`);
  }

  flushList();
  return html.join('');
}

function renderInlineMarkdown(value: string) {
  let rendered = escapeHtml(value);
  rendered = rendered.replace(/`([^`]+)`/g, '<code>$1</code>');
  rendered = rendered.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  rendered = rendered.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  rendered = rendered.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');
  return rendered;
}
