import {
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  type CommonVisualPropertyKey
} from './visualPropertyRegistry';

export const BUILTIN_VISUAL_OBJECT_TYPES = {
  group: 'core.group',
  rectangle: 'core.rectangle',
  ellipse: 'core.ellipse',
  line: 'core.line',
  polygon: 'core.polygon',
  text: 'core.text',
  image: 'core.image',
  valueDisplay: 'core.valueDisplay',
  button: 'core.button'
} as const;

export type BuiltinVisualObjectType = typeof BUILTIN_VISUAL_OBJECT_TYPES[keyof typeof BUILTIN_VISUAL_OBJECT_TYPES];

const GEOMETRY: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.x,
  VISUAL_PROPERTY_KEYS.y,
  VISUAL_PROPERTY_KEYS.width,
  VISUAL_PROPERTY_KEYS.height,
  VISUAL_PROPERTY_KEYS.zIndex
];

const TRANSFORM: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.rotation,
  VISUAL_PROPERTY_KEYS.scaleX,
  VISUAL_PROPERTY_KEYS.scaleY
];

const VISIBILITY: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.visible,
  VISUAL_PROPERTY_KEYS.opacity
];

const STROKE: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.strokeColor,
  VISUAL_PROPERTY_KEYS.strokeWidth,
  VISUAL_PROPERTY_KEYS.strokeStyle
];

const TEXT: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.text,
  VISUAL_PROPERTY_KEYS.textColor,
  VISUAL_PROPERTY_KEYS.fontFamily,
  VISUAL_PROPERTY_KEYS.fontSize,
  VISUAL_PROPERTY_KEYS.fontWeight,
  VISUAL_PROPERTY_KEYS.fontStyle,
  VISUAL_PROPERTY_KEYS.horizontalAlignment,
  VISUAL_PROPERTY_KEYS.verticalAlignment
];

const BASE = [...GEOMETRY, ...TRANSFORM, ...VISIBILITY] as const;

const schemas = new Map<BuiltinVisualObjectType, VisualObjectPropertySchema>([
  [BUILTIN_VISUAL_OBJECT_TYPES.group, schema(BUILTIN_VISUAL_OBJECT_TYPES.group, BASE)],
  [BUILTIN_VISUAL_OBJECT_TYPES.rectangle, schema(BUILTIN_VISUAL_OBJECT_TYPES.rectangle, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.fillColor,
    ...STROKE,
    VISUAL_PROPERTY_KEYS.cornerRadius
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.ellipse, schema(BUILTIN_VISUAL_OBJECT_TYPES.ellipse, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.fillColor,
    ...STROKE
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.line, schema(BUILTIN_VISUAL_OBJECT_TYPES.line, [
    ...BASE,
    ...STROKE
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.polygon, schema(BUILTIN_VISUAL_OBJECT_TYPES.polygon, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.fillColor,
    ...STROKE
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.text, schema(BUILTIN_VISUAL_OBJECT_TYPES.text, [
    ...BASE,
    ...TEXT
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.image, schema(BUILTIN_VISUAL_OBJECT_TYPES.image, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.imageFit,
    VISUAL_PROPERTY_KEYS.imagePositionX,
    VISUAL_PROPERTY_KEYS.imagePositionY
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay, schema(BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.backgroundColor,
    ...STROKE,
    VISUAL_PROPERTY_KEYS.cornerRadius,
    ...TEXT
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.button, schema(BUILTIN_VISUAL_OBJECT_TYPES.button, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.backgroundColor,
    ...STROKE,
    VISUAL_PROPERTY_KEYS.cornerRadius,
    ...TEXT
  ])]
]);

export function getBuiltinVisualObjectSchema(objectType: string): VisualObjectPropertySchema {
  const result = schemas.get(objectType as BuiltinVisualObjectType);
  if (!result) {
    throw new Error(`Unknown built-in visual object type '${objectType}'.`);
  }
  return result;
}

export function listBuiltinVisualObjectSchemas(): readonly VisualObjectPropertySchema[] {
  return Object.freeze([...schemas.values()]);
}

function schema(
  objectType: BuiltinVisualObjectType,
  propertyKeys: readonly CommonVisualPropertyKey[]
): VisualObjectPropertySchema {
  return new VisualObjectPropertySchema(objectType, propertyKeys);
}
