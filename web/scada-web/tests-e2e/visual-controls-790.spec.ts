import { expect, test } from '@playwright/test';
import {
  comboBoxOptionsEngineeringValue,
  normalizeComboBoxOptions,
  readCheckBoxAlarmSuppressionConfig,
  resolveComboBoxSelection,
  suppressionExpiry
} from '../src/engineering/visual-editor/controlVisualModel';
import { createBindingSetIntent, isBindingSourceCompatibleForObject } from '../src/engineering/visual-editor/binding-editor/bindingEditorModel';
import type { VisualEditorBindingSourceCatalogItem } from '../src/engineering/visual-editor/visualEditorContracts';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

const booleanTag: VisualEditorBindingSourceCatalogItem = {
  kind: 'Tag', target: 'Plant.Pump01.Enabled', label: 'Bomba 01 · Habilitada',
  dataType: 'Boolean', writable: true, tagReference: { tagId: 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e' }
};
const booleanExpression: VisualEditorBindingSourceCatalogItem = {
  kind: 'Expression', target: 'expression:pump-is-available', label: 'Bomba disponível',
  dataType: 'Boolean', writable: true
};
const booleanClientMemory: VisualEditorBindingSourceCatalogItem = {
  kind: 'ClientMemory', target: 'client-memory:pump-is-available', label: 'Disponibilidade calculada',
  dataType: 'Boolean', writable: true, family: 'clientMemory'
};
const booleanProperty: VisualEditorBindingSourceCatalogItem = {
  kind: 'Property', target: 'equipment:pump-01/is-available', label: 'Bomba 01 · Disponível',
  dataType: 'Boolean', writable: true, family: 'asset'
};
const numericTag: VisualEditorBindingSourceCatalogItem = {
  kind: 'Tag', target: 'Plant.Pump01.Speed', label: 'Bomba 01 · Velocidade',
  dataType: 'Double', writable: true, tagReference: { tagId: '00000000-0000-0000-0000-000000000007' }
};
const enumTag: VisualEditorBindingSourceCatalogItem = {
  kind: 'Tag', target: 'Plant.Pump01.Mode', label: 'Bomba 01 · Modo',
  dataType: 'Enum', writable: true, tagReference: { tagId: '00000000-0000-0000-0000-000000000008' }
};

test('CheckBox accepts compatible Boolean sources and only defaults concrete TAG writes to read/write', () => {
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.checkBox,
    { key: 'checked', type: 'boolean' }, booleanTag)).toBe(true);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.checkBox,
    { key: 'checked', type: 'boolean' }, booleanExpression)).toBe(true);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.checkBox,
    { key: 'checked', type: 'boolean' }, booleanClientMemory)).toBe(true);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.checkBox,
    { key: 'checked', type: 'boolean' }, booleanProperty)).toBe(true);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.checkBox,
    { key: 'checked', type: 'boolean' }, numericTag)).toBe(false);
  expect(createBindingSetIntent({ id: 'checkbox-01', type: BUILTIN_VISUAL_OBJECT_TYPES.checkBox }, 'checked', booleanTag))
    .toMatchObject({ binding: { key: 'checked', direction: 'readWrite', target: 'Plant.Pump01.Enabled' } });
  expect(createBindingSetIntent({ id: 'checkbox-01', type: BUILTIN_VISUAL_OBJECT_TYPES.checkBox }, 'checked', booleanExpression))
    .toMatchObject({ binding: { key: 'checked', direction: 'read', kind: 'Expression' } });
  for (const source of [booleanClientMemory, booleanProperty]) {
    expect(createBindingSetIntent({ id: 'checkbox-01', type: BUILTIN_VISUAL_OBJECT_TYPES.checkBox }, 'checked', source))
      .toMatchObject({ binding: { key: 'checked', direction: 'read' } });
  }
  expect(() => createBindingSetIntent({ id: 'checkbox-01', type: BUILTIN_VISUAL_OBJECT_TYPES.checkBox }, 'checked', numericTag))
    .toThrow(/not compatible/);
});

test('ComboBox options are normalized and Enum TAG values are matched without fallback', () => {
  const options = normalizeComboBoxOptions([
    { value: 'AUTO', label: 'Automático' },
    { value: 'MANUAL', label: 'Manual' }
  ]);
  expect(comboBoxOptionsEngineeringValue(options)).toEqual([
    { value: 'AUTO', label: 'Automático' },
    { value: 'MANUAL', label: 'Manual' }
  ]);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.comboBox,
    { key: 'text', type: 'string' }, enumTag)).toBe(true);
  expect(isBindingSourceCompatibleForObject(BUILTIN_VISUAL_OBJECT_TYPES.comboBox,
    { key: 'text', type: 'string' }, numericTag)).toBe(false);
  expect(resolveComboBoxSelection(options, { value: 'AUTO', dataType: 'Enum', quality: 0 })).toEqual({ state: 'selected', value: 'AUTO' });
  expect(resolveComboBoxSelection(options, { value: 'REMOTE', dataType: 'Enum', quality: 0 })).toEqual({ state: 'missing-option', value: 'REMOTE' });
  expect(resolveComboBoxSelection(options, { value: 'AUTO', dataType: 'Enum', quality: 'Bad' })).toMatchObject({ state: 'unavailable' });
  expect(resolveComboBoxSelection(options, { value: 'AUTO', dataType: 'Double', quality: 0 })).toMatchObject({ state: 'incompatible' });
  expect(() => normalizeComboBoxOptions([{ value: 'AUTO', label: 'A' }, { value: 'AUTO', label: 'B' }])).toThrow(/duplicated/);
});

test('alarm suppression is an optional typed CheckBox configuration with deterministic duration', () => {
  const config = readCheckBoxAlarmSuppressionConfig({
    mode: 'alarmSuppression', alarmDefinitionId: 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e',
    reasonCode: 'maintenance', duration: '1h'
  });
  expect(config).toMatchObject({ mode: 'alarmSuppression', reasonCode: 'maintenance', duration: '1h' });
  expect(readCheckBoxAlarmSuppressionConfig(undefined)).toBeNull();
  expect(() => readCheckBoxAlarmSuppressionConfig({ mode: 'alarmSuppression', alarmDefinitionId: 'bad' })).toThrow(/invalid/);
  expect(suppressionExpiry('1h', new Date('2026-10-31T12:00:00.000Z'))).toBe('2026-10-31T13:00:00.000Z');
  expect(suppressionExpiry('untilReleased', new Date('2026-10-31T12:00:00.000Z'))).toBeNull();
});
