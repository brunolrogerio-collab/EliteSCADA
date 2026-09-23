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
    tagReader: async reference => ({
      tag: { id: reference, name: 'Auto', path: 'Plant.Auto', dataType: 'String', readOnly: false },
      current: null
    }),
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

test('RED-2: Client Visual readable TAG write resolves the visible path then writes by the returned stable ID', async () => {
  const calls: Array<{ reference: string; value: unknown }> = [];
  const stableId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const provider = createClientVisualPythonCapabilityProvider({
    tagReader: async () => ({
      tag: { id: stableId, name: 'LevelPct', path: 'Plant.Process.LevelPct', dataType: 'Double', readOnly: false },
      current: null
    }),
    tagWriter: async (reference, value) => { calls.push({ reference, value }); }
  });

  await provider.readTag('Plant.Process.LevelPct');
  await provider.writeTag!('Plant.Process.LevelPct', 42);

  expect(calls).toEqual([{ reference: stableId, value: 42 }]);
});

test('review RED: a declared readable TAG write proves current identity before it writes', async () => {
  const expectedTagId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const visibleReference = 'Plant.Process.LevelPct';
  const reads: string[] = [];
  const writes: Array<{ reference: string; value: unknown }> = [];
  const provider = createClientVisualPythonCapabilityProvider({
    tagDependencies: [readableTagDependency(visibleReference, expectedTagId)],
    tagReader: async reference => {
      reads.push(reference);
      return runtimeTagDetail(expectedTagId, visibleReference);
    },
    tagWriter: async (reference, value) => { writes.push({ reference, value }); }
  });

  await provider.writeTag!(visibleReference, 42);

  expect(reads).toEqual([visibleReference]);
  expect(writes).toEqual([{ reference: expectedTagId, value: 42 }]);
});

test('review RED: a path reused by another TAG fails closed before Client Visual can write', async () => {
  const expectedTagId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const replacementTagId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  const visibleReference = 'Plant.Process.LevelPct';
  const writes: Array<{ reference: string; value: unknown }> = [];
  const provider = createClientVisualPythonCapabilityProvider({
    tagDependencies: [readableTagDependency(visibleReference, expectedTagId)],
    tagReader: async () => runtimeTagDetail(replacementTagId, visibleReference),
    tagWriter: async (reference, value) => { writes.push({ reference, value }); }
  });

  await expect(provider.writeTag!(visibleReference, 42)).rejects.toThrow('declared stable identity');
  expect(writes).toEqual([]);
});

test('Client Visual denies an undeclared readable TAG reference before it can read or write', async () => {
  const expectedTagId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const reads: string[] = [];
  const writes: string[] = [];
  const provider = createClientVisualPythonCapabilityProvider({
    tagDependencies: [readableTagDependency('Plant.Process.Declared', expectedTagId)],
    tagReader: async reference => {
      reads.push(reference);
      return runtimeTagDetail(expectedTagId, reference);
    },
    tagWriter: async reference => { writes.push(reference); }
  });

  await expect(provider.readTag('Plant.Process.Undeclared')).rejects.toThrow('not declared');
  await expect(provider.writeTag!('Plant.Process.Undeclared', 42)).rejects.toThrow('not declared');
  expect(reads).toEqual([]);
  expect(writes).toEqual([]);
});

test('Client Visual accepts case-equivalent readable paths but still writes only the persisted stable TAG ID', async () => {
  const expectedTagId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const persistedReference = 'Plant.Área.Nível';
  const sourceReference = 'plant.área.nível';
  const reads: string[] = [];
  const writes: Array<{ reference: string; value: unknown }> = [];
  const provider = createClientVisualPythonCapabilityProvider({
    tagDependencies: [readableTagDependency(persistedReference, expectedTagId)],
    tagReader: async reference => {
      reads.push(reference);
      return runtimeTagDetail(expectedTagId, persistedReference);
    },
    tagWriter: async (reference, value) => { writes.push({ reference, value }); }
  });

  await provider.readTag(sourceReference);
  await provider.writeTag!(sourceReference, 42);

  expect(reads).toEqual([persistedReference, persistedReference]);
  expect(writes).toEqual([{ reference: expectedTagId, value: 42 }]);
});

test('Client Visual readable-reference lookup matches the canonical ordinal case matrix', async () => {
  const expectedTagId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const cases = [
    ['Plant.K', 'plant.k', true],
    ['Plant.Área.Nível', 'plant.área.nível', true],
    ['Plant.K', 'Plant.k', false],
    ['Plant.ſ', 'Plant.s', false],
    ['Plant.I', 'Plant.i', true],
    ['Plant.İ', 'Plant.i', false],
    ['Plant.I', 'Plant.ı', false],
    ['Plant.Σ', 'Plant.σ', true],
    ['Plant.Σ', 'Plant.ς', true],
    ['Plant.É', 'Plant.E\u0301', false],
    ['Plant.A', 'Plant.B', false]
  ] as const;

  for (const [declaredReference, sourceReference, equivalent] of cases) {
    const reads: string[] = [];
    const provider = createClientVisualPythonCapabilityProvider({
      tagDependencies: [readableTagDependency(declaredReference, expectedTagId)],
      tagReader: async reference => {
        reads.push(reference);
        return runtimeTagDetail(expectedTagId, declaredReference);
      }
    });

    if (equivalent) {
      await expect(provider.readTag(sourceReference)).resolves.toMatchObject({ id: expectedTagId });
      expect(reads).toEqual([declaredReference]);
    } else {
      await expect(provider.readTag(sourceReference)).rejects.toThrow('not declared');
      expect(reads).toEqual([]);
    }
  }
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

function readableTagDependency(reference: string, tagId: string) {
  return {
    kind: 'tag' as const,
    stableReference: tagId,
    tagBinding: {
      version: 1,
      reference,
      expected: { tagId }
    }
  };
}

function runtimeTagDetail(id: string, path: string) {
  return {
    tag: { id, name: 'LevelPct', path, dataType: 'Double', readOnly: false },
    current: null
  };
}
