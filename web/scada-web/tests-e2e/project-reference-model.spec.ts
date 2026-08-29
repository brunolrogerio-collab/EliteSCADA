import { expect, test } from '@playwright/test';
import type { EngineeringPackageView } from '../src/engineering/types';
import {
  buildProjectReferenceCatalog,
  isReferenceCompatibleWithVisualProperty,
  projectReferenceIdentity,
  resolveProjectReference
} from '../src/engineering/project-reference/projectReferenceModel';

const STATUS_TAG_ID = '11111111-2222-3333-4444-555555555555';
const CLIENT_MEMORY_ID = '66666666-7777-8888-9999-aaaaaaaaaaaa';

const packageView: EngineeringPackageView = {
  schema: 'elite-scada-engineering',
  schemaVersion: 13,
  exportedAt: '2026-08-29T00:00:00Z',
  tags: [
    { id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee', name: 'Running', path: 'Plant.P01.Running', dataType: 'Boolean', readOnly: true },
    { id: STATUS_TAG_ID, name: 'Status', path: 'Plant.P01.Status', dataType: 'Int16', readOnly: true }
  ],
  alarms: []
};

test('Client Memory keeps friendly path plus stable canonical ID in the shared project reference catalog', () => {
  const catalog = buildProjectReferenceCatalog(packageView, [{
    id: CLIENT_MEMORY_ID,
    name: 'Operator selection',
    path: 'Client.UI.OperatorSelection',
    dataType: 'Int64',
    initialValue: '9223372036854775807',
    readOnly: false
  }]);

  const memory = catalog.find(item => item.family === 'clientMemory');
  expect(memory).toMatchObject({
    reference: 'Client.UI.OperatorSelection',
    label: 'Operator selection',
    dataType: 'Int64',
    bindingKind: 'ClientMemory',
    writable: true,
    tagReference: { tagId: CLIENT_MEMORY_ID },
    selectorCapability: null
  });
  expect(memory?.pathSegments).toEqual(['Client', 'UI', 'OperatorSelection']);
  expect(projectReferenceIdentity(memory!)).toBe(`tag:${CLIENT_MEMORY_ID}`);
});

test('Client Memory canonical identity survives a friendly path rename', () => {
  const before = buildProjectReferenceCatalog(packageView, [{
    id: CLIENT_MEMORY_ID, name: 'Selection', path: 'Client.Selection', dataType: 'Boolean'
  }]).find(item => item.family === 'clientMemory')!;
  const after = buildProjectReferenceCatalog(packageView, [{
    id: CLIENT_MEMORY_ID, name: 'Selection renamed', path: 'Client.UI.SelectionRenamed', dataType: 'Boolean'
  }]).find(item => item.family === 'clientMemory')!;

  expect(before.reference).not.toBe(after.reference);
  expect(projectReferenceIdentity(before)).toBe(projectReferenceIdentity(after));
  expect(after.tagReference).toEqual({ tagId: CLIENT_MEMORY_ID });
});

test('shared reference compatibility is strict for typed properties but permits scalar dynamic text', () => {
  const catalog = buildProjectReferenceCatalog(packageView, [{
    id: CLIENT_MEMORY_ID, name: 'Counter', path: 'Client.Counter', dataType: 'Int64', initialValue: '0'
  }]);
  const boolTag = catalog.find(item => item.reference === 'Plant.P01.Running')!;
  const counter = catalog.find(item => item.reference === 'Client.Counter')!;

  expect(isReferenceCompatibleWithVisualProperty('boolean', boolTag)).toBeTruthy();
  expect(isReferenceCompatibleWithVisualProperty('number', boolTag)).toBeFalsy();
  expect(isReferenceCompatibleWithVisualProperty('number', counter)).toBeTruthy();
  expect(isReferenceCompatibleWithVisualProperty('string', counter)).toBeFalsy();
  expect(isReferenceCompatibleWithVisualProperty('string', counter, { allowScalarText: true })).toBeTruthy();
});

test('integer TAGs expose bit capability without eagerly expanding the shared catalog', () => {
  const catalog = buildProjectReferenceCatalog(packageView);
  const status = catalog.find(item => item.reference === 'Plant.P01.Status')!;

  expect(status.tagReference).toEqual({ tagId: STATUS_TAG_ID });
  expect(status.selectorCapability).toEqual({ kind: 'bit', minIndex: 0, maxIndex: 15 });
  expect(catalog.filter(item => item.reference.startsWith('Plant.P01.Status.'))).toHaveLength(0);
});

test('exact .NN authoring resolves to a stable Boolean TAG bit reference', () => {
  const catalog = buildProjectReferenceCatalog(packageView);
  const resolved = resolveProjectReference(catalog, 'Plant.P01.Status.03');

  expect(resolved.status).toBe('found');
  expect(resolved.descriptor).toMatchObject({
    reference: 'Plant.P01.Status.03',
    dataType: 'Boolean',
    engineeringUnit: null,
    tagReference: {
      tagId: STATUS_TAG_ID,
      selector: { kind: 'bit', index: 3 }
    }
  });
  expect(isReferenceCompatibleWithVisualProperty('boolean', resolved.descriptor!)).toBeTruthy();
  expect(isReferenceCompatibleWithVisualProperty('number', resolved.descriptor!)).toBeFalsy();
});

test('Int16 bit range accepts 15 and rejects 16', () => {
  const catalog = buildProjectReferenceCatalog(packageView);

  expect(resolveProjectReference(catalog, 'Plant.P01.Status.15').status).toBe('found');
  expect(resolveProjectReference(catalog, 'Plant.P01.Status.16').status).toBe('notFound');
});

test('stable TAG bit identity survives a friendly path rename', () => {
  const before = resolveProjectReference(buildProjectReferenceCatalog(packageView), 'Plant.P01.Status.03').descriptor!;
  const renamed: EngineeringPackageView = {
    ...packageView,
    tags: packageView.tags.map(tag => tag.id === STATUS_TAG_ID
      ? { ...tag, name: 'Renamed status', path: 'Plant.P01.RenamedStatus' }
      : tag)
  };
  const after = resolveProjectReference(buildProjectReferenceCatalog(renamed), 'Plant.P01.RenamedStatus.03').descriptor!;

  expect(projectReferenceIdentity(before)).toBe(projectReferenceIdentity(after));
  expect(projectReferenceIdentity(after)).toBe(`tag:${STATUS_TAG_ID}:selector:bit:3`);
});
