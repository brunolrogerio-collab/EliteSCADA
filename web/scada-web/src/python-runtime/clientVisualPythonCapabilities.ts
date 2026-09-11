import {
  CLIENT_VISUAL_PYTHON_POLICY,
  type ClientVisualPythonCapability,
  type PythonRuntimeIdentity
} from './pythonRuntimeContracts';

export type ClientVisualPythonCapabilityContext = PythonRuntimeIdentity & {
  executionId: string;
};

export type ClientVisualPythonTagWriteValue = string | number | boolean;

export interface ClientVisualPythonCapabilityProvider {
  readTag?(reference: string, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  writeTag?(reference: string, value: ClientVisualPythonTagWriteValue, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  readClientMemory?(reference: string, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  writeClientMemory?(reference: string, value: unknown, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  readVisualProperty?(targetReference: string, propertyKey: string, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  writeVisualProperty?(targetReference: string, propertyKey: string, value: unknown, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  clearVisualProperty?(targetReference: string, propertyKey: string, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  requestVisualTween?(argumentsValue: unknown, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
  requestBackendOperation?(operation: string, argumentsValue: unknown, context: ClientVisualPythonCapabilityContext): Promise<unknown> | unknown;
}

export class ClientVisualPythonCapabilityError extends Error {
  constructor(
    public readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'ClientVisualPythonCapabilityError';
  }
}

export async function dispatchClientVisualPythonCapability(
  provider: ClientVisualPythonCapabilityProvider,
  capability: ClientVisualPythonCapability,
  operation: string,
  argumentsValue: unknown,
  context: ClientVisualPythonCapabilityContext
): Promise<unknown> {
  assertBridgeValue(argumentsValue, 'arguments');

  switch (capability) {
    case 'tag.read': {
      requireOperation(operation, 'read', capability);
      const reference = requireStringArgument(argumentsValue, 'reference');
      return normalizeProviderResult(await requireProvider(provider.readTag, capability)(reference, context));
    }

    case 'tag.write': {
      requireOperation(operation, 'write', capability);
      const reference = requireStringArgument(argumentsValue, 'reference');
      const value = requireTagWriteValue(requireOwnArgument(argumentsValue, 'value'));
      return normalizeProviderResult(await requireProvider(provider.writeTag, capability)(reference, value, context));
    }

    case 'clientMemory.read': {
      requireOperation(operation, 'read', capability);
      const reference = requireStringArgument(argumentsValue, 'reference');
      return normalizeProviderResult(await requireProvider(provider.readClientMemory, capability)(reference, context));
    }

    case 'clientMemory.write': {
      requireOperation(operation, 'write', capability);
      const reference = requireStringArgument(argumentsValue, 'reference');
      const value = requireOwnArgument(argumentsValue, 'value');
      assertBridgeValue(value, 'value');
      return normalizeProviderResult(await requireProvider(provider.writeClientMemory, capability)(reference, value, context));
    }

    case 'visualProperty.read': {
      requireOperation(operation, 'read', capability);
      const targetReference = requireStringArgument(argumentsValue, 'targetReference');
      const propertyKey = requireStringArgument(argumentsValue, 'propertyKey');
      return normalizeProviderResult(await requireProvider(provider.readVisualProperty, capability)(targetReference, propertyKey, context));
    }

    case 'visualProperty.write': {
      const targetReference = requireStringArgument(argumentsValue, 'targetReference');
      const propertyKey = requireStringArgument(argumentsValue, 'propertyKey');

      if (operation === 'clear') {
        return normalizeProviderResult(await requireProvider(provider.clearVisualProperty, capability)(targetReference, propertyKey, context));
      }

      requireOperation(operation, 'write', capability);
      const value = requireOwnArgument(argumentsValue, 'value');
      assertBridgeValue(value, 'value');
      return normalizeProviderResult(await requireProvider(provider.writeVisualProperty, capability)(targetReference, propertyKey, value, context));
    }

    case 'visualTween.request': {
      requireOperation(operation, 'request', capability);
      return normalizeProviderResult(await requireProvider(provider.requestVisualTween, capability)(cloneBridgeValue(argumentsValue), context));
    }

    case 'backendOperation.request': {
      if (!operation.trim()) {
        throw new ClientVisualPythonCapabilityError(
          'PYTHON_CAPABILITY_OPERATION_REQUIRED',
          'Backend operation name is required.'
        );
      }
      return normalizeProviderResult(await requireProvider(provider.requestBackendOperation, capability)(operation, cloneBridgeValue(argumentsValue), context));
    }

    default: {
      const exhaustive: never = capability;
      throw new ClientVisualPythonCapabilityError(
        'PYTHON_CAPABILITY_DENIED',
        `Unsupported Client Visual Python capability '${String(exhaustive)}'.`
      );
    }
  }
}

function requireProvider<T extends (...args: never[]) => unknown>(
  provider: T | undefined,
  capability: ClientVisualPythonCapability
): T {
  if (!provider) {
    throw new ClientVisualPythonCapabilityError(
      'PYTHON_CAPABILITY_PROVIDER_UNAVAILABLE',
      `Capability '${capability}' is not available in this Runtime Client.`
    );
  }
  return provider;
}

function requireOperation(
  actual: string,
  expected: string,
  capability: ClientVisualPythonCapability
) {
  if (actual !== expected) {
    throw new ClientVisualPythonCapabilityError(
      'PYTHON_CAPABILITY_OPERATION_DENIED',
      `Operation '${actual}' is not valid for capability '${capability}'.`
    );
  }
}

function requireStringArgument(value: unknown, key: string): string {
  const candidate = requireOwnArgument(value, key);
  if (typeof candidate !== 'string' || !candidate.trim()) {
    throw new ClientVisualPythonCapabilityError(
      'PYTHON_CAPABILITY_ARGUMENT_INVALID',
      `Capability argument '${key}' must be a non-empty string.`
    );
  }
  return candidate;
}

function requireTagWriteValue(value: unknown): ClientVisualPythonTagWriteValue {
  if (typeof value === 'boolean' || typeof value === 'string') return value;
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  throw new ClientVisualPythonCapabilityError(
    'PYTHON_CAPABILITY_ARGUMENT_INVALID',
    'TAG write value must be a boolean, finite number, or string.'
  );
}

function requireOwnArgument(value: unknown, key: string): unknown {
  const object = requireObject(value, 'arguments');
  if (!Object.hasOwn(object, key)) {
    throw new ClientVisualPythonCapabilityError(
      'PYTHON_CAPABILITY_ARGUMENT_INVALID',
      `Capability argument '${key}' must be provided as an own property.`
    );
  }
  return object[key];
}

function requireObject(value: unknown, label: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new ClientVisualPythonCapabilityError(
      'PYTHON_CAPABILITY_ARGUMENT_INVALID',
      `${label} must be an object.`
    );
  }
  return value as Record<string, unknown>;
}

function normalizeProviderResult(value: unknown): unknown {
  if (value === undefined) return null;
  return cloneBridgeValue(value);
}

export function cloneBridgeValue(value: unknown): unknown {
  assertBridgeValue(value, 'value');
  return cloneValidatedBridgeValue(value);
}

function cloneValidatedBridgeValue(value: unknown): unknown {
  if (value === null || typeof value !== 'object') return value;
  if (Array.isArray(value)) return value.map(item => cloneValidatedBridgeValue(item));

  const clone: Record<string, unknown> = {};
  for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
    Object.defineProperty(clone, key, {
      configurable: true,
      enumerable: true,
      writable: true,
      value: cloneValidatedBridgeValue(item)
    });
  }
  return clone;
}

export function assertBridgeValue(value: unknown, label = 'value'): void {
  const seen = new Set<object>();
  const state = { nodes: 0 };
  visit(value, label, seen, state, 0);
}

function visit(
  value: unknown,
  label: string,
  seen: Set<object>,
  state: { nodes: number },
  depth: number
): void {
  state.nodes++;
  if (state.nodes > CLIENT_VISUAL_PYTHON_POLICY.maxBridgeNodes ||
      depth > CLIENT_VISUAL_PYTHON_POLICY.maxBridgeDepth) {
    throwBridgeValueInvalid(label, 'exceeds the bounded structured-value size or depth');
  }

  if (value === null) return;

  switch (typeof value) {
    case 'string':
      if (value.length <= CLIENT_VISUAL_PYTHON_POLICY.maxBridgeStringLength) return;
      break;
    case 'boolean':
      return;
    case 'number':
      if (Number.isFinite(value)) return;
      break;
    case 'object': {
      if (seen.has(value)) break;
      seen.add(value);

      if (Array.isArray(value)) {
        for (let index = 0; index < value.length; index++) {
          visit(value[index], `${label}[${index}]`, seen, state, depth + 1);
        }
        seen.delete(value);
        return;
      }

      const prototype = Object.getPrototypeOf(value);
      if (prototype !== Object.prototype && prototype !== null) break;
      for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
        visit(item, `${label}.${key}`, seen, state, depth + 1);
      }
      seen.delete(value);
      return;
    }
  }

  throwBridgeValueInvalid(label, 'is not a supported bounded structured bridge value');
}

function throwBridgeValueInvalid(label: string, detail: string): never {
  throw new ClientVisualPythonCapabilityError(
    'PYTHON_BRIDGE_VALUE_INVALID',
    `${label} ${detail}.`
  );
}
