import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test.use({ locale: 'pt-BR' });

test('Modbus family Data Source UX uses server-owned serial catalog with manual fallback and structured ranges', async ({ page }) => {
  await page.route('**/api/engineering/host/serial-ports', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      authority: 'eliteScadaServer',
      ports: [{ deviceName: 'COM3' }, { deviceName: '/dev/ttyUSB0' }]
    })
  }));

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();
  const editor = page.getByTestId('schema-data-source-editor');
  await editor.getByRole('button', { name: 'Nova Fonte de dados' }).click();

  const type = editor.getByTestId('data-source-type');
  await type.selectOption('modbus.rtu');

  const serial = editor.getByTestId('data-source-setting-serialPort');
  await expect(serial).toBeVisible();
  await expect(editor.getByTestId('server-serial-port-authority')).toContainText('servidor EliteSCADA');
  await serial.fill('/dev/custom-offline-modbus');
  await expect(serial).toHaveValue('/dev/custom-offline-modbus');

  await type.selectOption('modbus.tcp.server');
  const ranges = editor.getByTestId('holding-register-ranges-editor');
  await expect(ranges).toBeVisible();
  await expect(editor.getByTestId('holding-range-start-0')).toHaveValue('0');
  await expect(editor.getByTestId('holding-range-end-0')).toHaveValue('999');
  await editor.getByTestId('holding-range-add').click();
  await editor.getByTestId('holding-range-start-1').fill('2000');
  await editor.getByTestId('holding-range-end-1').fill('2099');
  await expect(editor.getByTestId('holding-range-start-1')).toHaveValue('2000');
  await expect(editor.getByTestId('holding-range-end-1')).toHaveValue('2099');

  await type.selectOption('modbus.rtu.server');
  await expect(editor.getByTestId('data-source-setting-serialPort')).toBeVisible();
  await expect(editor.getByTestId('holding-register-ranges-editor')).toBeVisible();
});

test('Modbus family TAG authoring reuses one assistant and limits Server to Holding Registers with explicit client access', async () => {
  const source = await readFile(
    new URL('../src/engineering/TagAddressEditor.tsx', import.meta.url),
    'utf8');

  expect(source).toContain("'modbus.tcp'");
  expect(source).toContain("'modbus.rtu'");
  expect(source).toContain("'modbus.tcp.server'");
  expect(source).toContain("'modbus.rtu.server'");
  expect(source).toContain('<ModbusAssistant tag={tag} locale={locale} onChange={onChange} serverOnly />');
  expect(source).toContain("modbus.server.clientAccess");
  expect(source).toContain('data-testid="modbus-server-client-access"');
  expect(source).toContain("{!serverOnly && <option value=\"coil\">");
  expect(source).toContain('<option value="holding">');
});

test('Modbus family descriptors and localization keep serial/range UX generic and trilingual', async () => {
  const contracts = await readFile(
    new URL('../../../src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs', import.meta.url),
    'utf8');
  const rtu = await readFile(
    new URL('../../../src/Scada.Drivers/Modbus/ModbusRtuDriverDescriptorProvider.cs', import.meta.url),
    'utf8');
  const tcpServer = await readFile(
    new URL('../../../src/Scada.Drivers/Modbus/ModbusTcpServerDriverDescriptorProvider.cs', import.meta.url),
    'utf8');
  const i18n = await readFile(
    new URL('../src/engineering/driverCatalogI18n.ts', import.meta.url),
    'utf8');

  expect(contracts).toContain('SerialPort');
  expect(rtu).toContain('DriverConfigurationValueKind.SerialPort');
  expect(tcpServer).toContain('"holdingRanges"');
  expect(i18n).toContain("'pt-BR': 'Porta serial no servidor'");
  expect(i18n).toContain("en: 'Server serial port'");
  expect(i18n).toContain("es: 'Puerto serie del servidor'");
  expect(i18n).toContain("'driver.modbus.server.datasource.holdingRanges.label'");
});
