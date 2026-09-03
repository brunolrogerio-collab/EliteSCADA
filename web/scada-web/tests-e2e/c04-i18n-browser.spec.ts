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
