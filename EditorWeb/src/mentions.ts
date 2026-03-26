export type MentionTarget = {
  relativePath: string;
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
  markdownExcerpt?: string;
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
      const fileName = target.fileName.toLowerCase();
      return relativePath.includes(normalizedQuery) || fileName.includes(normalizedQuery);
    })
    .sort((left, right) => left.relativePath.localeCompare(right.relativePath));

  return limitMentionTargets(matches, limit);
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
    return `<div class="mention-preview-body mention-preview-body--static"><p class="mention-preview-error">${escapeHtml(
      result.error ?? 'Preview unavailable.'
    )}</p></div>`;
  }

  const body = renderPreviewMarkdown(result.markdownExcerpt ?? '');
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

  return left.relativePath.localeCompare(right.relativePath);
}

function limitMentionTargets(targets: MentionTarget[], limit: number | undefined) {
  return typeof limit === 'number' ? targets.slice(0, limit) : targets;
}

function renderPreviewMarkdown(markdown: string) {
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
  const html: string[] = [];
  let paragraphLines: string[] = [];
  let list: { tag: 'ol' | 'ul'; items: string[] } | null = null;
  let blockquoteLines: string[] = [];
  let codeFence: { marker: string; lines: string[] } | null = null;

  const flushParagraph = () => {
    if (paragraphLines.length === 0) {
      return;
    }

    html.push(`<p>${renderInlineMarkdown(paragraphLines.join(' '))}</p>`);
    paragraphLines = [];
  };

  const flushList = () => {
    if (!list) {
      return;
    }

    html.push(
      `<${list.tag}>${list.items.map(item => `<li>${renderInlineMarkdown(item)}</li>`).join('')}</${
        list.tag
      }>`
    );
    list = null;
  };

  const flushBlockquote = () => {
    if (blockquoteLines.length === 0) {
      return;
    }

    html.push(`<blockquote>${renderPreviewMarkdown(blockquoteLines.join('\n'))}</blockquote>`);
    blockquoteLines = [];
  };

  const flushOpenBlocks = () => {
    flushParagraph();
    flushList();
    flushBlockquote();
  };

  for (let index = 0; index < lines.length; index++) {
    const rawLine = lines[index];
    const line = rawLine.trimEnd();

    if (codeFence) {
      const closingFence = getFenceMarker(line);
      if (closingFence === codeFence.marker) {
        html.push(`<pre><code>${escapeHtml(codeFence.lines.join('\n'))}</code></pre>`);
        codeFence = null;
      } else {
        codeFence.lines.push(rawLine);
      }

      continue;
    }

    if (line.trim().length === 0) {
      flushOpenBlocks();
      continue;
    }

    const fenceMarker = getFenceMarker(line);
    if (fenceMarker) {
      flushOpenBlocks();
      codeFence = { marker: fenceMarker, lines: [] };
      continue;
    }

    const table = readMarkdownTable(lines, index);
    if (table) {
      flushOpenBlocks();
      html.push(renderMarkdownTable(table.rows));
      index = table.endIndex;
      continue;
    }

    const heading = /^(#{1,6})\s+(.+)$/.exec(line);
    if (heading) {
      flushOpenBlocks();
      const level = heading[1].length;
      html.push(`<h${level}>${renderInlineMarkdown(heading[2])}</h${level}>`);
      continue;
    }

    if (/^\s{0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)) {
      flushOpenBlocks();
      html.push('<hr>');
      continue;
    }

    const quote = /^\s{0,3}>\s?(.*)$/.exec(line);
    if (quote) {
      flushParagraph();
      flushList();
      blockquoteLines.push(quote[1]);
      continue;
    }

    const unordered = /^\s*[-*+]\s+(.+)$/.exec(line);
    if (unordered) {
      flushParagraph();
      flushBlockquote();
      list = appendListItem(list, 'ul', unordered[1]);
      continue;
    }

    const ordered = /^\s*\d+[.)]\s+(.+)$/.exec(line);
    if (ordered) {
      flushParagraph();
      flushBlockquote();
      list = appendListItem(list, 'ol', ordered[1]);
      continue;
    }

    flushList();
    flushBlockquote();
    paragraphLines.push(line.trim());
  }

  if (codeFence) {
    html.push(`<pre><code>${escapeHtml(codeFence.lines.join('\n'))}</code></pre>`);
  }

  flushOpenBlocks();
  return html.join('') || '<p class="mention-preview-empty">Empty note.</p>';
}

function renderInlineMarkdown(value: string) {
  const placeholders: string[] = [];
  const withCodePlaceholders = value.replace(/`([^`]+)`/g, (_match, code: string) => {
    const key = `%%COGNTCODE${placeholders.length}%%`;
    placeholders.push(`<code>${escapeHtml(code)}</code>`);
    return key;
  });

  let rendered = escapeHtml(withCodePlaceholders);
  rendered = rendered.replace(/!\[([^\]]*)\]\([^)]+\)/g, '<em class="mention-preview-image">$1</em>');
  rendered = rendered.replace(/\[([^\]]+)\]\([^)]+\)/g, '<span class="mention-preview-link">$1</span>');
  rendered = rendered.replace(/~~([^~]+)~~/g, '<del>$1</del>');
  rendered = rendered.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  rendered = rendered.replace(/__([^_]+)__/g, '<strong>$1</strong>');
  rendered = rendered.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  rendered = rendered.replace(/(^|[^_])_([^_]+)_/g, '$1<em>$2</em>');

  return placeholders.reduce(
    (html, placeholder, index) => html.replace(`%%COGNTCODE${index}%%`, placeholder),
    rendered
  );
}

function appendListItem(
  list: { tag: 'ol' | 'ul'; items: string[] } | null,
  tag: 'ol' | 'ul',
  item: string
) {
  if (!list || list.tag !== tag) {
    return { tag, items: [item] };
  }

  list.items.push(item);
  return list;
}

function getFenceMarker(line: string) {
  const match = /^\s{0,3}(```|~~~)/.exec(line);
  return match?.[1] ?? null;
}

function readMarkdownTable(lines: string[], startIndex: number) {
  const header = splitTableRow(lines[startIndex]);
  if (header.length < 2 || startIndex + 1 >= lines.length) {
    return null;
  }

  const separator = splitTableRow(lines[startIndex + 1]);
  if (
    separator.length !== header.length ||
    !separator.every(cell => /^:?-{3,}:?$/.test(cell.trim()))
  ) {
    return null;
  }

  const rows = [header];
  let endIndex = startIndex + 1;

  for (let index = startIndex + 2; index < lines.length; index++) {
    const line = lines[index];
    if (line.trim().length === 0 || !line.includes('|')) {
      break;
    }

    rows.push(splitTableRow(line));
    endIndex = index;
  }

  return { rows, endIndex };
}

function splitTableRow(line: string) {
  const trimmed = line.trim().replace(/^\|/, '').replace(/\|$/, '');
  return trimmed.split('|').map(cell => cell.trim());
}

function renderMarkdownTable(rows: string[][]) {
  const header = rows[0];
  const bodyRows = rows.slice(1);
  const head = `<thead><tr>${header
    .map(cell => `<th>${renderInlineMarkdown(cell)}</th>`)
    .join('')}</tr></thead>`;
  const body =
    bodyRows.length > 0
      ? `<tbody>${bodyRows
          .map(
            row =>
              `<tr>${header
                .map((_cell, index) => `<td>${renderInlineMarkdown(row[index] ?? '')}</td>`)
                .join('')}</tr>`
          )
          .join('')}</tbody>`
      : '';

  return `<table>${head}${body}</table>`;
}
