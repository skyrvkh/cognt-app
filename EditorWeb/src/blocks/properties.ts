export {
  CogntPropertiesBlockSpec,
  cogntPropertiesFlavour,
  cogntPropertiesTagName,
  createCogntSchemas,
  type CogntPropertiesBlockModel,
  type CogntPropertiesBlockProps,
  type CogntPropertyRow,
} from './properties-schema';
export { defineCogntPropertiesBlockElement } from './properties-view';
export { insertCogntPropertiesBlock } from './properties-insert';
export {
  createDefaultPropertiesRows,
  normalizeCogntProperties,
} from './properties-normalize';
export { upgradeSerializedCogntPropertiesSnapshots } from './properties-snapshot';
