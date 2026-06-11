import type { PageEditor } from '@blocksuite/presets';
import type { Doc } from '@blocksuite/store';
import { importMarkdownIntoExistingDoc } from '../documents/io';
import { normalizeRichHtmlForPasteWithImages } from './rich-html-paste';
import { insertRichHtmlPasteImages } from './rich-html-paste-image-insertion';

type RichHtmlPasteAutomationOptions = {
  editor: PageEditor;
  getCurrentDoc(): Doc | null;
  readNativeClipboardImage(): Promise<string | null>;
  readImageSourceAsDataUrl(imageSource: string, fallbackFileName: string): Promise<string | null>;
  persistMutation(): Promise<void>;
};

export async function pasteRichHtmlForAutomation(
  options: RichHtmlPasteAutomationOptions,
  html: string,
  plainText = ''
) {
  const doc = options.getCurrentDoc();
  if (!doc) {
    throw new Error('No active document is loaded.');
  }

  const normalized = await normalizeRichHtmlForPasteWithImages({
    html,
    plainText,
    readNativeClipboardImage: options.readNativeClipboardImage,
    readImageSourceAsDataUrl: options.readImageSourceAsDataUrl,
  });
  const markdown = createMarkdownFromRichHtml(normalized.html) || plainText;

  await importMarkdownIntoExistingDoc(doc, markdown);
  if (normalized.images.length === 0) {
    await options.persistMutation();
    return;
  }

  const inserted = await insertRichHtmlPasteImages({
    editor: options.editor,
    getCurrentDoc: options.getCurrentDoc,
    persistAttachmentMutation: options.persistMutation,
  }, normalized.images);
  if (inserted === 0) {
    await options.persistMutation();
  }
}

function createMarkdownFromRichHtml(html: string) {
  const document = new DOMParser().parseFromString(html, 'text/html');
  return Array.from(document.body.children)
    .map(element => convertElementToMarkdown(element).trim())
    .filter(Boolean)
    .join('\n\n');
}

function convertElementToMarkdown(element: Element): string {
  const tagName = element.tagName.toLowerCase();
  if (tagName === 'img' || isPasteImageMarker(element)) {
    return '';
  }

  const content = convertInlineChildrenToMarkdown(element).trim();
  if (!content) {
    return '';
  }

  if (/^h[1-6]$/.test(tagName)) {
    return `${'#'.repeat(Number(tagName.slice(1)))} ${content}`;
  }

  if (tagName === 'li') {
    return `- ${content}`;
  }

  if (tagName === 'ul' || tagName === 'ol') {
    return Array.from(element.children)
      .map(child => convertElementToMarkdown(child))
      .filter(Boolean)
      .join('\n');
  }

  return content;
}

function convertInlineChildrenToMarkdown(element: Element): string {
  return Array.from(element.childNodes)
    .map(convertNodeToMarkdown)
    .join('')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n');
}

function convertNodeToMarkdown(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent ?? '';
  }

  if (!(node instanceof Element) || isPasteImageMarker(node)) {
    return '';
  }

  const tagName = node.tagName.toLowerCase();
  if (tagName === 'br') {
    return '\n';
  }

  const content = convertInlineChildrenToMarkdown(node);
  if (tagName === 'strong' || tagName === 'b') {
    return content.trim() ? `**${content}**` : '';
  }

  if (tagName === 'em' || tagName === 'i') {
    return content.trim() ? `*${content}*` : '';
  }

  if (tagName === 'code') {
    return content.trim() ? `\`${content}\`` : '';
  }

  if (tagName === 'a') {
    const href = node.getAttribute('href')?.trim();
    return href && content.trim() ? `[${content}](${href})` : content;
  }

  if (tagName === 'img') {
    return '';
  }

  return content;
}

function isPasteImageMarker(element: Element) {
  return /^\[\[cognt-paste-image:/.test(element.textContent?.trim() ?? '');
}
