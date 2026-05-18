export type CogntEditorTheme = {
  name?: string;
  Name?: string;
  mode?: string;
  Mode?: string;
  page?: string;
  Page?: string;
  surface?: string;
  Surface?: string;
  panel?: string;
  Panel?: string;
  panelAlt?: string;
  PanelAlt?: string;
  line?: string;
  Line?: string;
  ink?: string;
  Ink?: string;
  muted?: string;
  Muted?: string;
  accent?: string;
  Accent?: string;
  accentStrong?: string;
  AccentStrong?: string;
  accentSoft?: string;
  AccentSoft?: string;
  selection?: string;
  Selection?: string;
  cloze?: string;
  Cloze?: string;
};

type NormalizedEditorTheme = Required<{
  name: string;
  mode: 'light' | 'dark';
  page: string;
  surface: string;
  panel: string;
  panelAlt: string;
  line: string;
  ink: string;
  muted: string;
  accent: string;
  accentStrong: string;
  accentSoft: string;
  selection: string;
  cloze: string;
}>;

const fallbackTheme: NormalizedEditorTheme = {
  name: 'Light',
  mode: 'light',
  page: '#FFFFFF',
  surface: '#FFFFFF',
  panel: '#F8FAFF',
  panelAlt: '#F2F5FF',
  line: '#DDE4EF',
  ink: '#142033',
  muted: '#667085',
  accent: '#5867FF',
  accentStrong: '#4254E8',
  accentSoft: '#EEF2FF',
  selection: '#DDE7FF',
  cloze: '#DBE4EE',
};

export function applyCogntEditorTheme(hostElement: HTMLElement, theme: CogntEditorTheme) {
  const normalized = normalizeTheme(theme);
  const documentElement = hostElement.ownerDocument.documentElement;
  const body = hostElement.ownerDocument.body;
  const shell = hostElement.closest<HTMLElement>('.cognt-blocksuite-host') ?? hostElement;

  for (const target of [documentElement, body, shell, hostElement]) {
    applyVariables(target, normalized);
  }

  documentElement.dataset.theme = normalized.mode;
  body.dataset.theme = normalized.mode;
  documentElement.style.colorScheme = normalized.mode;
  body.style.colorScheme = normalized.mode;
  shell.dataset.cogntTheme = normalized.name;
  shell.dataset.cogntThemeMode = normalized.mode;
}

function applyVariables(element: HTMLElement, theme: NormalizedEditorTheme) {
  const variables: Record<string, string> = {
    '--cognt-font-sans': '"Inter", "Roboto", "Noto Sans", "Segoe UI Variable Text", "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
    '--cognt-font-display': '"Inter", "Roboto", "Noto Sans", "Segoe UI Variable Display", "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
    '--cognt-font-reading': '"Source Sans Pro", "Inter", "Roboto", "Noto Sans", "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
    '--cognt-font-mono': '"Roboto Mono", "Cascadia Mono", "Cascadia Code", "Consolas", monospace',
    '--cognt-editor-page': theme.page,
    '--cognt-editor-surface': theme.surface,
    '--cognt-editor-panel': theme.panel,
    '--cognt-editor-panel-alt': theme.panelAlt,
    '--cognt-editor-line': theme.line,
    '--cognt-editor-ink': theme.ink,
    '--cognt-editor-muted': theme.muted,
    '--cognt-editor-accent': theme.accent,
    '--cognt-editor-accent-strong': theme.accentStrong,
    '--cognt-editor-accent-soft': theme.accentSoft,
    '--cognt-editor-selection': theme.selection,
    '--cognt-editor-cloze': theme.cloze,
    '--affine-theme-mode': theme.mode,
    '--affine-font-family': 'var(--cognt-font-reading)',
    '--affine-font-code-family': 'var(--cognt-font-mono)',
    '--affine-font-h1': '40px',
    '--affine-font-h2': '32px',
    '--affine-font-h3': '26px',
    '--affine-font-h4': '21px',
    '--affine-font-base': '15px',
    '--affine-font-sm': '13px',
    '--affine-font-xs': '12px',
    '--affine-background-primary-color': theme.page,
    '--affine-background-secondary-color': theme.panel,
    '--affine-background-tertiary-color': theme.panelAlt,
    '--affine-background-overlay-panel-color': theme.surface,
    '--affine-background-code-block': theme.panelAlt,
    '--affine-page-background': theme.page,
    '--affine-text-primary-color': theme.ink,
    '--affine-text-secondary-color': theme.muted,
    '--affine-text-emphasis-color': theme.accentStrong,
    '--affine-text-disable-color': theme.muted,
    '--affine-placeholder-color': theme.muted,
    '--affine-border-color': theme.line,
    '--affine-divider-color': theme.line,
    '--affine-icon-color': theme.muted,
    '--affine-icon-secondary': theme.muted,
    '--affine-link-color': theme.accentStrong,
    '--affine-primary-color': theme.accent,
    '--affine-hover-color': theme.accentSoft,
    '--affine-selected-color': theme.selection,
    '--affine-blue-100': theme.selection,
    '--affine-blue': theme.accent,
    '--affine-black': theme.mode === 'dark' ? theme.ink : '#000000',
    '--affine-black-10': alpha(theme.ink, 0.1),
    '--affine-black-30': alpha(theme.ink, 0.3),
    '--affine-black-50': alpha(theme.ink, 0.5),
    '--affine-black-80': alpha(theme.ink, 0.8),
    '--affine-black-90': alpha(theme.ink, 0.9),
    '--affine-white': theme.mode === 'dark' ? theme.page : '#FFFFFF',
    '--affine-white-10': alpha(theme.page, 0.1),
    '--affine-white-30': alpha(theme.page, 0.3),
    '--affine-white-50': alpha(theme.page, 0.5),
    '--affine-white-80': alpha(theme.page, 0.8),
    '--affine-white-90': alpha(theme.page, 0.9),
    '--affine-pure-black': theme.mode === 'dark' ? theme.ink : '#000000',
    '--affine-pure-white': theme.mode === 'dark' ? theme.page : '#FFFFFF',
    '--affine-v2-text-primary': theme.ink,
    '--affine-v2-text-secondary': theme.muted,
    '--affine-v2-text-tertiary': theme.muted,
    '--affine-v2-text-disable': theme.muted,
    '--affine-v2-text-emphasis': theme.accentStrong,
    '--affine-v2-text-link': theme.accentStrong,
    '--affine-v2-text-placeholder': theme.muted,
    '--affine-v2-layer-background-primary': theme.page,
    '--affine-v2-layer-background-secondary': theme.panel,
    '--affine-v2-layer-background-tertiary': theme.panelAlt,
    '--affine-v2-layer-background-overlay-panel': theme.surface,
    '--affine-v2-layer-background-code-block': theme.panelAlt,
    '--affine-v2-layer-inside-border-border': theme.line,
    '--affine-v2-layer-inside-border-black-border': theme.line,
    '--affine-v2-layer-black': theme.mode === 'dark' ? theme.ink : '#000000',
    '--affine-v2-layer-white': theme.mode === 'dark' ? theme.page : '#FFFFFF',
    '--affine-v2-icon-primary': theme.ink,
    '--affine-v2-icon-secondary': theme.muted,
    '--affine-v2-icon-tertiary': theme.muted,
    '--affine-v2-icon-monotone': theme.muted,
  };

  for (const [name, value] of Object.entries(variables)) {
    element.style.setProperty(name, value);
  }
}

function normalizeTheme(theme: CogntEditorTheme): NormalizedEditorTheme {
  const mode = getValue(theme, 'mode').toLowerCase() === 'dark' ? 'dark' : 'light';

  return {
    name: getValue(theme, 'name') || fallbackTheme.name,
    mode,
    page: getValue(theme, 'page') || fallbackTheme.page,
    surface: getValue(theme, 'surface') || fallbackTheme.surface,
    panel: getValue(theme, 'panel') || fallbackTheme.panel,
    panelAlt: getValue(theme, 'panelAlt') || fallbackTheme.panelAlt,
    line: getValue(theme, 'line') || fallbackTheme.line,
    ink: getValue(theme, 'ink') || fallbackTheme.ink,
    muted: getValue(theme, 'muted') || fallbackTheme.muted,
    accent: getValue(theme, 'accent') || fallbackTheme.accent,
    accentStrong: getValue(theme, 'accentStrong') || fallbackTheme.accentStrong,
    accentSoft: getValue(theme, 'accentSoft') || fallbackTheme.accentSoft,
    selection: getValue(theme, 'selection') || fallbackTheme.selection,
    cloze: getValue(theme, 'cloze') || fallbackTheme.cloze,
  };
}

function getValue(theme: CogntEditorTheme, key: keyof CogntEditorTheme) {
  const value = theme[key] ?? theme[capitalize(key) as keyof CogntEditorTheme];
  return typeof value === 'string' ? value : '';
}

function capitalize(value: string) {
  return value.length === 0 ? value : `${value[0].toUpperCase()}${value.slice(1)}`;
}

function alpha(hex: string, opacity: number) {
  const normalized = hex.replace('#', '');
  if (normalized.length !== 6) {
    return hex;
  }

  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}
