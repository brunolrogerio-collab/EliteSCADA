import { VisualObjectPropertySchema, VISUAL_PROPERTY_KEYS } from './visualPropertyRegistry';
import { getBuiltinVisualObjectSchema } from './builtinVisualObjectSchemas';

const KNOWN_LEGACY_TYPES = new Set(['tank', 'value', 'dynamo', 'status']);
const COMPATIBILITY_PROPERTIES = [
  VISUAL_PROPERTY_KEYS.x,
  VISUAL_PROPERTY_KEYS.y,
  VISUAL_PROPERTY_KEYS.width,
  VISUAL_PROPERTY_KEYS.height,
  VISUAL_PROPERTY_KEYS.zIndex,
  VISUAL_PROPERTY_KEYS.rotation,
  VISUAL_PROPERTY_KEYS.scaleX,
  VISUAL_PROPERTY_KEYS.scaleY,
  VISUAL_PROPERTY_KEYS.visible,
  VISUAL_PROPERTY_KEYS.opacity,
  VISUAL_PROPERTY_KEYS.tooltip
] as const;

const compatibilitySchemas = new Map<string, VisualObjectPropertySchema>();

/**
 * Adapts only persisted, explicitly-known legacy object types for strict
 * Engineering consumers. It is not a renderer alias or a built-in registry.
 */
export function getVisualSchemaForEngineering(objectType: string): VisualObjectPropertySchema {
  try {
    return getBuiltinVisualObjectSchema(objectType);
  } catch {
    if (!KNOWN_LEGACY_TYPES.has(objectType)) throw new Error(`Unknown built-in visual object type '${objectType}'.`);
    let schema = compatibilitySchemas.get(objectType);
    if (!schema) {
      schema = new VisualObjectPropertySchema(`legacy.compatibility.${objectType}`, COMPATIBILITY_PROPERTIES);
      compatibilitySchemas.set(objectType, schema);
    }
    return schema;
  }
}

export function isKnownLegacyVisualType(objectType: string): boolean {
  return KNOWN_LEGACY_TYPES.has(objectType);
}
