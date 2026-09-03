import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  hasDriverCatalogResource,
  resolveDriverCatalogResource
} from '../src/engineering/driverCatalogI18n';

test('driver catalog resource resolver localizes known field keys and preserves invariant fallback', () => {
  const key = 'driver.modbus.tcp.datasource.scanIntervalMilliseconds.label';

  expect(resolveDriverCatalogResource('pt-BR', key, 'Scan interval (ms)')).toBe('Intervalo de varredura (ms)');
  expect(resolveDriverCatalogResource('en', key, 'fallback')).toBe('Scan interval (ms)');
  expect(resolveDriverCatalogResource('es', key, 'fallback')).toBe('Intervalo de sondeo (ms)');
  expect(hasDriverCatalogResource(key)).toBe(true);

  expect(resolveDriverCatalogResource('es', 'driver.unknown.field', 'Invariant fallback')).toBe('Invariant fallback');
  expect(hasDriverCatalogResource('driver.unknown.field')).toBe(false);
});

test('Modbus and OPC UA descriptors publish resource keys through the canonical driver contract', async () => {
  const modbus = await readFile(new URL('../../src/Scada.Drivers/Modbus/ModbusTcpDriverDescriptorProvider.cs', import.meta.url), 'utf8');
  const opcUa = await readFile(new URL('../../src/Scada.Drivers/OpcUa/OpcUaDriverDescriptorProvider.cs', import.meta.url), 'utf8');
  const catalog = await readFile(new URL('../../src/Scada.DriverHost/Engineering/EngineeringDataSourceTypeCatalog.cs', import.meta.url), 'utf8');

  expect(modbus).toContain('DisplayNameResourceKey: "driver.modbus.tcp.datasource.host.label"');
  expect(modbus).toContain('DescriptionResourceKey: "driver.modbus.tcp.datasource.host.description"');
  expect(opcUa).toContain('DisplayNameResourceKey: "driver.opcua.datasource.endpointUrl.label"');
  expect(catalog).toContain('field.DisplayNameResourceKey');
  expect(catalog).toContain('field.DescriptionResourceKey');
});

test('shared Data Source editor resolves driver field resource keys instead of rendering invariant labels directly', async () => {
  const editor = await readFile(new URL('../src/engineering/DataSourceCatalogEditor.tsx', import.meta.url), 'utf8');
  const generic = await readFile(new URL('../src/engineering/GenericTagBindingAssistant.tsx', import.meta.url), 'utf8');

  expect(editor).toContain('resolveDriverCatalogResource(locale, field.displayNameResourceKey');
  expect(editor).toContain('resolveDriverCatalogResource(locale, field.descriptionResourceKey');
  expect(generic).toContain('resolveDriverCatalogResource(locale, field.displayNameResourceKey');
  expect(generic).toContain('resolveDriverCatalogResource(locale, field.descriptionResourceKey');
});
