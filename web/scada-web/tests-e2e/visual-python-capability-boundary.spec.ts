import { expect, test } from '@playwright/test';
import {
  ClientVisualPythonCapabilityError,
  dispatchClientVisualPythonCapability,
  type ClientVisualPythonCapabilityContext,
  type ClientVisualPythonCapabilityProvider
} from '../src/python-runtime/clientVisualPythonCapabilities';

const contextA: ClientVisualPythonCapabilityContext = {
  scriptId: '11111111-1111-1111-1111-111111111111',
  runtimeInstanceId: 'script-runtime-a',
  visualRuntimeInstanceId: 'visual-runtime-a',
  executionId: 'execution-a'
};

const contextB: ClientVisualPythonCapabilityContext = {
  ...contextA,
  visualRuntimeInstanceId: 'visual-runtime-b',
  executionId: 'execution-b'
};

test('visual property bridge preserves visual runtime identity and structured values', async () => {
  const calls: Array<Record<string, unknown>> = [];
  const provider: ClientVisualPythonCapabilityProvider = {
    readVisualProperty(targetReference, propertyKey, context) {
      calls.push({ kind: 'read', targetReference, propertyKey, context: { ...context } });
      return {
        propertyKey,
        value: 25,
        source: 'script',
        targetReference
      };
    },
    writeVisualProperty(targetReference, propertyKey, value, context) {
      calls.push({
        kind: 'write',
        targetReference,
        propertyKey,
        value,
        context: { ...context }
      });
      return {
        accepted: true,
        targetReference,
        propertyKey,
        value,
        visualRuntimeInstanceId: context.visualRuntimeInstanceId
      };
    }
  };

  const read = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.read',
    'read',
    { targetReference: 'pump-01', propertyKey: 'x' },
    contextA
  );
  const writeInput = { nested: ['safe', 42, true] };
  const write = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: 'pump-01', propertyKey: 'text', value: writeInput },
    contextB
  );

  expect(read).toEqual({
    propertyKey: 'x',
    value: 25,
    source: 'script',
    targetReference: 'pump-01'
  });
  expect(write).toEqual({
    accepted: true,
    targetReference: 'pump-01',
    propertyKey: 'text',
    value: writeInput,
    visualRuntimeInstanceId: 'visual-runtime-b'
  });

  expect(calls).toEqual([
    {
      kind: 'read',
      targetReference: 'pump-01',
      propertyKey: 'x',
      context: contextA
    },
    {
      kind: 'write',
      targetReference: 'pump-01',
      propertyKey: 'text',
      value: writeInput,
      context: contextB
    }
  ]);

  expect(write).not.toBe(writeInput);
});

test('visual property bridge fails closed for missing provider, wrong operation and malformed references', async () => {
  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      {},
      'visualProperty.read',
      'read',
      { targetReference: 'pump-01', propertyKey: 'x' },
      contextA
    ),
    'PYTHON_CAPABILITY_PROVIDER_UNAVAILABLE'
  );

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      { readVisualProperty: () => 1 },
      'visualProperty.read',
      'write',
      { targetReference: 'pump-01', propertyKey: 'x' },
      contextA
    ),
    'PYTHON_CAPABILITY_OPERATION_DENIED'
  );

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      { writeVisualProperty: () => null },
      'visualProperty.write',
      'read',
      { targetReference: 'pump-01', propertyKey: 'x', value: 1 },
      contextA
    ),
    'PYTHON_CAPABILITY_OPERATION_DENIED'
  );

  for (const argumentsValue of [
    { targetReference: '', propertyKey: 'x' },
    { targetReference: 'pump-01', propertyKey: '' },
    { targetReference: 42, propertyKey: 'x' },
    { targetReference: 'pump-01', propertyKey: null }
  ]) {
    await expectCapabilityCode(
      dispatchClientVisualPythonCapability(
        { readVisualProperty: () => 1 },
        'visualProperty.read',
        'read',
        argumentsValue,
        contextA
      ),
      'PYTHON_CAPABILITY_ARGUMENT_INVALID'
    );
  }
});

test('visual property bridge rejects renderer-private handles and non structured-clone authority', async () => {
  class RendererPrivateHandle {
    constructor(readonly nodeId: string) {}
  }

  let providerInvoked = false;
  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      {
        writeVisualProperty() {
          providerInvoked = true;
          return null;
        }
      },
      'visualProperty.write',
      'write',
      {
        targetReference: 'pump-01',
        propertyKey: 'text',
        value: new RendererPrivateHandle('dom-node-1')
      },
      contextA
    ),
    'PYTHON_BRIDGE_VALUE_INVALID'
  );
  expect(providerInvoked).toBe(false);

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      {
        readVisualProperty() {
          return new RendererPrivateHandle('dom-node-2');
        }
      },
      'visualProperty.read',
      'read',
      { targetReference: 'pump-01', propertyKey: 'x' },
      contextA
    ),
    'PYTHON_BRIDGE_VALUE_INVALID'
  );

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      {
        readVisualProperty() {
          return { value: 1, mutateDom: () => document.body.remove() };
        }
      },
      'visualProperty.read',
      'read',
      { targetReference: 'pump-01', propertyKey: 'x' },
      contextA
    ),
    'PYTHON_BRIDGE_VALUE_INVALID'
  );
});

test('visual instance identity remains explicit across otherwise identical script executions', async () => {
  const seen: string[] = [];
  const provider: ClientVisualPythonCapabilityProvider = {
    writeVisualProperty(_targetReference, _propertyKey, _value, context) {
      seen.push(`${context.runtimeInstanceId}:${context.visualRuntimeInstanceId}:${context.executionId}`);
      return context.visualRuntimeInstanceId ?? null;
    }
  };

  const first = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: 'pump-01', propertyKey: 'visible', value: false },
    contextA
  );
  const second = await dispatchClientVisualPythonCapability(
    provider,
    'visualProperty.write',
    'write',
    { targetReference: 'pump-01', propertyKey: 'visible', value: true },
    contextB
  );

  expect(first).toBe('visual-runtime-a');
  expect(second).toBe('visual-runtime-b');
  expect(seen).toEqual([
    'script-runtime-a:visual-runtime-a:execution-a',
    'script-runtime-a:visual-runtime-b:execution-b'
  ]);
});

async function expectCapabilityCode(promise: Promise<unknown>, expectedCode: string) {
  try {
    await promise;
    throw new Error(`Expected capability failure '${expectedCode}'.`);
  } catch (error) {
    expect(error).toBeInstanceOf(ClientVisualPythonCapabilityError);
    expect((error as ClientVisualPythonCapabilityError).code).toBe(expectedCode);
  }
}
