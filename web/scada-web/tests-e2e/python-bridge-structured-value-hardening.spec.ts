import { expect, test } from '@playwright/test';
import {
  ClientVisualPythonCapabilityError,
  cloneBridgeValue,
  dispatchClientVisualPythonCapability
} from '../src/python-runtime/clientVisualPythonCapabilities';
import { CLIENT_VISUAL_PYTHON_POLICY } from '../src/python-runtime/pythonRuntimeContracts';

const context = {
  scriptId: 'script:bridge-hardening',
  runtimeInstanceId: 'script-runtime-hardening',
  visualRuntimeInstanceId: 'visual-runtime-hardening',
  executionId: 'execution-hardening'
};

test('structured bridge cloning preserves __proto__ only as inert own data and does not mutate object prototypes', () => {
  const input: Record<string, unknown> = { safe: 1 };
  Object.defineProperty(input, '__proto__', {
    configurable: true,
    enumerable: true,
    writable: true,
    value: { polluted: true }
  });

  const clone = cloneBridgeValue(input) as Record<string, unknown>;

  expect(Object.getPrototypeOf(clone)).toBe(Object.prototype);
  expect(Object.hasOwn(clone, '__proto__')).toBe(true);
  expect(clone.__proto__).toEqual({ polluted: true });
  expect(({} as { polluted?: boolean }).polluted).toBeUndefined();
});

test('structured bridge values are bounded by depth, node count and string length', () => {
  const root: Record<string, unknown> = {};
  let cursor = root;
  for (let index = 0; index <= CLIENT_VISUAL_PYTHON_POLICY.maxBridgeDepth; index++) {
    const next: Record<string, unknown> = {};
    cursor.next = next;
    cursor = next;
  }
  expect(() => cloneBridgeValue(root)).toThrow(/bounded structured-value size or depth/);

  const tooManyNodes = Array.from(
    { length: CLIENT_VISUAL_PYTHON_POLICY.maxBridgeNodes + 1 },
    () => 1
  );
  expect(() => cloneBridgeValue(tooManyNodes)).toThrow(/bounded structured-value size or depth/);

  const tooLong = 'x'.repeat(CLIENT_VISUAL_PYTHON_POLICY.maxBridgeStringLength + 1);
  expect(() => cloneBridgeValue({ text: tooLong })).toThrow(/supported bounded structured bridge value/);
});

test('capability arguments cannot be satisfied by inherited properties', async () => {
  const previousDescriptor = Object.getOwnPropertyDescriptor(Object.prototype, 'targetReference');
  Object.defineProperty(Object.prototype, 'targetReference', {
    configurable: true,
    enumerable: false,
    writable: true,
    value: 'inherited-target'
  });

  try {
    const request = dispatchClientVisualPythonCapability(
      { readVisualProperty: () => 1 },
      'visualProperty.read',
      'read',
      { propertyKey: 'x' },
      context
    );

    await expect(request).rejects.toMatchObject({
      name: 'ClientVisualPythonCapabilityError',
      code: 'PYTHON_CAPABILITY_ARGUMENT_INVALID'
    } satisfies Partial<ClientVisualPythonCapabilityError>);
  } finally {
    if (previousDescriptor) {
      Object.defineProperty(Object.prototype, 'targetReference', previousDescriptor);
    } else {
      delete (Object.prototype as { targetReference?: unknown }).targetReference;
    }
  }
});
