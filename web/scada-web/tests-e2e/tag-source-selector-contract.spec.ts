import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  assignTagDataSource,
  filterTagDataSources,
  resolveTagDataSource,
  type TagSourceAwareEngineering
} from '../src/engineering/TagSourceSelector.logic';
import type { DataSourceEngineering } from '../src/engineering/types';

const sourceA: DataSourceEngineering = {
  id: '11111111-1111-1111-1111-111111111111',
  key: 'plc-line-a',
  name: 'PLC Linha A',
  driver: 'modbus.tcp',
  enabled: true
};
const sourceB: DataSourceEngineering = {
  id: '22222222-2222-2222-2222-222222222222',
  key: 'opc-main',
  name: 'Servidor OPC Principal',
  driver: 'opc-ua',
  enabled: true
};

function tag(overrides: Partial<TagSourceAwareEngineering> = {}): TagSourceAwareEngineering {
  return {
    name: 'Pressure', path: 'Plant.Pressure', dataType: 'double', readOnly: true,
    ...overrides
  };
}

const binding = {
  contractVersion: 1,
  schemaId: 'driver.binding',
  schemaVersion: 1,
  portableAddress: 'holding:10',
  settings: { mode: 'sample' }
} as const;

test.describe('TAG Source stable selection', () => {
  test('selection persists canonical id and compatibility key together', () => {
    const assigned = assignTagDataSource(tag(), sourceA);
    expect(assigned.dataSourceId).toBe(sourceA.id);
    expect(assigned.source).toBe(sourceA.key);
  });

  test('stable id survives source key rename', () => {
    const renamed = { ...sourceA, key: 'plc-line-a-renamed' };
    const resolved = resolveTagDataSource(tag({ dataSourceId: sourceA.id, source: sourceA.key }), [renamed]);
    expect(resolved.status).toBe('resolved');
    expect(resolved.source?.key).toBe('plc-line-a-renamed');
  });

  test('reselecting a renamed source preserves address and canonical binding by stable id', () => {
    const renamed = { ...sourceA, key: 'plc-line-a-renamed' };
    const assigned = assignTagDataSource(tag({
      dataSourceId: sourceA.id,
      source: sourceA.key,
      address: 'holding:10',
      communicationBinding: binding
    }), renamed);

    expect(assigned.source).toBe(renamed.key);
    expect(assigned.address).toBe('holding:10');
    expect(assigned.communicationBinding).toEqual(binding);
  });

  test('explicit migration of the same legacy key preserves address and binding while adding stable id', () => {
    const assigned = assignTagDataSource(tag({
      source: sourceA.key,
      address: 'holding:10',
      communicationBinding: binding
    }), sourceA);

    expect(assigned.dataSourceId).toBe(sourceA.id);
    expect(assigned.address).toBe('holding:10');
    expect(assigned.communicationBinding).toEqual(binding);
  });

  test('switching to a different source clears address selectors and binding', () => {
    const assigned = assignTagDataSource(tag({
      dataSourceId: sourceA.id,
      source: sourceA.key,
      address: 'holding:10',
      addressSelector: { kind: 'bit', index: 2 },
      communicationBinding: binding
    }), sourceB);

    expect(assigned.dataSourceId).toBe(sourceB.id);
    expect(assigned.address).toBeNull();
    expect(assigned.addressSelector).toBeNull();
    expect(assigned.communicationBinding).toBeNull();
  });

  test('orphaned stable id never falls back to another source with the same legacy key', () => {
    const replacement = { ...sourceB, key: sourceA.key };
    const resolved = resolveTagDataSource(tag({
      dataSourceId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      source: sourceA.key
    }), [replacement]);
    expect(resolved.status).toBe('unresolved');
    expect(resolved.source).toBeNull();
  });

  test('legacy key-only references remain resolvable for explicit migration', () => {
    const resolved = resolveTagDataSource(tag({ source: sourceA.key }), [sourceA]);
    expect(resolved.status).toBe('legacy-resolved');
    expect(resolved.source?.id).toBe(sourceA.id);
  });

  test('search matches friendly name, key and driver', () => {
    expect(filterTagDataSources([sourceA, sourceB], 'Principal')).toEqual([sourceB]);
    expect(filterTagDataSources([sourceA, sourceB], 'modbus')).toEqual([sourceA]);
    expect(filterTagDataSources([sourceA, sourceB], 'plc-line')).toEqual([sourceA]);
  });
});

test('ordinary TAG editor no longer exposes free-text Source editing', async () => {
  const editor = await readFile(new URL('../src/engineering/SecuredEngineeringEditors.tsx', import.meta.url), 'utf8');
  const selector = await readFile(new URL('../src/engineering/TagSourceSelector.tsx', import.meta.url), 'utf8');
  const tagSection = editor.slice(editor.indexOf('export function TagEditor'), editor.indexOf('export function DataSourceEditor'));

  expect(tagSection).toContain('<TagSourceSelector');
  expect(tagSection).toContain('sources={model.dataSources ?? []}');
  expect(tagSection).toContain('assignTagDataSource');
  expect(tagSection).not.toContain("<TextField label={text('editor.field.source')}");
  expect(selector).toContain('data-testid="tag-source-search"');
  expect(selector).toContain('data-testid="tag-source-select"');
  expect(selector).toContain("resolved.status === 'unresolved'");
});
