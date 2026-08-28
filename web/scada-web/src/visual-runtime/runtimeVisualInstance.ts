import type { VisualObjectPropertySchema } from './visualPropertyRegistry';
import { createRuntimeVisualPropertyRegistryPort } from './runtimeVisualPropertyAdapter';
import type {
  VisualRuntimePropertyDefinitionPort,
  VisualRuntimePropertyRegistryPort,
  VisualRuntimePropertyValidation
} from './runtimeVisualPropertyPort';

export type RuntimeVisualPropertySource =
  | 'animation'
  | 'script'
  | 'binding'
  | 'engineering'
  | 'default';

export type RuntimeVisualPropertyLayer = Exclude<RuntimeVisualPropertySource, 'default'>;

export type RuntimeVisualDefinitionProjection = {
  objectId: string;
  key: string;
  objectType: string;
  parentObjectId?: string | null;
  baseProperties?: Readonly<Record<string, unknown>>;
};

export type RuntimeVisualInstanceIdentity = {
  runtimeInstanceId: string;
  objectId: string;
  objectKey: string;
  objectType: string;
  visualContextInstanceId?: string;
  parentRuntimeInstanceId?: string;
};

export type RuntimeVisualResolvedProperty = {
  propertyKey: string;
  value: unknown;
  source: RuntimeVisualPropertySource;
};

export type RuntimeVisualPropertyState = Readonly<{
  value: unknown;
  source: RuntimeVisualPropertySource;
}>;

export type RuntimeVisualPropertyFailure = {
  propertyKey: string;
  layer: RuntimeVisualPropertyLayer;
  code: string;
  reason: string;
};

export type RuntimeVisualPropertyDiagnostic = RuntimeVisualResolvedProperty & {
  runtimeInstanceId: string;
  disposed: boolean;
  validationFailures: RuntimeVisualPropertyFailure[];
};

export type RuntimeVisualInstanceOptions = {
  definition: RuntimeVisualDefinitionProjection;
  registry?: VisualRuntimePropertyRegistryPort;
  schema?: VisualObjectPropertySchema;
  runtimeInstanceId?: string;
  visualContextInstanceId?: string;
  parentRuntimeInstanceId?: string;
};

export class RuntimeVisualInstanceError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly propertyKey?: string
  ) {
    super(message);
    this.name = 'RuntimeVisualInstanceError';
  }
}

type LayerMap = Map<string, unknown>;
type FailureMap = Map<RuntimeVisualPropertyLayer, RuntimeVisualPropertyFailure>;

export class RuntimeVisualInstance {
  private readonly registry: VisualRuntimePropertyRegistryPort;
  private readonly engineeringBase = new Map<string, unknown>();
  private readonly bindingLayer: LayerMap = new Map();
  private readonly scriptLayer: LayerMap = new Map();
  private readonly animationLayer: LayerMap = new Map();
  private readonly failures = new Map<string, FailureMap>();
  private readonly disposers = new Set<() => void>();
  private disposed = false;

  readonly identity: RuntimeVisualInstanceIdentity;
  readonly sourceParentObjectId?: string;

  constructor(options: RuntimeVisualInstanceOptions) {
    const { definition } = options;
    const registry = resolveRuntimeRegistry(options);
    requireIdentityPart(definition.objectId, 'objectId');
    requireIdentityPart(definition.key, 'key');
    requireIdentityPart(definition.objectType, 'objectType');

    this.registry = registry;
    this.identity = Object.freeze({
      runtimeInstanceId: options.runtimeInstanceId ?? createRuntimeInstanceId(),
      objectId: definition.objectId,
      objectKey: definition.key,
      objectType: definition.objectType,
      visualContextInstanceId: normalizeOptionalIdentity(options.visualContextInstanceId),
      parentRuntimeInstanceId: normalizeOptionalIdentity(options.parentRuntimeInstanceId)
    });
    requireIdentityPart(this.identity.runtimeInstanceId, 'runtimeInstanceId');
    this.sourceParentObjectId = normalizeOptionalIdentity(definition.parentObjectId);

    for (const [propertyKey, value] of Object.entries(definition.baseProperties ?? {})) {
      const property = this.requireRegistered(propertyKey);
      const validation = this.registry.validate(property.key, value);
      if (!validation.valid) {
        this.recordFailure(property.key, 'engineering', validation);
        continue;
      }
      this.engineeringBase.set(property.key, cloneRuntimeValue(value));
    }
  }

  get runtimeInstanceId(): string {
    return this.identity.runtimeInstanceId;
  }

  get objectId(): string {
    return this.identity.objectId;
  }

  get objectKey(): string {
    return this.identity.objectKey;
  }

  get objectType(): string {
    return this.identity.objectType;
  }

  get isDisposed(): boolean {
    return this.disposed;
  }

  get engineeringBaseSnapshot(): Readonly<Record<string, unknown>> {
    const snapshot: Record<string, unknown> = {};
    for (const [propertyKey, value] of this.engineeringBase) {
      snapshot[propertyKey] = cloneRuntimeValue(value);
    }
    return Object.freeze(snapshot);
  }

  readEffective(propertyKey: string): RuntimeVisualResolvedProperty {
    const property = this.requireRegistered(propertyKey);
    return this.resolve(property);
  }

  readPropertyState(propertyKey: string): RuntimeVisualPropertyState {
    const resolved = this.readEffective(propertyKey);
    return Object.freeze({
      value: resolved.value,
      source: resolved.source
    });
  }

  readRuntimeReadable(propertyKey: string): RuntimeVisualResolvedProperty {
    const property = this.requireRegistered(propertyKey);
    if (!property.runtimeReadable) {
      throw new RuntimeVisualInstanceError(
        'VISUAL_PROPERTY_NOT_RUNTIME_READABLE',
        `Visual property '${property.key}' is not runtime-readable.`,
        property.key
      );
    }
    return this.resolve(property);
  }

  getPropertyDiagnostic(propertyKey: string): RuntimeVisualPropertyDiagnostic {
    const resolved = this.readEffective(propertyKey);
    const failures = [...(this.failures.get(resolved.propertyKey)?.values() ?? [])]
      .map(item => ({ ...item }));

    return {
      ...resolved,
      runtimeInstanceId: this.identity.runtimeInstanceId,
      disposed: this.disposed,
      validationFailures: failures
    };
  }

  setBindingValue(propertyKey: string, value: unknown): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertBindingSupported(property);
    this.setValidatedLayer(property, 'binding', value, this.bindingLayer);
  }

  clearBindingValue(propertyKey: string): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertBindingSupported(property);
    this.bindingLayer.delete(property.key);
    this.clearFailure(property.key, 'binding');
  }

  setScriptOverride(propertyKey: string, value: unknown): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertRuntimeWritable(property);
    this.setValidatedLayer(property, 'script', value, this.scriptLayer);
  }

  clearScriptOverride(propertyKey: string): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertRuntimeWritable(property);
    this.scriptLayer.delete(property.key);
    this.clearFailure(property.key, 'script');
  }

  setAnimationOverride(propertyKey: string, value: unknown): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertAnimatable(property);
    this.setValidatedLayer(property, 'animation', value, this.animationLayer);
  }

  clearAnimationOverride(propertyKey: string): void {
    this.assertWritableLifecycle();
    const property = this.requireRegistered(propertyKey);
    this.assertAnimatable(property);
    this.animationLayer.delete(property.key);
    this.clearFailure(property.key, 'animation');
  }

  registerDisposer(disposer: () => void): () => void {
    this.assertWritableLifecycle();
    this.disposers.add(disposer);
    return () => {
      this.disposers.delete(disposer);
    };
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;

    const callbacks = [...this.disposers];
    this.disposers.clear();
    this.bindingLayer.clear();
    this.scriptLayer.clear();
    this.animationLayer.clear();

    for (const propertyFailures of this.failures.values()) {
      propertyFailures.delete('binding');
      propertyFailures.delete('script');
      propertyFailures.delete('animation');
    }

    for (const callback of callbacks) {
      try {
        callback();
      } catch {
        // Cleanup failure cannot resurrect writable runtime state or block disposal.
      }
    }
  }

  private resolve(property: VisualRuntimePropertyDefinitionPort): RuntimeVisualResolvedProperty {
    const layeredValue = firstPresent(
      property.key,
      ['animation', this.animationLayer] as const,
      ['script', this.scriptLayer] as const,
      ['binding', this.bindingLayer] as const,
      ['engineering', this.engineeringBase] as const
    );

    if (layeredValue) {
      return {
        propertyKey: property.key,
        value: cloneRuntimeValue(layeredValue.value),
        source: layeredValue.source
      };
    }

    const defaultValidation = this.registry.validate(property.key, property.defaultValue);
    if (!defaultValidation.valid) {
      throw new RuntimeVisualInstanceError(
        'VISUAL_PROPERTY_DEFAULT_INVALID',
        `Registered default for visual property '${property.key}' is invalid.`,
        property.key
      );
    }

    return {
      propertyKey: property.key,
      value: cloneRuntimeValue(property.defaultValue),
      source: 'default'
    };
  }

  private setValidatedLayer(
    property: VisualRuntimePropertyDefinitionPort,
    layer: Exclude<RuntimeVisualPropertyLayer, 'engineering'>,
    value: unknown,
    target: LayerMap
  ): void {
    const validation = this.registry.validate(property.key, value);
    if (!validation.valid) {
      this.recordFailure(property.key, layer, validation);
      throw new RuntimeVisualInstanceError(
        validation.code || 'VISUAL_PROPERTY_VALUE_INVALID',
        validation.reason || `Invalid value for visual property '${property.key}'.`,
        property.key
      );
    }

    target.set(property.key, cloneRuntimeValue(value));
    this.clearFailure(property.key, layer);
  }

  private recordFailure(
    propertyKey: string,
    layer: RuntimeVisualPropertyLayer,
    validation: Extract<VisualRuntimePropertyValidation, { valid: false }>
  ): void {
    let propertyFailures = this.failures.get(propertyKey);
    if (!propertyFailures) {
      propertyFailures = new Map();
      this.failures.set(propertyKey, propertyFailures);
    }
    propertyFailures.set(layer, {
      propertyKey,
      layer,
      code: validation.code || 'VISUAL_PROPERTY_VALUE_INVALID',
      reason: validation.reason || 'Visual property value is invalid.'
    });
  }

  private clearFailure(propertyKey: string, layer: RuntimeVisualPropertyLayer): void {
    const propertyFailures = this.failures.get(propertyKey);
    if (!propertyFailures) return;
    propertyFailures.delete(layer);
    if (propertyFailures.size === 0) this.failures.delete(propertyKey);
  }

  private requireRegistered(propertyKey: string): VisualRuntimePropertyDefinitionPort {
    const normalized = propertyKey.trim();
    if (!normalized) {
      throw new RuntimeVisualInstanceError(
        'VISUAL_PROPERTY_KEY_REQUIRED',
        'Visual property key is required.'
      );
    }

    const property = this.registry.find(normalized);
    if (!property) {
      throw new RuntimeVisualInstanceError(
        'VISUAL_PROPERTY_NOT_REGISTERED',
        `Visual property '${normalized}' is not registered.`,
        normalized
      );
    }
    return property;
  }

  private assertBindingSupported(property: VisualRuntimePropertyDefinitionPort): void {
    if (property.supportsBinding) return;
    throw new RuntimeVisualInstanceError(
      'VISUAL_PROPERTY_BINDING_NOT_SUPPORTED',
      `Visual property '${property.key}' does not support binding values.`,
      property.key
    );
  }

  private assertRuntimeWritable(property: VisualRuntimePropertyDefinitionPort): void {
    if (property.runtimeWritable) return;
    throw new RuntimeVisualInstanceError(
      'VISUAL_PROPERTY_NOT_RUNTIME_WRITABLE',
      `Visual property '${property.key}' is not runtime-writable.`,
      property.key
    );
  }

  private assertAnimatable(property: VisualRuntimePropertyDefinitionPort): void {
    if (property.animatable) return;
    throw new RuntimeVisualInstanceError(
      'VISUAL_PROPERTY_NOT_ANIMATABLE',
      `Visual property '${property.key}' is not animatable.`,
      property.key
    );
  }

  private assertWritableLifecycle(): void {
    if (!this.disposed) return;
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_INSTANCE_DISPOSED',
      `Runtime visual instance '${this.identity.runtimeInstanceId}' is disposed.`
    );
  }
}

function resolveRuntimeRegistry(options: RuntimeVisualInstanceOptions): VisualRuntimePropertyRegistryPort {
  if (options.registry && options.schema) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_REGISTRY_AMBIGUOUS',
      'Runtime Visual Instance accepts either a registry port or a Visual Object schema, not both.'
    );
  }
  if (options.registry) return options.registry;
  if (options.schema) return createRuntimeVisualPropertyRegistryPort(options.schema);
  throw new RuntimeVisualInstanceError(
    'VISUAL_RUNTIME_REGISTRY_REQUIRED',
    'Runtime Visual Instance requires a visual property registry or schema.'
  );
}

function firstPresent(
  propertyKey: string,
  ...layers: readonly (readonly [RuntimeVisualPropertyLayer, ReadonlyMap<string, unknown>])[]
): { source: RuntimeVisualPropertyLayer; value: unknown } | undefined {
  for (const [source, values] of layers) {
    if (values.has(propertyKey)) {
      return { source, value: values.get(propertyKey) };
    }
  }
  return undefined;
}

function createRuntimeInstanceId(): string {
  const randomUuid = globalThis.crypto?.randomUUID;
  if (typeof randomUuid !== 'function') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_INSTANCE_ID_FACTORY_UNAVAILABLE',
      'Browser UUID generation is required when no Runtime Visual Instance ID is supplied.'
    );
  }
  return randomUuid.call(globalThis.crypto);
}

function requireIdentityPart(value: string, field: string): void {
  if (!value.trim()) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_IDENTITY_REQUIRED',
      `Runtime visual identity field '${field}' is required.`
    );
  }
}

function normalizeOptionalIdentity(value: string | null | undefined): string | undefined {
  if (value === undefined || value === null) return undefined;
  const normalized = value.trim();
  return normalized || undefined;
}

function cloneRuntimeValue<T>(value: T): T {
  if (value === null || value === undefined || typeof value !== 'object') return value;
  return structuredClone(value);
}
