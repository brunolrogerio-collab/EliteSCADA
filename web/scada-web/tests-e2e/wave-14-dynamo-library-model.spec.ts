import { expect, test } from '@playwright/test';
import type { DynamoEngineering } from '../src/engineering/types';
import {
  buildDynamoLibraryEntries,
  filterDynamoLibraryEntries,
  listDynamoLibraryCategories,
  selectDefaultDynamoCatalog
} from '../src/engineering/visual-editor/dynamoLibraryModel';

function dynamo(key: string, name: string, category: string, width: number, height: number, visualStyle = 'detailed-2d'): DynamoEngineering {
  return {
    id: `${key}-id`,
    key,
    name,
    properties: {
      category,
      defaultWidth: String(width),
      defaultHeight: String(height),
      visualStyle
    },
    parameters: [
      { key: 'equipmentPath', kind: 'EquipmentPath' },
      { key: 'fault', kind: 'TagReference' }
    ],
    elements: []
  };
}

const definitions = [
  dynamo('dynamo.pump.standard', 'Bomba centrífuga', 'pump', 132, 92),
  dynamo('process.motor.standard', 'Motor padrão', 'motor', 106, 92),
  dynamo('process.motor.vfd', 'Motor com inversor', 'motor', 138, 96),
  dynamo('process.valve.onoff', 'Válvula abre/fecha', 'valve', 128, 92),
  dynamo('process.tank.vertical', 'Tanque vertical', 'tank', 108, 158),
  dynamo('process.tank.vertical.high-performance', 'Tanque vertical — High Performance', 'tank', 108, 158, 'high-performance')
] as const;

test('library model exposes category dimensions thumbnail and public-interface count', () => {
  const entries = buildDynamoLibraryEntries(definitions, 'pt-BR');
  const pump = entries.find(entry => entry.definition.key === 'dynamo.pump.standard');

  expect(pump).toMatchObject({ category: 'pump', width: 132, height: 92, parameterCount: 2, visualStyle: 'detailed-2d' });
  expect(pump).not.toHaveProperty('glyph');
  expect(listDynamoLibraryCategories(entries)).toEqual(['motor', 'pump', 'tank', 'valve']);
});

test('library search is accent-insensitive and category filtering stays deterministic', () => {
  const entries = buildDynamoLibraryEntries(definitions, 'pt-BR');

  expect(filterDynamoLibraryEntries(entries, { query: 'centrifuga' }).map(entry => entry.definition.key))
    .toEqual(['dynamo.pump.standard']);
  expect(filterDynamoLibraryEntries(entries, { query: 'inversor' }).map(entry => entry.definition.key))
    .toEqual(['process.motor.vfd']);
  expect(filterDynamoLibraryEntries(entries, { category: 'motor' }).map(entry => entry.definition.key).sort())
    .toEqual(['process.motor.standard', 'process.motor.vfd']);
  expect(filterDynamoLibraryEntries(entries, { query: 'high-performance' }).map(entry => entry.definition.key))
    .toEqual(['process.tank.vertical.high-performance']);
});

test('default catalog retires only explicitly legacy built-ins and preserves project dynamos', () => {
  const legacyBuiltin: DynamoEngineering = {
    ...definitions[0], metadata: { builtinLibrary: 'true', catalogStatus: 'legacy' }
  };
  const activeBuiltin: DynamoEngineering = {
    ...definitions[1], metadata: { builtinLibrary: 'true', catalogStatus: 'active' }
  };
  const projectDynamo: DynamoEngineering = { ...definitions[2], metadata: { builtinLibrary: 'false' } };

  expect(selectDefaultDynamoCatalog([legacyBuiltin, activeBuiltin, projectDynamo]).map(item => item.key))
    .toEqual([activeBuiltin.key, projectDynamo.key]);
});
