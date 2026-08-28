import { expect, test } from '@playwright/test';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  RuntimeVisualInstance,
  VisualObjectPropertySchema,
  VisualPropertyRegistry,
  createVisualPythonPropertyCapabilityProvider,
  projectVisualEngineeringDefinition,
  type VisualPropertyDefinition
} from '../src/visual-runtime';
import { dispatchClientVisualPythonCapability } from '../src/python-runtime/clientVisualPythonCapabilities';

const internalDebug = {
  key: 'internalDebug',
  type: 'string',
  defaultValue: 'hidden',
  engineeringEditable: true,
  runtimeReadable: false,
  runtimeWritable: false,
  supportsBinding: false,
  animatable: false
} satisfies VisualPropertyDefinition;

function fixture() {
  const registry = new VisualPropertyRegistry([
    ...COMMON_VISUAL_PROPERTY_REGISTRY.list(),
    internalDebug
  ]);
  const schema = new VisualObjectPropertySchema(
    'wave07.python.provider',
    ['x', 'visible', 'assetRef', 'internalDebug'],
    registry
  );
  const definition = projectVisualEngineeringDefinition({
    objectId: 'object:python-provider',
    key: 'pythonProvider',
    objectType: 'wave07.python.provider',
    baseProperties: {
      x: 12,
      visible: true,
      assetRef: { assetId: 'asset:python-provider' },
      internalDebug: 'private'
    }
  }, schema);
  const instance = new RuntimeVisualInstance({
    runtimeInstanceId: 'visual-python-provider-instance',
    visualContextInstanceId: 'screen:provider',
    definition,
    schema
  });
  const provider = createVisualPythonPropertyCapabilityProvider(instance);
  const context = {
    scriptId: 'script:provider',
    runtimeInstanceId: 'script-runtime-provider',
    visualRuntimeInstanceId: instance.runtimeInstanceId,
    executionId: 'execution-provider'
  };

  return { instance, provider, context };
}

test('integrated Python visual provider binds reads and writes to the current runtime instance', async () => {
  const { instance, provider, context } = fixture();

  const initial = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: instance.objectId, propertyKey: 'x' },
    context
  );
  expect(initial).toEqual({ value: 12, source: 'engineering' });

  const write = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: instance.objectKey, propertyKey: 'x', value: 77 },
    context
  );
  expect(write).toEqual({
    accepted: true,
    propertyKey: 'x',
    visualRuntimeInstanceId: instance.runtimeInstanceId
  });

  const after = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: instance.objectKey, propertyKey: 'x' },
    context
  );
  expect(after).toEqual({ value: 77, source: 'script' });
  expect(instance.engineeringBaseSnapshot.x).toBe(12);
});

test('integrated Python visual provider fails closed for policy, target, instance and lifecycle violations', async () => {
  const { instance, provider, context } = fixture();

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: instance.objectKey, propertyKey: 'internalDebug' },
    context
  )).rejects.toThrow(/not runtime-readable/);

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: instance.objectKey, propertyKey: 'assetRef', value: { assetId: 'asset:other' } },
    context
  )).rejects.toThrow(/not runtime-writable/);

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: 'object:outside', propertyKey: 'x' },
    context
  )).rejects.toThrow(/outside the current Runtime Visual Instance context/);

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: instance.objectKey, propertyKey: 'x', value: 20 },
    { ...context, visualRuntimeInstanceId: 'visual:other' }
  )).rejects.toThrow(/does not own the requested Runtime Visual Instance/);

  instance.dispose();
  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: instance.objectKey, propertyKey: 'x' },
    context
  )).rejects.toThrow(/is disposed/);
});
