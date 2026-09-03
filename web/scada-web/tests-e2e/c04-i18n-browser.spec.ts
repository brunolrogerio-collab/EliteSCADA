import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const sourceId = '33333333-3333-4333-8333-333333333333';
const tagId = '44444444-4444-4444-8444-444444444444';

async function mockC04Project(page: Page) {
  await page.route('**/api/engineering/workspace', route => route.fulfill({
    json: {
      projectKey: 'c04-i18n', projectName: 'C04 i18n', baseRevision: 1,
      checkedOutAtUtc: '2026-09-03T00:00:00Z', lastSavedAtUtc: '2026-09-03T00:00:00Z',
      isDirty: false, changeVersion: 1, tagCount: 1, alarmCount: 0, dataSourceCount: 1,
      templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
      securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
    }
  }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({
    json: {
      schema: 'scada.engineering', schemaVersion: 15, exportedAt: '2026-09-03T00:00:00Z',
      tags: [{ id: tagId, name: 'Level', path: 'Plant.Level', dataType: 'float', source: 'source-a', dataSourceId: sourceId, address: 'A1', readOnly: true }],
      alarms: [],
      dataSources: [{ id: sourceId, key: 'source-a', name: 'Source A', driver: 'test.driver', enabled: true, settings: {} }],
      templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
    }
  }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({
    json: {
      dataSourceTypes: [{
        typeKey: 'test.driver', displayName: 'Test Driver', kind: 'communicationDriver',
        capabilities: {
          supportsConnectionTest: false, supportsDiscovery: false, supportsBrowse: false,
          supportsFileImport: false, supportsReconcile: false, supportsSharedTransportInfrastructure: false
        },
        configurationSchema: { schemaId: 'test.driver', schemaVersion: 1, dataSourceFields: [], tagBindingFields: [] },
        tagBindingSchemaId: 'test.driver', tagBindingSchemaVersion: 1
      }]
    }
  }));
}

async function mockModbusCatalogProject(page: Page) {
  await page.route('**/api/engineering/workspace', route => route.fulfill({
    json: {
      projectKey: 'c04-catalog-i18n', projectName: 'C04 catalog i18n', baseRevision: 1,
      checkedOutAtUtc: '2026-09-03T00:00:00Z', lastSavedAtUtc: '2026-09-03T00:00:00Z',
      isDirty: false, changeVersion: 2, tagCount: 0, alarmCount: 0, dataSourceCount: 1,
      templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
      securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
    }
  }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({
    json: {
      schema: 'scada.engineering', schemaVersion: 15, exportedAt: '2026-09-03T00:00:00Z', tags: [], alarms: [],
      dataSources: [{ id: sourceId, key: 'modbus-main', name: 'Modbus Principal', driver: 'modbus.tcp', enabled: true, settings: { scanIntervalMilliseconds: '1000' } }],
      templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
    }
  }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({
    json: {
      dataSourceTypes: [{
        typeKey: 'modbus.tcp', displayName: 'Modbus TCP', kind: 'communicationDriver',
        capabilities: {
          supportsConnectionTest: false, supportsDiscovery: false, supportsBrowse: false,
          supportsFileImport: false, supportsReconcile: false, supportsSharedTransportInfrastructure: false
        },
        configurationSchema: {
          schemaId: 'modbus.tcp.engineering', schemaVersion: 1,
          dataSourceFields: [{
            key: 'scanIntervalMilliseconds', valueKind: 'integer', required: false,
            displayName: 'Scan interval (ms)', description: 'Polling interval in milliseconds.',
            displayNameResourceKey: 'driver.modbus.tcp.datasource.scanIntervalMilliseconds.label',
            descriptionResourceKey: 'driver.modbus.tcp.datasource.scanIntervalMilliseconds.description',
            defaultValue: '1000', allowedValues: [], minimum: 10, maximum: 600000,
            advanced: false, expectedFormat: 'whole number from 10 to 600000', exampleValue: '1000'
          }],
          tagBindingFields: []
        },
        tagBindingSchemaId: 'modbus.tcp.engineering', tagBindingSchemaVersion: 1
      }]
    }
  }));
}

test('C04 Source and Address surfaces switch pt-BR, en and es without changing canonical identifiers', async ({ page }) => {
  await mockC04Project(page);
  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Plant\.Level/ }).first().click();

  const sourceSearch = page.getByTestId('tag-source-search');
  await expect(sourceSearch).toHaveAttribute('aria-label', 'Pesquisar Data Sources configurados');
  await expect(page.getByLabel('Endereço')).toHaveValue('A1');

  await page.getByLabel('Idioma').selectOption('en');
  await expect(sourceSearch).toHaveAttribute('aria-label', 'Search configured Data Sources');
  await expect(page.getByLabel('Address')).toHaveValue('A1');

  await page.getByLabel('Language').selectOption('es');
  await expect(sourceSearch).toHaveAttribute('aria-label', 'Buscar Data Sources configurados');
  await expect(page.getByLabel('Dirección')).toHaveValue('A1');

  await expect(page.getByTestId('tag-source-select')).toHaveValue(`id:${sourceId}`);
});

test('backend Driver resource keys localize Data Source fields without changing canonical setting values', async ({ page }) => {
  await mockModbusCatalogProject(page);
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();

  const setting = page.getByTestId('data-source-setting-scanIntervalMilliseconds');
  await expect(page.getByText('Intervalo de varredura (ms)', { exact: true })).toBeVisible();
  await expect(setting).toHaveValue('1000');

  await page.getByLabel('Idioma').selectOption('en');
  await expect(page.getByText('Scan interval (ms)', { exact: true })).toBeVisible();
  await expect(setting).toHaveValue('1000');

  await page.getByLabel('Language').selectOption('es');
  await expect(page.getByText('Intervalo de sondeo (ms)', { exact: true })).toBeVisible();
  await expect(setting).toHaveValue('1000');

  await expect(page.getByTestId('data-source-type')).toHaveValue('modbus.tcp');
});
