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
  trend: 'core.trend',
  alarmBrowser: 'core.alarmBrowser',
  eventBrowser: 'core.eventBrowser',
  button: 'core.button',
  slider: 'core.slider'
} as const;

export type BuiltinVisualObjectType = typeof BUILTIN_VISUAL_OBJECT_TYPES[keyof typeof BUILTIN_VISUAL_OBJECT_TYPES];

const ANALOG_FILL_CAPABLE_TYPES = new Set<string>([
  BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
  BUILTIN_VISUAL_OBJECT_TYPES.ellipse
]);

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
  VISUAL_PROPERTY_KEYS.scaleY,
  VISUAL_PROPERTY_KEYS.horizontalFlip,
  VISUAL_PROPERTY_KEYS.verticalFlip
];

const VISIBILITY: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.visible,
  VISUAL_PROPERTY_KEYS.opacity,
  VISUAL_PROPERTY_KEYS.tooltip,
  VISUAL_PROPERTY_KEYS.enabled
];

const EFFECTS: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.shadowEnabled,
  VISUAL_PROPERTY_KEYS.shadowColor,
  VISUAL_PROPERTY_KEYS.shadowOffsetX,
  VISUAL_PROPERTY_KEYS.shadowOffsetY,
  VISUAL_PROPERTY_KEYS.shadowBlur
];

const FILL: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.fillStyle,
  VISUAL_PROPERTY_KEYS.fillColor,
  VISUAL_PROPERTY_KEYS.fillSecondaryColor,
  VISUAL_PROPERTY_KEYS.gradientDirection
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
  VISUAL_PROPERTY_KEYS.underline,
  VISUAL_PROPERTY_KEYS.textWrap,
  VISUAL_PROPERTY_KEYS.lineHeight,
  VISUAL_PROPERTY_KEYS.textOverflow,
  VISUAL_PROPERTY_KEYS.horizontalAlignment,
  VISUAL_PROPERTY_KEYS.verticalAlignment
];

const TREND: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.backgroundColor,
  VISUAL_PROPERTY_KEYS.strokeColor,
  VISUAL_PROPERTY_KEYS.strokeWidth,
  VISUAL_PROPERTY_KEYS.cornerRadius,
  VISUAL_PROPERTY_KEYS.trendMode,
  VISUAL_PROPERTY_KEYS.trendWindowSeconds,
  VISUAL_PROPERTY_KEYS.trendRefreshSeconds,
  VISUAL_PROPERTY_KEYS.trendLegendVisible,
  VISUAL_PROPERTY_KEYS.trendGridVisible,
  VISUAL_PROPERTY_KEYS.trendAxesVisible,
  VISUAL_PROPERTY_KEYS.trendQualityVisible
];

const BROWSER: readonly CommonVisualPropertyKey[] = [
  VISUAL_PROPERTY_KEYS.backgroundColor,
  VISUAL_PROPERTY_KEYS.strokeColor,
  VISUAL_PROPERTY_KEYS.strokeWidth,
  VISUAL_PROPERTY_KEYS.cornerRadius
];

const BASE = [...GEOMETRY, ...TRANSFORM, ...VISIBILITY, ...EFFECTS] as const;

const schemas = new Map<BuiltinVisualObjectType, VisualObjectPropertySchema>([
  [BUILTIN_VISUAL_OBJECT_TYPES.group, schema(BUILTIN_VISUAL_OBJECT_TYPES.group, BASE)],
  [BUILTIN_VISUAL_OBJECT_TYPES.rectangle, schema(BUILTIN_VISUAL_OBJECT_TYPES.rectangle, [
    ...BASE,
    ...FILL,
    ...STROKE,
    VISUAL_PROPERTY_KEYS.cornerRadius
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.ellipse, schema(BUILTIN_VISUAL_OBJECT_TYPES.ellipse, [
    ...BASE,
    ...FILL,
    ...STROKE
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.line, schema(BUILTIN_VISUAL_OBJECT_TYPES.line, [
    ...BASE,
    ...STROKE
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.polygon, schema(BUILTIN_VISUAL_OBJECT_TYPES.polygon, [
    ...BASE,
    ...FILL,
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
  [BUILTIN_VISUAL_OBJECT_TYPES.trend, schema(BUILTIN_VISUAL_OBJECT_TYPES.trend, [
    ...BASE,
    ...TREND
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser, schema(BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser, [
    ...BASE,
    ...BROWSER
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser, schema(BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser, [
    ...BASE,
    ...BROWSER
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.button, schema(BUILTIN_VISUAL_OBJECT_TYPES.button, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.backgroundColor,
    ...STROKE,
    VISUAL_PROPERTY_KEYS.cornerRadius,
    ...TEXT
  ])],
  [BUILTIN_VISUAL_OBJECT_TYPES.slider, schema(BUILTIN_VISUAL_OBJECT_TYPES.slider, [
    ...BASE,
    VISUAL_PROPERTY_KEYS.value,
    VISUAL_PROPERTY_KEYS.minimum,
    VISUAL_PROPERTY_KEYS.maximum,
    VISUAL_PROPERTY_KEYS.step,
    VISUAL_PROPERTY_KEYS.orientation,
    VISUAL_PROPERTY_KEYS.interactionEnabled,
    VISUAL_PROPERTY_KEYS.reverseDirection,
    VISUAL_PROPERTY_KEYS.trackColor,
    VISUAL_PROPERTY_KEYS.thumbColor,
    VISUAL_PROPERTY_KEYS.strokeColor,
    VISUAL_PROPERTY_KEYS.strokeWidth,
    VISUAL_PROPERTY_KEYS.cornerRadius
  ])]
]);

export function supportsAnalogFill(objectType: string): boolean {
  return ANALOG_FILL_CAPABLE_TYPES.has(objectType);
}

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
