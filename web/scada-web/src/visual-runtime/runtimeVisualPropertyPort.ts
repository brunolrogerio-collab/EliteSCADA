export type VisualRuntimePropertyDefinitionPort = {
  key: string;
  defaultValue: unknown;
  runtimeReadable: boolean;
  runtimeWritable: boolean;
  supportsBinding: boolean;
  animatable: boolean;
};

export type VisualRuntimePropertyValidation =
  | { valid: true; value: unknown }
  | { valid: false; code: string; reason: string };

/**
 * Narrow internal consumer port for Runtime Visual Instance resolution.
 * The public runtime constructor consumes a VisualObjectPropertySchema; this port
 * exists only to keep runtime resolution independent from registry implementation details.
 */
export interface VisualRuntimePropertyRegistryPort {
  find(propertyKey: string): VisualRuntimePropertyDefinitionPort | undefined;
  validate(propertyKey: string, value: unknown): VisualRuntimePropertyValidation;
}
