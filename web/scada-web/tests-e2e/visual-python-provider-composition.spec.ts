import { expect, test } from '@playwright/test';
import { dispatchClientVisualPythonCapability } from '../src/python-runtime/clientVisualPythonCapabilities';
import { createClientVisualPythonCapabilityProvider } from '../src/python-runtime/createClientVisualPythonCapabilityProvider';

const context = {
  scriptId: 'script:composition',
  runtimeInstanceId: 'script-runtime-composition',
  visualRuntimeInstanceId: 'visual-runtime-composition',
  executionId: 'execution-composition'
};

test('official Client Visual Python provider composes visual property authority without losing provider method context', async () => {
  const visualPropertyProvider = {
    marker: 'visual-boundary',
    readVisualProperty(this: { marker: string }, targetReference: string, propertyKey: string) {
      return { marker: this.marker, targetReference, propertyKey };
    },
    writeVisualProperty(this: { marker: string }, targetReference: string, propertyKey: string, value: unknown) {
      return { marker: this.marker, targetReference, propertyKey, value };
    }
  };

  const provider = createClientVisualPythonCapabilityProvider({ visualPropertyProvider });

  const read = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: 'object:1', propertyKey: 'x' },
    context
  );
  expect(read).toEqual({
    marker: 'visual-boundary',
    targetReference: 'object:1',
    propertyKey: 'x'
  });

  const write = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: 'object:1', propertyKey: 'x', value: 42 },
    context
  );
  expect(write).toEqual({
    marker: 'visual-boundary',
    targetReference: 'object:1',
    propertyKey: 'x',
    value: 42
  });
});
