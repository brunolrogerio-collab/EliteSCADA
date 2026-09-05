import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const sourceId = '11111111-1111-4111-8111-111111111111';
const tagId = '22222222-2222-4222-8222-222222222222';

const workspace = {
  projectKey: 'c04-browser-contract',
  projectName: 'C04 Browser Contract',
  baseRevision: 1,
  checkedOutAtUtc: '2026-09-03T00:00:00Z',
  lastSavedAtUtc: '2026-09-03T00:00:00Z',
  isDirty: false,
  changeVersion: 7,
  tagCount: 1,
  alarmCount: 0,
  dataSourceCount: 1,
  templateCount: 0,
  equipmentCount: 0,
  dynamoCount: 0,
  screenCount: 0,
  popupCount: 0,
  securityRoleCount: 0,
  commandCount: 0,
  visualAssetCount: 0
};

const engineeringPackage = {
  schema: 'scada.engineering',
  schemaVersion: 15,
  exportedAt: '2026-09-03T00:00:00Z',
  tags: [{
    id: tagId,
    name: 'Pressure',
    path: 'Plant.Pressure',
    dataType: 'float',
    source: 'plc-main',
    dataSourceId: null,
    address: 'holding:0',
    readOnly: true
  }],
  alarms: [],
  dataSources: [{
    id: sourceId,
    key: 'plc-main',
    name: 'Main PLC',
    driver: 'test.driver',
    enabled: true,
    settings: {}
  }],
  templates: [],
  equipment: [],
  dynamos: [],
  screens: [],
  popups: [],
  securityRoles: [],
  gateways: [],
  visualAssets: []
};

test('TAG Source selector sends stable Data Source identity through Preview without depending on the historical DEMO', async ({ page }) => {
  let previewCandidate: typeof engineeringPackage | null = null;

  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: engineeringPackage }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({
    json: {
      dataSourceTypes: [{
        typeKey: 'test.driver',
        displayName: 'Test Driver',
        kind: 'communicationDriver',
        capabilities: {
          supportsConnectionTest: false,
          supportsDiscovery: false,
          supportsBrowse: false,
          supportsFileImport: false,
          supportsReconcile: false,
          supportsSharedTransportInfrastructure: false
        },
        configurationSchema: {
          schemaId: 'test.driver',
          schemaVersion: 1,
          dataSourceFields: [],
          tagBindingFields: []
        },
        tagBindingSchemaId: 'test.driver',
        tagBindingSchemaVersion: 1
      }]
    }
  }));
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewCandidate = await route.request().postDataJSON() as typeof engineeringPackage;
    await route.fulfill({
      json: {
        mode: 'Preview',
        createCount: 0,
        updateCount: 1,
        skipCount: 0,
        errorCount: 0,
        items: [],
        canApply: true
      }
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Plant\.Pressure/ }).first().click();

  const search = page.getByTestId('tag-source-search');
  const selector = page.getByTestId('tag-source-select');
  await expect(search).toBeVisible();
  await expect(selector).toBeVisible();

  await search.fill('Main PLC');
  const identity = `id:${sourceId}`;
  await expect(selector.locator(`option[value="${identity}"]`)).toHaveCount(1);
  await selector.selectOption(identity);
  await expect(selector).toHaveValue(identity);

  await page.getByLabel('Nome').fill('Pressure C04 preview');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('Preview não altera o Workspace nem o runtime.', { exact: true })).toBeVisible();

  expect(previewCandidate).not.toBeNull();
  const previewedTag = previewCandidate!.tags.find(tag => tag.id === tagId);
  expect(previewedTag).toBeTruthy();
  expect(previewedTag?.source).toBe('plc-main');
  expect(previewedTag?.dataSourceId).toBe(sourceId);
  expect(workspace.isDirty).toBeFalsy();
  expect(workspace.changeVersion).toBe(7);
});
