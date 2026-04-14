import { BlockComponent } from '@blocksuite/block-std';
import { PlusIcon, RemoveIcon, TitleIcon } from '@blocksuite/icons/lit';
import { html, nothing } from 'lit-html';
import { createBlockId } from '../documents/block-suite-document';
import {
  configureNonEditableBlockView,
  defineBlockElementOnce,
  selectCurrentBlock,
  stopBlockControlEvent,
  stopBlockEventPropagation,
} from './block-view-helpers';
import {
  cogntPropertiesTagName,
  type CogntPropertiesBlockModel,
  type CogntPropertyRow,
} from './properties-schema';
import { normalizeCogntProperties } from './properties-normalize';
import { cogntPropertiesBlockStyles } from './properties-styles';

class CogntPropertiesBlockComponent extends BlockComponent<CogntPropertiesBlockModel> {
  static styles = cogntPropertiesBlockStyles;

  connectedCallback() {
    super.connectedCallback();
    configureNonEditableBlockView(this);
  }

  private get rows() {
    return normalizeCogntProperties(this.model.properties);
  }

  private updateRows(rows: CogntPropertyRow[]) {
    if (this.doc.readonly) {
      return;
    }

    this.doc.captureSync();
    this.doc.updateBlock(this.model, { properties: rows } as never);
    this.requestUpdate();
  }

  private updateRow(index: number, key: 'name' | 'value', value: string) {
    const rows = this.rows;
    const row = rows[index];

    if (!row) {
      return;
    }

    rows[index] = { ...row, [key]: value };
    this.updateRows(rows);
  }

  private addRow(event: Event) {
    stopBlockControlEvent(event);
    this.updateRows([
      ...this.rows,
      { id: createBlockId(), name: 'property', value: '' },
    ]);
  }

  private removeRow(event: Event, index: number) {
    stopBlockControlEvent(event);
    const rows = this.rows;

    if (rows.length <= 1) {
      return;
    }

    rows.splice(index, 1);
    this.updateRows(rows);
  }

  private stopControlEvent(event: Event) {
    stopBlockEventPropagation(event);
  }

  private selectBlock(event: Event) {
    selectCurrentBlock(this, event);
  }

  renderBlock() {
    const rows = this.rows;
    const readonly = this.doc.readonly;

    return html`
      <section
        class="cognt-properties"
        data-cognt-block="properties"
        @click=${this.selectBlock}
      >
        <div class="cognt-properties__header">
          <span class="cognt-properties__title-icon" aria-hidden="true">
            ${TitleIcon()}
          </span>
          <h3 class="cognt-properties__title">${this.model.title || 'Properties'}</h3>
          ${readonly
            ? nothing
            : html`
                <button
                  class="cognt-properties__button cognt-properties__add"
                  title="Add property"
                  type="button"
                  @click=${this.addRow}
                  @mousedown=${this.stopControlEvent}
                >
                  ${PlusIcon()}
                  <span class="cognt-properties__add-label">Property</span>
                </button>
              `}
        </div>
        <div class="cognt-properties__rows">
          ${rows.map((row, index) => html`
            <div class="cognt-properties__row" data-property-id=${row.id}>
              <span class="cognt-properties__row-icon" aria-hidden="true"></span>
              <input
                class="cognt-properties__input cognt-properties__input--name"
                aria-label="Property name"
                placeholder="Property"
                .value=${row.name}
                ?readonly=${readonly}
                @input=${(event: Event) =>
                  this.updateRow(index, 'name', (event.currentTarget as HTMLInputElement).value)}
                @click=${this.stopControlEvent}
                @mousedown=${this.stopControlEvent}
              />
              <input
                class="cognt-properties__input"
                aria-label="Property value"
                placeholder="Empty"
                .value=${row.value}
                ?readonly=${readonly}
                @input=${(event: Event) =>
                  this.updateRow(index, 'value', (event.currentTarget as HTMLInputElement).value)}
                @click=${this.stopControlEvent}
                @mousedown=${this.stopControlEvent}
              />
              ${readonly
                ? nothing
                : html`
                    <button
                      class="cognt-properties__button cognt-properties__remove"
                      title="Remove property"
                      type="button"
                      ?disabled=${rows.length <= 1}
                      @click=${(event: Event) => this.removeRow(event, index)}
                      @mousedown=${this.stopControlEvent}
                    >
                      ${RemoveIcon()}
                    </button>
                  `}
            </div>
          `)}
        </div>
      </section>
    `;
  }
}

export function defineCogntPropertiesBlockElement() {
  defineBlockElementOnce(cogntPropertiesTagName, CogntPropertiesBlockComponent);
}
