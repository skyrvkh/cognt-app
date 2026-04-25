import { BlockComponent } from '@blocksuite/block-std';
import { html, nothing, type TemplateResult } from 'lit-html';
import {
  configureNonEditableBlockView,
  defineBlockElementOnce,
  selectCurrentBlock,
  stopBlockControlEvent,
  stopBlockEventPropagation,
} from './block-view-helpers';
import {
  cogntReferencesTagName,
  type CogntReferenceItem,
  type CogntReferencesBlockModel,
} from './references-schema';
import { cogntReferencesBlockStyles } from './references-styles';

const openReferenceDocumentEvent = 'cognt-open-reference-document';

class CogntReferencesBlockComponent extends BlockComponent<CogntReferencesBlockModel> {
  static styles = cogntReferencesBlockStyles;

  connectedCallback() {
    super.connectedCallback();
    configureNonEditableBlockView(this);
  }

  private get incoming() {
    return Array.isArray(this.model.incoming) ? this.model.incoming : [];
  }

  private get outgoing() {
    return Array.isArray(this.model.outgoing) ? this.model.outgoing : [];
  }

  private selectBlock(event: Event) {
    selectCurrentBlock(this, event);
  }

  private stopControlEvent(event: Event) {
    stopBlockEventPropagation(event);
  }

  private openDocument(event: Event, item: CogntReferenceItem) {
    stopBlockControlEvent(event);

    this.dispatchEvent(new CustomEvent(openReferenceDocumentEvent, {
      bubbles: true,
      composed: true,
      detail: {
        relativePath: item.openRelativePath,
      },
    }));
  }

  private handleItemKeyDown(event: KeyboardEvent, item: CogntReferenceItem) {
    if (event.key === 'Enter') {
      this.openDocument(event, item);
    }
  }

  private renderGroup(label: string, items: CogntReferenceItem[]) {
    return html`
      <section class="cognt-references__group" aria-label=${label}>
        <h4 class="cognt-references__label">${label}</h4>
        ${items.length > 0
          ? html`
              <div class="cognt-references__list">
                ${items.map(item => this.renderItem(item))}
              </div>
            `
          : html`<p class="cognt-references__empty">No links.</p>`}
      </section>
    `;
  }

  private renderItem(item: CogntReferenceItem) {
    return html`
      <button
        class="cognt-references__item"
        type="button"
        aria-label=${item.direction === 'incoming'
          ? `${item.displayTitle} links to this document`
          : `This document links to ${item.displayTitle}`}
        @click=${(event: MouseEvent) => this.openDocument(event, item)}
        @keydown=${(event: KeyboardEvent) => this.handleItemKeyDown(event, item)}
        @mousedown=${this.stopControlEvent}
      >
        <span class="cognt-references__item-title">${item.displayTitle}</span>
        ${item.snippet
          ? html`
              <span class="cognt-references__item-snippet">
                ${renderHighlightedSnippet(item.snippet, item.matchedText)}
              </span>
            `
          : nothing}
      </button>
    `;
  }

  renderBlock() {
    return html`
      <section
        class="cognt-references"
        data-cognt-block="references"
        aria-label=${this.model.title || 'References'}
        @click=${this.selectBlock}
      >
        <div class="cognt-references__divider" aria-hidden="true"></div>
        <div class="cognt-references__groups">
          ${this.renderGroup('Inbound Links', this.incoming)}
          ${this.renderGroup('Outbound Links', this.outgoing)}
        </div>
      </section>
    `;
  }
}

export function defineCogntReferencesBlockElement() {
  defineBlockElementOnce(cogntReferencesTagName, CogntReferencesBlockComponent);
}

function renderHighlightedSnippet(snippet: string, matchedText: string): TemplateResult | string {
  const normalizedMatch = matchedText.trim();
  if (!normalizedMatch) {
    return snippet;
  }

  const index = snippet.toLocaleLowerCase().indexOf(normalizedMatch.toLocaleLowerCase());
  if (index < 0) {
    return snippet;
  }

  return html`
    ${snippet.slice(0, index)}
    <mark>${snippet.slice(index, index + normalizedMatch.length)}</mark>
    ${snippet.slice(index + normalizedMatch.length)}
  `;
}
