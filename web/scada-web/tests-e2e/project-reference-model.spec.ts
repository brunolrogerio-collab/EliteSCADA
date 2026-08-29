import { expect, test } from '@playwright/test';
import type { EngineeringPackageView } from '../src/engineering/types';
import {
  buildProjectReferenceCatalog,
  isReferenceCompatibleWithVisualProperty
} from '../src/engineering/project-reference/projectReferenceModel';

const packageView: EngineeringPackageView = {
  schema: 'elite-scada-engineering',
  schemaVersion: 13,
  exportedAt: '2026-08-29T00:00:00Z',
  tags: [{ name: 'Running', path: 'Plant.P01.Running', dataType: 'Boolean', readOnly: true }],
  alarms: []
};

test('Client Memory keeps canonical path separate from display name in the shared project reference catalog', () => {
  const catalog = buildProjectReferenceCatalog(packageView, [{
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
    writable: true
  });
  expect(memory?.pathSegments).toEqual(['Client', 'UI', 'OperatorSelection']);
});

test('shared reference compatibility is strict for typed properties but permits scalar dynamic text', () => {
  const catalog = buildProjectReferenceCatalog(packageView, [{
    name: 'Counter', path: 'Client.Counter', dataType: 'Int64', initialValue: '0'
  }]);
  const boolTag = catalog.find(item => item.reference === 'Plant.P01.Running')!;
  const counter = catalog.find(item => item.reference === 'Client.Counter')!;

  expect(isReferenceCompatibleWithVisualProperty('boolean', boolTag)).toBeTruthy();
  expect(isReferenceCompatibleWithVisualProperty('number', boolTag)).toBeFalsy();
  expect(isReferenceCompatibleWithVisualProperty('number', counter)).toBeTruthy();
  expect(isReferenceCompatibleWithVisualProperty('string', counter)).toBeFalsy();
  expect(isReferenceCompatibleWithVisualProperty('string', counter, { allowScalarText: true })).toBeTruthy();
});
