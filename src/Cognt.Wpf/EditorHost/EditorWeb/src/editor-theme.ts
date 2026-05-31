import { reportDiagnostic } from './host';

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
  interfaceFontFamily?: string;
  InterfaceFontFamily?: string;
  displayFontFamily?: string;
  DisplayFontFamily?: string;
  readingFontFamily?: string;
  ReadingFontFamily?: string;
  codeFontFamily?: string;
  CodeFontFamily?: string;
  editorTextFontSize?: number;
  EditorTextFontSize?: number;
  editorHeadingFontSize?: number;
  EditorHeadingFontSize?: number;
  editorCodeFontSize?: number;
  EditorCodeFontSize?: number;
  editorZoomPercent?: number;
  EditorZoomPercent?: number;
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
  interfaceFontFamily: string;
  displayFontFamily: string;
  readingFontFamily: string;
  codeFontFamily: string;
  editorTextFontSize: number;
  editorHeadingFontSize: number;
  editorCodeFontSize: number;
  editorZoomPercent: number;
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
  interfaceFontFamily: 'Inter',
  displayFontFamily: 'Source Serif 4',
  readingFontFamily: 'Source Serif 4',
  codeFontFamily: 'Recursive',
  editorTextFontSize: 15,
  editorHeadingFontSize: 40,
  editorCodeFontSize: 13,
  editorZoomPercent: 100,
};

export function applyCogntEditorTheme(hostElement: HTMLElement, theme: CogntEditorTheme) {
  const normalized = normalizeTheme(theme);
  const documentElement = hostElement.ownerDocument.documentElement;
  const body = hostElement.ownerDocument.body;
  const shell = hostElement.closest<HTMLElement>('.cognt-blocksuite-host') ?? hostElement;

  for (const target of [documentElement, body, shell, hostElement]) {
    applyThemeVariables(target, normalized);
  }

  documentElement.dataset.theme = normalized.mode;
  body.dataset.theme = normalized.mode;
  documentElement.style.colorScheme = normalized.mode;
  body.style.colorScheme = normalized.mode;
  shell.dataset.cogntTheme = normalized.name;
  shell.dataset.cogntThemeMode = normalized.mode;
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
    interfaceFontFamily: cssFontStack(
      getValue(theme, 'interfaceFontFamily') || fallbackTheme.interfaceFontFamily,
      fallbackTheme.interfaceFontFamily),
    displayFontFamily: cssFontStack(
      getValue(theme, 'displayFontFamily') || fallbackTheme.displayFontFamily,
      fallbackTheme.displayFontFamily),
    readingFontFamily: cssFontStack(
      getValue(theme, 'readingFontFamily') || fallbackTheme.readingFontFamily,
      fallbackTheme.readingFontFamily),
    codeFontFamily: cssFontStack(
      getValue(theme, 'codeFontFamily') || fallbackTheme.codeFontFamily,
      fallbackTheme.codeFontFamily),
    editorTextFontSize: getNumberValue(theme, 'editorTextFontSize', fallbackTheme.editorTextFontSize, 10, 28),
    editorHeadingFontSize: getNumberValue(theme, 'editorHeadingFontSize', fallbackTheme.editorHeadingFontSize, 18, 72),
    editorCodeFontSize: getNumberValue(theme, 'editorCodeFontSize', fallbackTheme.editorCodeFontSize, 9, 24),
    editorZoomPercent: getNumberValue(theme, 'editorZoomPercent', fallbackTheme.editorZoomPercent, 50, 200),
  };
}

function applyThemeVariables(element: HTMLElement, theme: NormalizedEditorTheme) {
  const variables = buildThemeVariables(theme);
  for (const [name, value] of Object.entries(variables)) {
    element.style.setProperty(name, value);
  }
}

function buildThemeVariables(theme: NormalizedEditorTheme): Record<string, string> {
  const editorTextSize = Math.round(theme.editorTextFontSize);
  const headingSize = Math.round(theme.editorHeadingFontSize);
  const codeSize = Math.round(theme.editorCodeFontSize);
  const heading2Size = Math.max(editorTextSize + 8, Math.round(headingSize * 0.8));
  const heading3Size = Math.max(editorTextSize + 5, Math.round(headingSize * 0.65));
  const heading4Size = Math.max(editorTextSize + 2, Math.round(headingSize * 0.52));

  return {
    '--cognt-font-sans': theme.interfaceFontFamily,
    '--cognt-font-display': theme.displayFontFamily,
    '--cognt-font-reading': theme.readingFontFamily,
    '--cognt-font-mono': theme.codeFontFamily,
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
    '--affine-font-h1': `${headingSize}px`,
    '--affine-font-h2': `${heading2Size}px`,
    '--affine-font-h3': `${heading3Size}px`,
    '--affine-font-h4': `${heading4Size}px`,
    '--affine-font-base': `${editorTextSize}px`,
    '--affine-font-sm': `${Math.max(10, editorTextSize - 2)}px`,
    '--affine-font-xs': `${Math.max(9, editorTextSize - 3)}px`,
    '--cognt-editor-code-font-size': `${codeSize}px`,
    '--cognt-editor-zoom': `${theme.editorZoomPercent / 100}`,
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
}

function getValue(theme: CogntEditorTheme, key: keyof CogntEditorTheme) {
  const value = theme[key] ?? theme[capitalize(key) as keyof CogntEditorTheme];
  return typeof value === 'string' ? value : '';
}

function getNumberValue(
  theme: CogntEditorTheme,
  key: keyof CogntEditorTheme,
  fallback: number,
  minimum: number,
  maximum: number
) {
  const raw = theme[key] ?? theme[capitalize(key) as keyof CogntEditorTheme];
  const value = typeof raw === 'number' ? raw : Number.parseFloat(String(raw ?? ''));
  if (!Number.isFinite(value)) {
    reportDiagnostic('warn', 'Using fallback Cognt editor theme number value.', {
      key,
      fallback,
      minimum,
      maximum,
    });
    return fallback;
  }

  return Math.min(maximum, Math.max(minimum, Math.round(value)));
}

function cssFontStack(value: string, fallback: string) {
  const stack = value
    .split(',')
    .map(font => normalizeCssFontName(font.trim()))
    .filter(Boolean)
    .join(', ');

  return stack.length > 0 ? stack : fallback;
}

function normalizeCssFontName(font: string) {
  if (font.length === 0 || font.startsWith('"') || font.startsWith("'")) {
    return font;
  }

  if (/[^a-zA-Z-]/.test(font)) {
    return `"${font.replaceAll('"', '\\"')}"`;
  }

  return font;
}

function alpha(hex: string, opacity: number) {
  const normalized = hex.replace('#', '');
  if (normalized.length !== 6) {
    reportDiagnostic('warn', 'Skipped alpha conversion for non-hex color.', {
      hex,
      opacity,
    });
    return hex;
  }

  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}

function capitalize(value: string) {
  return value.length === 0 ? value : `${value[0].toUpperCase()}${value.slice(1)}`;
}
