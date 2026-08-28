import type { VisualObjectPropertySchema } from './visualPropertyRegistry';
import type { VisualPropertyDefinition } from './visualPropertyTypes';
import type {
  VisualRuntimePropertyDefinitionPort,
  VisualRuntimePropertyRegistryPort,
  VisualRuntimePropertyValidation
} from './runtimeVisualPropertyPort';

/**
 * Internal adapter between the authoritative typed Visual Object schema and the
 * narrow Runtime Visual Instance consumer port. It intentionally accepts only
 * VisualObjectPropertySchema so Runtime cannot acquire a second registry authority.
 */
export function createRuntimeVisualPropertyRegistryPort(
  schema: VisualObjectPropertySchema
): VisualRuntimePropertyRegistryPort {
  return Object.freeze({
    find(propertyKey: string): VisualRuntimePropertyDefinitionPort | undefined {
      if (!schema.declares(propertyKey)) return undefined;
      return toRuntimeDefinition(schema.getRequired(propertyKey));
    },

    validate(propertyKey: string, value: unknown): VisualRuntimePropertyValidation {
      const result = schema.validate(propertyKey, value);
      if (result.ok) {
        return {
          valid: true,
          value: result.value
        };
      }
      return {
        valid: false,
        code: result.code,
        reason: result.detail ?? result.code
      };
    }
  });
}

function toRuntimeDefinition(
  definition: VisualPropertyDefinition
): VisualRuntimePropertyDefinitionPort {
  return Object.freeze({
    key: definition.key,
    defaultValue: definition.defaultValue,
    runtimeReadable: definition.runtimeReadable,
    runtimeWritable: definition.runtimeWritable,
    supportsBinding: definition.supportsBinding,
    animatable: definition.animatable
  });
}
