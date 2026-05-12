import { getNotePathFileName } from './links/note-path-utils';

export type MentionTarget = {
  relativePath: string;
  title: string;
};

export type MentionViewMode = 'preview' | 'full';

export type MentionPreviewResult = {
  requestId: string;
  ok: boolean;
  relativePath: string;
  mode?: MentionViewMode;
  documentContent?: string;
  error?: string;
};

export function filterMentionTargets(targets: MentionTarget[], query: string, limit?: number) {
  const normalizedQuery = query.trim().toLowerCase();

  if (normalizedQuery.length === 0) {
    return limitMentionTargets([...targets], limit);
  }

  const matches = [...targets]
    .filter(target => {
      const relativePath = target.relativePath.toLowerCase();
      const title = target.title.toLowerCase();
      const fileName = getMentionFileName(target).toLowerCase();
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

function compareMentionTargetsByTitleThenPath(left: MentionTarget, right: MentionTarget) {
  const titleCompare = left.title.localeCompare(right.title);
  return titleCompare !== 0 ? titleCompare : left.relativePath.localeCompare(right.relativePath);
}

function limitMentionTargets(targets: MentionTarget[], limit: number | undefined) {
  return typeof limit === 'number' ? targets.slice(0, limit) : targets;
}

export function getMentionFileName(target: MentionTarget) {
  return getNotePathFileName(target.relativePath);
}
