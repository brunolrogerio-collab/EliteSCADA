export const VISUAL_PROPERTY_TYPES = [
  'number',
  'boolean',
  'string',
  'color',
  'enum',
  'assetRef'
] as const;

export type VisualPropertyType = typeof VISUAL_PROPERTY_TYPES[number];

export type AssetReference = Readonly<{
  assetId: string;
  name?: string;
  mediaType?: string;
}>;

export type VisualPropertyValue = number | boolean | string | AssetReference;

export type VisualPropertyDefinitionBase<TType extends VisualPropertyType, TValue> = Readonly<{
  key: string;
  type: TType;
  defaultValue: TValue;
  engineeringEditable: boolean;
  runtimeReadable: boolean;
  runtimeWritable: boolean;
  supportsBinding: boolean;
  animatable: boolean;
  unit?: string;
  presentationHint?: string;
  category?: string;
}>;

export type NumberVisualPropertyDefinition = VisualPropertyDefinitionBase<'number', number> & Readonly<{
  minimum?: number;
  maximum?: number;
}>;

export type BooleanVisualPropertyDefinition = VisualPropertyDefinitionBase<'boolean', boolean>;
export type StringVisualPropertyDefinition = VisualPropertyDefinitionBase<'string', string>;
export type ColorVisualPropertyDefinition = VisualPropertyDefinitionBase<'color', string>;

export type EnumVisualPropertyDefinition = VisualPropertyDefinitionBase<'enum', string> & Readonly<{
  allowedValues: readonly string[];
}>;

export type AssetRefVisualPropertyDefinition = VisualPropertyDefinitionBase<'assetRef', AssetReference>;

export type VisualPropertyDefinition =
  | NumberVisualPropertyDefinition
  | BooleanVisualPropertyDefinition
  | StringVisualPropertyDefinition
  | ColorVisualPropertyDefinition
  | EnumVisualPropertyDefinition
  | AssetRefVisualPropertyDefinition;

export type VisualPropertyValidationCode =
  | 'property.unregistered'
  | 'value.type'
  | 'number.nonFinite'
  | 'number.minimum'
  | 'number.maximum'
  | 'color.format'
  | 'enum.value'
  | 'assetRef.shape'
  | 'assetRef.id';

export type VisualPropertyValidationSuccess = Readonly<{
  ok: true;
  propertyKey: string;
  value: VisualPropertyValue;
}>;

export type VisualPropertyValidationFailure = Readonly<{
  ok: false;
  propertyKey: string;
  code: VisualPropertyValidationCode;
  detail?: string;
}>;

export type VisualPropertyValidationResult =
  | VisualPropertyValidationSuccess
  | VisualPropertyValidationFailure;

export class VisualPropertyContractError extends Error {
  readonly code: string;
  readonly propertyKey?: string;

  constructor(code: string, message: string, propertyKey?: string) {
    super(message);
    this.name = 'VisualPropertyContractError';
    this.code = code;
    this.propertyKey = propertyKey;
  }
}

export function cloneVisualPropertyValue(value: VisualPropertyValue): VisualPropertyValue {
  if (typeof value !== 'object') return value;
  return Object.freeze({ ...value });
}

export function isAssetReference(value: unknown): value is AssetReference {
  if (!isPlainRecord(value)) return false;

  const allowedKeys = new Set(['assetId', 'name', 'mediaType']);
  if (Object.keys(value).some(key => !allowedKeys.has(key))) return false;
  if (!isStableAssetId(value.assetId)) return false;
  if (value.name !== undefined && typeof value.name !== 'string') return false;
  if (value.mediaType !== undefined && typeof value.mediaType !== 'string') return false;

  return true;
}

export function isStableAssetId(value: unknown): value is string {
  return typeof value === 'string' &&
    value.length >= 1 &&
    value.length <= 128 &&
    /^[A-Za-z0-9][A-Za-z0-9._:-]*$/.test(value);
}

export function isStableVisualToken(value: unknown): value is string {
  return typeof value === 'string' &&
    value.length >= 1 &&
    value.length <= 160 &&
    value === value.trim() &&
    !/[\u0000-\u001F\u007F]/.test(value);
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}
