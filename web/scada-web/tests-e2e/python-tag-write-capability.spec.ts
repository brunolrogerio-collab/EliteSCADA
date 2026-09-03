import { expect, test } from '@playwright/test';
import {
  ClientVisualPythonCapabilityError,
  dispatchClientVisualPythonCapability,
  type ClientVisualPythonCapabilityContext
} from '../src/python-runtime/clientVisualPythonCapabilities';
import { createClientVisualPythonCapabilityProvider } from '../src/python-runtime/createClientVisualPythonCapabilityProvider';
import { CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES } from '../src/python-runtime/pythonRuntimeContracts';

const context: ClientVisualPythonCapabilityContext = {
  scriptId: 'script:tag-write',
  runtimeInstanceId: 'script-runtime:tag-write',
  executionId: 'execution:tag-write'
};

test('TAG write capability dispatches only through the trusted provider with stable identity and scalar value', async () => {
  const calls: Array<{ reference: string; value: unknown }> = [];
  const provider = {
    writeTag(reference: string, value: string | number | boolean) {
      calls.push({ reference, value });
      return { accepted: true, reference };
    }
  };

  const result = await dispatchClientVisualPythonCapability(
    provider,
    'tag.write',
    'write',
    { reference: '11111111-1111-1111-1111-111111111111', value: 42.5 },
    context
  );

  expect(calls).toEqual([{ reference: '11111111-1111-1111-1111-111111111111', value: 42.5 }]);
  expect(result).toEqual({ accepted: true, reference: '11111111-1111-1111-1111-111111111111' });
});

test('TAG write capability fails closed for missing provider, wrong operation and structured values', async () => {
  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      {},
      'tag.write',
      'write',
      { reference: '11111111-1111-1111-1111-111111111111', value: true },
      context
    ),
    'PYTHON_CAPABILITY_PROVIDER_UNAVAILABLE'
  );

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      { writeTag: () => null },
      'tag.write',
      'read',
      { reference: '11111111-1111-1111-1111-111111111111', value: true },
      context
    ),
    'PYTHON_CAPABILITY_OPERATION_DENIED'
  );

  for (const value of [null, { nested: true }, [1, 2, 3], Number.POSITIVE_INFINITY]) {
    await expectCapabilityCode(
      dispatchClientVisualPythonCapability(
        { writeTag: () => null },
        'tag.write',
        'write',
        { reference: '11111111-1111-1111-1111-111111111111', value },
        context
      ),
      value === Number.POSITIVE_INFINITY ? 'PYTHON_BRIDGE_VALUE_INVALID' : 'PYTHON_CAPABILITY_ARGUMENT_INVALID'
    );
  }
});

test('official provider routes TAG writes to the injected mediated Runtime writer', async () => {
  const calls: Array<{ reference: string; value: unknown }> = [];
  const provider = createClientVisualPythonCapabilityProvider({
    tagReader: async () => {
      throw new Error('TAG read is not expected in this test.');
    },
    tagWriter: async (reference, value) => {
      calls.push({ reference, value });
    }
  });

  const result = await dispatchClientVisualPythonCapability(
    provider,
    'tag.write',
    'write',
    { reference: '22222222-2222-2222-2222-222222222222', value: 'Auto' },
    context
  );

  expect(calls).toEqual([{ reference: '22222222-2222-2222-2222-222222222222', value: 'Auto' }]);
  expect(result).toEqual({ accepted: true, reference: '22222222-2222-2222-2222-222222222222' });
});

test('Engineering preview can explicitly remove process TAG-write authority while preserving the same sandbox bridge contract', async () => {
  const previewProvider = createClientVisualPythonCapabilityProvider({ tagWriter: null });
  expect(previewProvider.writeTag).toBeUndefined();

  await expectCapabilityCode(
    dispatchClientVisualPythonCapability(
      previewProvider,
      'tag.write',
      'write',
      { reference: '33333333-3333-3333-3333-333333333333', value: true },
      context
    ),
    'PYTHON_CAPABILITY_PROVIDER_UNAVAILABLE'
  );
});

test('direct shared TAG mutation and Driver authority remain explicitly denied boundaries', () => {
  expect(CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES).toContain('shared-tag-write-direct');
  expect(CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES).toContain('industrial-driver');
  expect(CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES).toContain('arbitrary-network');
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
