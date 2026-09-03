import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  applyModbusAddressBuild,
  metadataValue,
  parseCanonicalModbusAddress
} from '../src/engineering/TagAddressAssistant.logic';
import type { TagSourceAwareEngineering } from '../src/engineering/TagSourceSelector.logic';

test('Modbus assistant output converges legacy compatibility fields and canonical CommunicationBinding', () => {
  const tag: TagSourceAwareEngineering = {
    name: 'Status',
    path: 'Plant.Status',
    dataType: 'boolean',
    readOnly: true,
    address: 'holding:99',
    metadata: {
      'modbus.unitId': '7',
      'modbus.wordOrder': 'LowWordFirst',
      'project.note': 'keep-me'
    }
  };

  const next = applyModbusAddressBuild(tag, {
    address: 'holding:0',
    metadata: {
      'modbus.unitId': '2',
      'modbus.valueType': 'Boolean'
    },
    addressSelector: { kind: 'bit', index: 3 },
    writableArea: true,
    canonicalReferenceBase: 'zeroBased',
    bindingSchema: {
      schemaId: 'modbus.tcp.engineering',
      schemaVersion: 1
    }
  });

  expect(next.address).toBe('holding:0');
  expect(next.addressSelector).toEqual({ kind: 'bit', index: 3 });
  expect(next.metadata?.['modbus.unitId']).toBe('2');
  expect(next.metadata?.['modbus.valueType']).toBe('Boolean');
  expect(next.metadata?.['modbus.wordOrder']).toBeUndefined();
  expect(next.metadata?.['project.note']).toBe('keep-me');
  expect(next.communicationBinding).toEqual({
    contractVersion: 1,
    schemaId: 'modbus.tcp.engineering',
    schemaVersion: 1,
    portableAddress: 'holding:0',
    settings: {
      'modbus.unitId': '2',
      'modbus.valueType': 'Boolean'
    }
  });
});

test('Modbus assistant reads canonical binding settings before legacy metadata', () => {
  const tag: TagSourceAwareEngineering = {
    name: 'Pressure', path: 'Plant.Pressure', dataType: 'double', readOnly: true,
    metadata: { 'modbus.unitId': '1' },
    communicationBinding: {
      contractVersion: 1,
      schemaId: 'modbus.tcp.engineering',
      schemaVersion: 1,
      portableAddress: 'holding:10',
      settings: { 'modbus.unitId': '9' }
    }
  };

  expect(metadataValue(tag, 'modbus.unitId')).toBe('9');
});

test('manual canonical Modbus syntax is parseable without guessing reference conventions', () => {
  expect(parseCanonicalModbusAddress('holding:0')).toEqual({ area: 'holding', reference: '0' });
  expect(parseCanonicalModbusAddress('INPUT:65535')).toEqual({ area: 'input', reference: '65535' });
  expect(parseCanonicalModbusAddress('40001')).toBeNull();
});

test('TAG editor routes specialized assistants through one registry and keeps schema-driven fallback', async () => {
  const editor = await readFile(new URL('../src/engineering/SecuredEngineeringEditors.tsx', import.meta.url), 'utf8');
  const assistant = await readFile(new URL('../src/engineering/TagAddressEditor.tsx', import.meta.url), 'utf8');
  const generic = await readFile(new URL('../src/engineering/GenericTagBindingAssistant.tsx', import.meta.url), 'utf8');
  const api = await readFile(new URL('../src/engineering/tagAddressApi.ts', import.meta.url), 'utf8');
  const tagSection = editor.slice(editor.indexOf('export function TagEditor'), editor.indexOf('export function DataSourceEditor'));

  expect(tagSection).toContain('<TagAddressEditor');
  expect(assistant).toContain('specializedAssistants');
  expect(assistant).toContain("'modbus.tcp':");
  expect(assistant).toContain("'opc-ua':");
  expect(assistant).toContain("'dnp3.master':");
  expect(assistant).toContain("'iec60870.5.104':");
  expect(assistant).toContain('<GenericTagBindingAssistant');
  expect(assistant).not.toContain("source?.driver.toLowerCase() === 'modbus.tcp'");
  expect(assistant).toContain('data-testid="tag-address-manual"');
  expect(assistant).toContain('data-testid="modbus-address-assistant"');
  expect(assistant).toContain('data-testid="modbus-reference-base"');
  expect(assistant).toContain('data-testid="modbus-address-build"');
  expect(generic).toContain('configurationSchema?.tagBindingFields');
  expect(generic).toContain('tagBindingSchemaIdentity');
  expect(generic).toContain('data-testid="generic-tag-binding-assistant"');
  expect(api).toContain('/api/engineering/tag-address/modbus/build');
  expect(api).toContain("loadTagBindingSchema('modbus.tcp')");
});

test('DNP3 and IEC-104 specialized assistants validate against the backend TAG binding definition', async () => {
  const dnp3 = await readFile(new URL('../src/engineering/Dnp3TagAddressAssistant.tsx', import.meta.url), 'utf8');
  const iec104 = await readFile(new URL('../src/engineering/Iec104TagAddressAssistant.tsx', import.meta.url), 'utf8');
  const schemaResolver = await readFile(new URL('../src/engineering/TagBindingSchema.ts', import.meta.url), 'utf8');

  expect(dnp3).toContain('loadTagBindingDefinition(DNP3_DRIVER_TYPE)');
  expect(dnp3).toContain("requireAllowedTagBindingValue(definition, 'pointKind', pointKind)");
  expect(dnp3).toContain("requireTagBindingField(definition, 'index')");
  expect(iec104).toContain('loadTagBindingDefinition(IEC104_DRIVER_TYPE)');
  expect(iec104).toContain("requireAllowedTagBindingValue(definition, 'iec104.typeId', typeId)");
  expect(iec104).toContain("requireAllowedTagBindingValue(definition, 'iec104.commandTypeId', commandTypeId)");
  expect(dnp3).not.toContain('elite.dnp3');
  expect(iec104).not.toContain('elite.iec60870.5.104.point');
  expect(schemaResolver).toContain('loadDataSourceTypeCatalog');
  expect(schemaResolver).toContain('tagBindingSchemaIdentity');
  expect(schemaResolver).toContain('requireAllowedTagBindingValue');
});
