import type {
  VisualPropertyDefinition,
  VisualPropertyValidationResult
} from './visualPropertyTypes';
import type {
  VisualRuntimePropertyDefinitionPort,
  VisualRuntimePropertyRegistryPort,
  VisualRuntimePropertyValidation
} from './runtimeVisualPropertyPort';

type VisualPropertyLookupSource = Readonly<{
  getRequired(propertyKey: string): VisualPropertyDefinition;
  get?: (propertyKey: string) => VisualPropertyDefinition | undefined;
  declares?: (propertyKey: string) => boolean;
  validate(propertyKey: string, value: unknown): VisualPropertyValidationResult;
}>;

/**
 * Coordinator-owned internal adapter between the typed Engineering property registry/schema
 * and the narrow Runtime Visual Instance consumer port.
 */
export function createRuntimeVisualPropertyRegistryPort(
  source: VisualPropertyLookupSource
): VisualRuntimePropertyRegistryPort {
  return Object.freeze({
    find(propertyKey: string): VisualRuntimePropertyDefinitionPort | undefined {
      const definition = findDefinition(source, propertyKey);
      if (!definition) return undefined;
      return toRuntimeDefinition(definition);
    },

    validate(propertyKey: string, value: unknown): VisualRuntimePropertyValidation {
      const result = source.validate(propertyKey, value);
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

function findDefinition(
  source: VisualPropertyLookupSource,
  propertyKey: string
): VisualPropertyDefinition | undefined {
  if (source.declares) {
    return source.declares(propertyKey) ? source.getRequired(propertyKey) : undefined;
  }
  return source.get?.(propertyKey);
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
