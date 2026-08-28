import { expect, test } from '@playwright/test';
import { dispatchClientVisualPythonCapability } from '../src/python-runtime/clientVisualPythonCapabilities';
import { createClientVisualPythonCapabilityProvider } from '../src/python-runtime/createClientVisualPythonCapabilityProvider';
import {
  RuntimeVisualInstance,
  VisualObjectPropertySchema,
  createVisualPythonPropertyCapabilityProvider,
  projectVisualEngineeringDefinition
} from '../src/visual-runtime';

function fixture() {
  const schema = new VisualObjectPropertySchema('wave07.python.clear', ['x', 'visible']);
  const definition = projectVisualEngineeringDefinition({
    objectId: 'object:python-clear',
    key: 'pythonClear',
    objectType: 'wave07.python.clear',
    baseProperties: { x: 12, visible: true }
  }, schema);
  const instance = new RuntimeVisualInstance({
    runtimeInstanceId: 'visual-python-clear-instance',
    visualContextInstanceId: 'screen:python-clear',
    definition,
    schema
  });
  const provider = createClientVisualPythonCapabilityProvider({
    visualPropertyProvider: createVisualPythonPropertyCapabilityProvider(instance)
  });
  const context = {
    scriptId: 'script:python-clear',
    runtimeInstanceId: 'script-runtime-python-clear',
    visualRuntimeInstanceId: instance.runtimeInstanceId,
    executionId: 'execution-python-clear'
  };

  return { instance, provider, context };
}

test('Client Visual Python can explicitly clear its Script override without using null as a sentinel', async () => {
  const { instance, provider, context } = fixture();

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
  expect(instance.readPropertyState('x')).toEqual({ value: 77, source: 'script' });

  const cleared = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'clear',
    { targetReference: instance.objectKey, propertyKey: 'x' },
    context
  );
  expect(cleared).toEqual({
    accepted: true,
    propertyKey: 'x',
    visualRuntimeInstanceId: instance.runtimeInstanceId
  });
  expect(instance.readPropertyState('x')).toEqual({ value: 12, source: 'engineering' });
  expect(instance.engineeringBaseSnapshot.x).toBe(12);
});

test('clear preserves the same instance, target and runtime-writable authority checks as write', async () => {
  const { instance, provider, context } = fixture();

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'clear',
    { targetReference: 'object:outside', propertyKey: 'x' },
    context
  )).rejects.toThrow(/outside the current Runtime Visual Instance context/);

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'clear',
    { targetReference: instance.objectKey, propertyKey: 'x' },
    { ...context, visualRuntimeInstanceId: 'visual:other' }
  )).rejects.toThrow(/does not own the requested Runtime Visual Instance/);

  await expect(dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'clear',
    { targetReference: instance.objectKey, propertyKey: 'unknown' },
    context
  )).rejects.toThrow(/not registered/);
});
