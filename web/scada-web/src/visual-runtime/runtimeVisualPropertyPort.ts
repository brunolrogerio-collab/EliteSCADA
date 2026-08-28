export type VisualRuntimePropertyDefinitionPort = {
  key: string;
  defaultValue: unknown;
  runtimeReadable: boolean;
  runtimeWritable: boolean;
  supportsBinding: boolean;
  animatable: boolean;
};

export type VisualRuntimePropertyValidation =
  | { valid: true }
  | { valid: false; code: string; reason: string };

/**
 * Narrow consumer port for the Wave 07 Runtime Visual Instance.
 * DEV 1 owns the concrete typed registry and validation semantics; the coordinator
 * reconciles that registry to this port during integration.
 */
export interface VisualRuntimePropertyRegistryPort {
  find(propertyKey: string): VisualRuntimePropertyDefinitionPort | undefined;
  validate(propertyKey: string, value: unknown): VisualRuntimePropertyValidation;
}
