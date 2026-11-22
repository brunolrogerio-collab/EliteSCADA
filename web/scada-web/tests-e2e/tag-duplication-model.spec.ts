import { expect, test } from '@playwright/test';
import {
  copyTagConfiguration,
  createDuplicateTagDrafts,
  createSequentialTagDrafts,
  sequentialModbusReference,
  validateGeneratedTagDrafts
} from '../src/engineering/tagDuplicationModel';
import type { TagSourceAwareEngineering } from '../src/engineering/TagSourceSelector.logic';

const source: TagSourceAwareEngineering = {
  id: '11111111-1111-4111-8111-111111111111',
  name: 'Motor Speed',
  path: 'Plant.Motor.Speed',
  dataType: 'float',
  source: 'plc-main',
  dataSourceId: '22222222-2222-4222-8222-222222222222',
  address: 'holding:10',
  engineeringUnit: 'rpm',
  description: 'Motor speed command',
  readOnly: false,
  scaleMinimum: 0,
  scaleMaximum: 1800,
  historian: { enabled: true, strategy: 'periodic', periodMilliseconds: 1000 },
  historianCaptureProfileId: '33333333-3333-4333-8333-333333333333',
  metadata: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' },
  accessPolicy: { readRoles: ['operator'], writeRoles: ['engineer'] },
  addressSelector: { kind: 'bit', index: 3 },
  communicationBinding: {
    contractVersion: 1,
    schemaId: 'modbus.tcp.engineering',
    schemaVersion: 1,
    portableAddress: 'holding:10',
    settings: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' },
    valueTransform: { contractVersion: 1, byteSwap: true, wordSwap: false }
  }
};

function ids() {
  let current = 0;
  return () => `aaaaaaaa-aaaa-4aaa-8aaa-${String(++current).padStart(12, '0')}`;
}

test('single duplicate receives a new stable ID and copies configuration only', () => {
  const runtimeDecorated = {
    ...source,
    runtimeValue: 321.5,
    quality: 'Good',
    timestampUtc: '2026-09-30T10:00:00Z',
    historianRows: [{ value: 321.5 }],
    alarmHistory: [{ state: 'active' }],
    diagnostics: { latency: 2 }
  };

  const draft = copyTagConfiguration(runtimeDecorated as TagSourceAwareEngineering, 'aaaaaaaa-aaaa-4aaa-8aaa-000000000001');

  expect(draft.id).not.toBe(source.id);
  expect(draft.source).toBe(source.source);
  expect(draft.dataSourceId).toBe(source.dataSourceId);
  expect(draft.communicationBinding).toEqual(source.communicationBinding);
  expect(draft.communicationBinding).not.toBe(source.communicationBinding);
  expect(draft.historian).toEqual(source.historian);
  expect(draft.historianCaptureProfileId).toBe(source.historianCaptureProfileId);
  expect(draft.accessPolicy).toEqual(source.accessPolicy);
  expect((draft as any).runtimeValue).toBeUndefined();
  expect((draft as any).quality).toBeUndefined();
  expect((draft as any).timestampUtc).toBeUndefined();
  expect((draft as any).historianRows).toBeUndefined();
  expect((draft as any).alarmHistory).toBeUndefined();
  expect((draft as any).diagnostics).toBeUndefined();
  expect(runtimeDecorated.id).toBe(source.id);
  expect(runtimeDecorated.path).toBe(source.path);
});

test('duplicate selected creates safe non-colliding drafts with distinct stable IDs', () => {
  const another: TagSourceAwareEngineering = {
    ...source,
    id: '44444444-4444-4444-8444-444444444444',
    name: 'Motor Current',
    path: 'Plant.Motor.Current',
    address: 'holding:20'
  };
  const generated = createDuplicateTagDrafts([source, another], [source, another], ids());

  expect(generated).toHaveLength(2);
  expect(new Set(generated.map(tag => tag.id)).size).toBe(2);
  expect(generated[0].id).not.toBe(source.id);
  expect(generated[0].path).toBe('Plant.Motor.Speed_copy');
  expect(generated[1].path).toBe('Plant.Motor.Current_copy');
  expect(generated[0].communicationBinding?.valueTransform).toEqual(source.communicationBinding?.valueTransform);
  expect(source.path).toBe('Plant.Motor.Speed');
  expect(another.path).toBe('Plant.Motor.Current');
});

test('20 sequential TAG drafts use deterministic name/path suffixes and canonical Modbus +1 references', () => {
  const generated = createSequentialTagDrafts(source, [source], {
    count: 20,
    namePattern: '{name}_{n}',
    pathPattern: '{path}_{n}',
    suffixStart: 1,
    suffixStep: 1,
    addressStep: 1
  }, ids());

  expect(generated).toHaveLength(20);
  expect(generated[0].name).toBe('Motor Speed_1');
  expect(generated[19].name).toBe('Motor Speed_20');
  expect(generated[0].path).toBe('Plant.Motor.Speed_1');
  expect(generated[19].path).toBe('Plant.Motor.Speed_20');

  const references = generated.map((_, index) => sequentialModbusReference(source.address, 1, index));
  expect(references[0]).toEqual({ area: 'holding', reference: 11 });
  expect(references[19]).toEqual({ area: 'holding', reference: 30 });
});

test('canonical Modbus sequential arithmetic supports all four frozen areas and rejects opaque addresses', () => {
  for (const area of ['coil', 'discrete', 'holding', 'input'] as const) {
    expect(sequentialModbusReference(`${area}:5`, 2, 0)).toEqual({ area, reference: 7 });
  }

  expect(() => sequentialModbusReference('40001', 1, 0)).toThrow(/canonical Modbus address/);
  expect(() => sequentialModbusReference('opc:ns=2;s=Tag', 1, 0)).toThrow(/canonical Modbus address/);
  expect(() => sequentialModbusReference('holding:65535', 1, 0)).toThrow(/outside the canonical/);
});

test('generated Preview validation catches stable ID, path, scoped name and Modbus address collisions before mutation', () => {
  const duplicate = copyTagConfiguration(source, source.id!);
  const validation = validateGeneratedTagDrafts([source], [duplicate], { checkAddressCollisions: true });

  expect(validation.canPreview).toBe(false);
  expect(validation.collisions.map(item => item.code)).toEqual(expect.arrayContaining([
    'TAG_ID_COLLISION',
    'TAG_PATH_COLLISION',
    'TAG_NAME_COLLISION',
    'TAG_ADDRESS_COLLISION'
  ]));

  const safe = createDuplicateTagDrafts([source], [source], ids());
  const safeValidation = validateGeneratedTagDrafts([source], safe, { checkAddressCollisions: false });
  expect(safeValidation).toEqual({ canPreview: true, collisions: [] });
});

test('address collision validation is scoped to the stable Data Source identity', () => {
  const otherSource = {
    ...copyTagConfiguration(source, 'bbbbbbbb-bbbb-4bbb-8bbb-000000000001'),
    dataSourceId: '55555555-5555-4555-8555-555555555555',
    source: 'plc-secondary',
    name: 'Secondary Speed',
    path: 'Secondary.Speed'
  };
  const validation = validateGeneratedTagDrafts([source], [otherSource], { checkAddressCollisions: true });
  expect(validation.canPreview).toBe(true);
});
