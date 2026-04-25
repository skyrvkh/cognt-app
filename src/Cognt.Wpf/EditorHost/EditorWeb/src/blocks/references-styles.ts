import { css } from '@lit/reactive-element/css-tag.js';

export const cogntReferencesBlockStyles = css`
    .cognt-references {
      box-sizing: border-box;
      width: 100%;
      min-width: 0;
      margin: var(--affine-paragraph-margin, 10px 0);
      padding: 4px 0 8px;
      background: transparent;
      color: var(--affine-text-primary-color);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm);
      line-height: var(--affine-line-height);
    }

    .cognt-references__divider {
      width: 100%;
      height: 1px;
      margin: 0 0 12px;
      background: var(--affine-border-color);
    }

    .cognt-references__groups {
      display: grid;
      gap: 10px;
      min-width: 0;
    }

    .cognt-references__group {
      display: grid;
      gap: 4px;
      min-width: 0;
    }

    .cognt-references__label {
      margin: 0;
      color: var(--affine-text-secondary-color);
      font-size: var(--affine-font-sm);
      font-weight: 600;
      line-height: 22px;
    }

    .cognt-references__list {
      display: grid;
      gap: 2px;
      min-width: 0;
    }

    .cognt-references__item {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 2px;
      width: 100%;
      min-width: 0;
      min-height: 34px;
      padding: 6px 8px;
      border: 0;
      border-radius: 4px;
      background: transparent;
      color: var(--affine-text-primary-color);
      cursor: pointer;
      font-family: var(--affine-font-family);
      text-align: left;
    }

    .cognt-references__item:hover,
    .cognt-references__item:focus,
    .cognt-references__item:focus-visible {
      background: var(--affine-hover-color);
      outline: none;
    }

    .cognt-references__item-title {
      min-width: 0;
      overflow: hidden;
      color: var(--affine-text-primary-color);
      font-size: var(--affine-font-sm);
      font-weight: 600;
      line-height: 20px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .cognt-references__item-snippet,
    .cognt-references__empty {
      min-width: 0;
      overflow: hidden;
      color: var(--affine-text-secondary-color);
      font-size: var(--affine-font-xs);
      line-height: 1.45;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .cognt-references__empty {
      margin: 0;
      padding: 2px 8px 4px;
    }

    .cognt-references mark {
      border-radius: 4px;
      background: var(--affine-edgeless-yellow, #fff0a8);
      color: inherit;
      padding: 0 2px;
    }

    @media (max-width: 640px) {
      .cognt-references__item {
        padding-inline: 4px;
      }

      .cognt-references__item-title,
      .cognt-references__item-snippet {
        white-space: normal;
      }
    }
  `;
