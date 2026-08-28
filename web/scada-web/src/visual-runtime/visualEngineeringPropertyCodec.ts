import { VisualObjectPropertySchema } from './visualPropertyRegistry';
import {
  cloneVisualPropertyValue,
  VisualPropertyContractError,
  type VisualPropertyValue
} from './visualPropertyTypes';

/**
 * Validates JSON-native canonical Engineering values against the same property
 * schema consumed by Runtime and the future Property Inspector. No textual
 * coercion or type guessing occurs on the current schema path.
 */
export function decodeVisualEngineeringProperties(
  serializedValues: Readonly<Record<string, unknown>> | null | undefined,
  schema: VisualObjectPropertySchema
): Readonly<Record<string, VisualPropertyValue>> {
  const decoded: Record<string, VisualPropertyValue> = Object.create(null) as Record<string, VisualPropertyValue>;

  for (const [propertyKey, candidate] of Object.entries(serializedValues ?? {})) {
    const validation = schema.validate(propertyKey, candidate);
    if (!validation.ok) {
      throw new VisualPropertyContractError(
        `engineeringProperty.${validation.code}`,
        `Canonical Engineering value for '${propertyKey}' is invalid: ${validation.code}.`,
        propertyKey
      );
    }

    decoded[propertyKey] = cloneVisualPropertyValue(validation.value);
  }

  return Object.freeze(decoded);
}

/**
 * Produces a JSON-native Engineering property bag from already typed values.
 * Values are validated and cloned so no caller-owned mutable object becomes
 * canonical project state by reference.
 */
export function encodeVisualEngineeringProperties(
  typedValues: Readonly<Record<string, VisualPropertyValue>> | null | undefined,
  schema: VisualObjectPropertySchema
): Readonly<Record<string, VisualPropertyValue>> {
  const encoded: Record<string, VisualPropertyValue> = Object.create(null) as Record<string, VisualPropertyValue>;

  for (const [propertyKey, candidate] of Object.entries(typedValues ?? {})) {
    const validation = schema.validate(propertyKey, candidate);
    if (!validation.ok) {
      throw new VisualPropertyContractError(
        `engineeringProperty.${validation.code}`,
        `Typed Engineering value for '${propertyKey}' is invalid: ${validation.code}.`,
        propertyKey
      );
    }

    encoded[propertyKey] = cloneVisualPropertyValue(validation.value);
  }

  return Object.freeze(encoded);
}
