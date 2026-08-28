import { VisualObjectPropertySchema } from './visualPropertyRegistry';
import {
  cloneVisualPropertyValue,
  VisualPropertyContractError,
  type AssetReference,
  type VisualPropertyDefinition,
  type VisualPropertyValue
} from './visualPropertyTypes';

const CANONICAL_INTEGER = /^-?(?:0|[1-9][0-9]*)$/;
const CANONICAL_NUMBER = /^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$/;
const INT32_MIN = -2147483648;
const INT32_MAX = 2147483647;

/**
 * Transitional adapter for schema-v10/v11 string-valued visual Engineering.
 * Conversion is directed exclusively by the declared property schema. No value
 * is type-guessed from its textual contents.
 */
export function decodeLegacyVisualEngineeringProperties(
  serializedValues: Readonly<Record<string, string>> | null | undefined,
  schema: VisualObjectPropertySchema
): Readonly<Record<string, VisualPropertyValue>> {
  const decoded: Record<string, VisualPropertyValue> = Object.create(null) as Record<string, VisualPropertyValue>;

  for (const [propertyKey, serialized] of Object.entries(serializedValues ?? {})) {
    const definition = schema.getRequired(propertyKey);
    const candidate = decodeValue(definition, serialized);
    const validation = schema.validate(propertyKey, candidate);
    if (!validation.ok) {
      throw new VisualPropertyContractError(
        `legacyProperty.${validation.code}`,
        `Legacy Engineering value for '${propertyKey}' is invalid: ${validation.code}.`,
        propertyKey
      );
    }
    decoded[propertyKey] = cloneVisualPropertyValue(validation.value);
  }

  return Object.freeze(decoded);
}

/**
 * Encodes explicitly engineered typed values back into the legacy string bag.
 * A null assetRef is represented by absence of the property, not by a sentinel.
 */
export function encodeLegacyVisualEngineeringProperties(
  typedValues: Readonly<Record<string, VisualPropertyValue>> | null | undefined,
  schema: VisualObjectPropertySchema
): Readonly<Record<string, string>> {
  const encoded: Record<string, string> = Object.create(null) as Record<string, string>;

  for (const [propertyKey, candidate] of Object.entries(typedValues ?? {})) {
    const definition = schema.getRequired(propertyKey);
    const validation = schema.validate(propertyKey, candidate);
    if (!validation.ok) {
      throw new VisualPropertyContractError(
        `legacyProperty.${validation.code}`,
        `Typed Engineering value for '${propertyKey}' is invalid: ${validation.code}.`,
        propertyKey
      );
    }

    const serialized = encodeValue(definition, validation.value);
    if (serialized !== null) encoded[propertyKey] = serialized;
  }

  return Object.freeze(encoded);
}

function decodeValue(definition: VisualPropertyDefinition, serialized: string): VisualPropertyValue {
  if (typeof serialized !== 'string') {
    throw invalidSerialized(definition.key, 'Legacy visual property values must be strings.');
  }

  switch (definition.type) {
    case 'boolean':
      if (serialized === 'true') return true;
      if (serialized === 'false') return false;
      throw invalidSerialized(definition.key, "Boolean text must be exactly 'true' or 'false'.");

    case 'number': {
      if (definition.integer === true) {
        if (!CANONICAL_INTEGER.test(serialized)) {
          throw invalidSerialized(definition.key, 'Integer text is not canonical.');
        }
        const value = Number(serialized);
        if (!Number.isSafeInteger(value) || value < INT32_MIN || value > INT32_MAX) {
          throw invalidSerialized(definition.key, 'Integer is outside the supported 32-bit range.');
        }
        return value;
      }

      if (!CANONICAL_NUMBER.test(serialized)) {
        throw invalidSerialized(definition.key, 'Number text is not canonical.');
      }
      const value = Number(serialized);
      if (!Number.isFinite(value)) {
        throw invalidSerialized(definition.key, 'Number must be finite.');
      }
      return value;
    }

    case 'string':
    case 'color':
    case 'enum':
      return serialized;

    case 'assetRef':
      return Object.freeze({ assetId: serialized } satisfies AssetReference);
  }
}

function encodeValue(
  definition: VisualPropertyDefinition,
  value: VisualPropertyValue
): string | null {
  switch (definition.type) {
    case 'boolean':
      return value === true ? 'true' : 'false';

    case 'number':
      return String(value as number);

    case 'string':
    case 'color':
    case 'enum':
      return value as string;

    case 'assetRef':
      return value === null ? null : (value as AssetReference).assetId;
  }
}

function invalidSerialized(propertyKey: string, detail: string): VisualPropertyContractError {
  return new VisualPropertyContractError(
    'legacyProperty.invalidSerializedValue',
    `Legacy Engineering property '${propertyKey}' is invalid. ${detail}`,
    propertyKey
  );
}
