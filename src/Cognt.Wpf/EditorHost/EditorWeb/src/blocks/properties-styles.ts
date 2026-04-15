import { css } from 'lit';

export const cogntPropertiesBlockStyles = css`
    .cognt-properties {
      box-sizing: border-box;
      width: 100%;
      min-width: 0;
      margin: var(--affine-paragraph-margin, 10px 0);
      padding: 2px 0;
      border-radius: 6px;
      background: transparent;
      color: var(--affine-text-primary-color);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm);
      line-height: var(--affine-line-height);
    }

    .cognt-properties__header {
      display: grid;
      grid-template-columns: 24px minmax(0, 1fr) auto;
      align-items: center;
      gap: 8px;
      min-height: 34px;
      padding: 0 4px;
      border-radius: 4px;
    }

    .cognt-properties__header:hover {
      background: var(--affine-hover-color);
    }

    .cognt-properties__title-icon {
      display: inline-grid;
      place-items: center;
      width: 24px;
      height: 24px;
      color: var(--affine-icon-secondary);
    }

    .cognt-properties__title-icon svg {
      width: 18px;
      height: 18px;
      color: currentColor;
      fill: currentColor;
    }

    .cognt-properties__title {
      margin: 0;
      overflow: hidden;
      color: var(--affine-text-primary-color);
      font-size: var(--affine-font-base);
      font-weight: 600;
      line-height: 24px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .cognt-properties__rows {
      display: grid;
      margin-top: 2px;
      gap: 2px;
    }

    .cognt-properties__row {
      display: grid;
      grid-template-columns: 24px minmax(96px, 0.34fr) minmax(120px, 1fr) 28px;
      gap: 8px;
      align-items: center;
      min-height: 34px;
      padding: 0 4px;
      border-radius: 4px;
    }

    .cognt-properties__row:hover,
    .cognt-properties__row:focus-within {
      background: var(--affine-hover-color);
    }

    .cognt-properties__row-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      color: var(--affine-icon-secondary);
      cursor: grab;
    }

    .cognt-properties__row-icon::before {
      content: '';
      width: 14px;
      height: 14px;
      opacity: 0.72;
      background:
        radial-gradient(circle, currentColor 1.3px, transparent 1.4px) 0 1px / 7px 7px,
        radial-gradient(circle, currentColor 1.3px, transparent 1.4px) 3.5px 1px / 7px 7px;
    }

    .cognt-properties__input {
      box-sizing: border-box;
      width: 100%;
      min-width: 0;
      height: 28px;
      border: 1px solid transparent;
      border-radius: 4px;
      background: transparent;
      color: var(--affine-text-primary-color);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm);
      font-style: normal;
      font-weight: 400;
      line-height: 28px;
      padding: 0 6px;
      outline: none;
      text-overflow: ellipsis;
    }

    .cognt-properties__input--name {
      color: var(--affine-text-secondary-color);
      font-weight: 500;
    }

    .cognt-properties__input::placeholder {
      color: var(--affine-placeholder-color);
    }

    .cognt-properties__input:hover:not(:focus) {
      background: var(--affine-background-secondary-color);
    }

    .cognt-properties__input:focus,
    .cognt-properties__input:focus-visible {
      border-color: var(--affine-primary-color);
      background: var(--affine-background-primary-color);
      box-shadow: var(--affine-shadow-1);
    }

    .cognt-properties__button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 4px;
      height: 28px;
      min-width: 28px;
      border: 0;
      border-radius: 4px;
      background: transparent;
      color: var(--affine-text-secondary-color);
      font-family: var(--affine-font-family);
      font-size: var(--affine-font-sm);
      font-weight: 500;
      line-height: 1;
      padding: 0 6px;
      cursor: pointer;
    }

    .cognt-properties__button svg {
      width: 16px;
      height: 16px;
      color: currentColor;
      fill: currentColor;
    }

    .cognt-properties__button:hover,
    .cognt-properties__button:focus-visible {
      background: var(--affine-hover-color);
      color: var(--affine-text-primary-color);
      outline: none;
    }

    .cognt-properties__button[disabled] {
      cursor: default;
      opacity: 0.35;
    }

    .cognt-properties__add {
      width: auto;
      min-width: 0;
      padding: 0 10px;
      color: var(--affine-text-secondary-color);
    }

    .cognt-properties__remove {
      opacity: 0;
    }

    .cognt-properties__row:hover .cognt-properties__remove,
    .cognt-properties__row:focus-within .cognt-properties__remove {
      opacity: 1;
    }

    @media (max-width: 640px) {
      .cognt-properties__row {
        grid-template-columns: 20px minmax(72px, 0.4fr) minmax(96px, 1fr) 28px;
        gap: 6px;
      }

      .cognt-properties__add-label {
        display: none;
      }
    }
  `;
