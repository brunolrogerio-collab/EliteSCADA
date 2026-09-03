import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const sourceId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const existingTagId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const bindingSchemaId = 'elitescada.driver.opc-ua';
const bindingSchemaVersion = 2;

const engineeringPackage = {
  schema: 'elitescada-engineering',
  schemaVersion: 15,
  exportedAt: '2026-09-03T00:00:00Z',
  tags: [
    {
      id: existingTagId,
      name: 'Existing OPC TAG',
      path: 'Plant.OPC.Existing',
      dataType: 'double',
      source: 'opc-main',
      dataSourceId: sourceId,
      address: 'node=ns%3D2%3Bs%3DExisting',
      readOnly: true,
      communicationBinding: {
        contractVersion: 1,
        schemaId: bindingSchemaId,
        schemaVersion: bindingSchemaVersion,
        portableAddress: 'node=ns%3D2%3Bs%3DExisting',
        settings: {
          samplingInterval: '00:00:01',
          queueSize: '1',
          discardOldest: 'true'
        }
      }
    }
  ],
  alarms: [],
  dataSources: [
    {
      id: sourceId,
      key: 'opc-main',
      name: 'OPC UA Principal',
      driver: 'opc-ua',
      enabled: true,
      settings: {
        endpointUrl: 'opc.tcp://127.0.0.1:4840',
        securityMode: 'None'
      }
    }
  ],
  templates: [],
  equipment: [],
  dynamos: [],
  screens: [],
  popups: [],
  securityRoles: [],
  gateways: [],
  visualAssets: []
};

const workspace = {
  projectKey: 'c04-browser-test',
  projectName: 'C04 Browser Test',
  baseRevision: 1,
  checkedOutAtUtc: '2026-09-03T00:00:00Z',
  lastSavedAtUtc: '2026-09-03T00:00:00Z',
  isDirty: false,
  changeVersion: 11,
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

const catalog = {
  dataSourceTypes: [
    {
      typeKey: 'opc-ua',
      displayName: 'OPC UA',
      kind: 'communicationDriver',
      capabilities: {
        supportsConnectionTest: true,
        supportsDiscovery: true,
        supportsBrowse: true,
        supportsFileImport: false,
        supportsReconcile: true,
        supportsSharedTransportInfrastructure: false
      },
      configurationSchema: {
        schemaId: 'opcua.source.configuration',
        schemaVersion: 9,
        dataSourceFields: [],
        tagBindingFields: [
          field('samplingInterval', 'duration', '00:00:01'),
          field('queueSize', 'integer', '1'),
          field('discardOldest', 'boolean', 'true')
        ]
      },
      tagBindingSchemaId: bindingSchemaId,
      tagBindingSchemaVersion: bindingSchemaVersion
    }
  ]
};

const browsePage = {
  nodes: [
    {
      nodeId: 'ns=2;s=Temperature',
      stableIdentity: 'nsu=urn:test;s=Temperature',
      displayName: 'Temperature',
      isContainer: false,
      isReadable: true,
      isWritable: false,
      portableAddress: 'node=ns%3D2%3Bs%3DTemperature',
      suggestedDataType: 'double',
      engineeringUnit: 'degC',
      metadata: { 'opcUa.description': 'Temperature from browse' }
    },
    {
      nodeId: 'ns=2;s=Pressure',
      stableIdentity: 'nsu=urn:test;s=Pressure',
      displayName: 'Pressure',
      isContainer: false,
      isReadable: true,
      isWritable: true,
      portableAddress: 'node=ns%3D2%3Bs%3DPressure',
      suggestedDataType: 'double',
      engineeringUnit: 'bar'
    }
  ],
  continuationToken: null,
  isPartial: false,
  issues: []
};

test('OPC UA browser multi-select creates canonical TAG candidates through Preview and Apply boundaries', async ({ page }) => {
  let previewCandidate: typeof engineeringPackage | null = null;
  let appliedCandidate: typeof engineeringPackage | null = null;
  let applyVersion: string | null = null;

  await page.route('**/api/engineering/workspace', async route => {
    await route.fulfill({ json: workspace });
  });
  await page.route('**/api/engineering/export/json', async route => {
    await route.fulfill({ json: engineeringPackage });
  });
  await page.route('**/api/engineering/data-source-types', async route => {
    await route.fulfill({ json: catalog });
  });
  await page.route(`**/api/engineering/data-sources/${sourceId}/driver-tools/browse`, async route => {
    expect(route.request().method()).toBe('POST');
    await route.fulfill({ json: browsePage });
  });
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewCandidate = route.request().postDataJSON() as typeof engineeringPackage;
    await route.fulfill({
      json: {
        mode: 'Preview',
        createCount: 2,
        updateCount: 0,
        skipCount: 0,
        errorCount: 0,
        items: [],
        canApply: true
      }
    });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    appliedCandidate = route.request().postDataJSON() as typeof engineeringPackage;
    applyVersion = route.request().headers()['x-elitescada-workspace-version'] ?? null;
    await route.fulfill({
      json: {
        mode: 'Apply',
        created: 2,
        updated: 0,
        skipped: 0,
        issues: []
      }
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Plant\.OPC\.Existing/ }).first().click();

  await expect(page.getByTestId('opcua-tag-browser')).toBeVisible();
  await page.getByTestId('opcua-browse-root').click();
  await expect(page.getByTestId('opcua-browse-results')).toBeVisible();
  await expect(page.getByText('Temperature', { exact: true })).toBeVisible();
  await expect(page.getByText('Pressure', { exact: true })).toBeVisible();

  const checkboxes = page.getByTestId('opcua-browse-results').locator('input[type="checkbox"]');
  await expect(checkboxes).toHaveCount(2);
  await checkboxes.nth(0).check();
  await checkboxes.nth(1).check();

  await page.getByTestId('opcua-import-prefix').fill('Plant.Imported');
  await page.getByTestId('opcua-import-preview').click();
  await expect(page.getByTestId('opcua-import-preview-result')).toContainText('2 criar');

  expect(previewCandidate).not.toBeNull();
  const imported = previewCandidate!.tags.filter(tag => tag.path.startsWith('Plant.Imported.'));
  expect(imported).toHaveLength(2);
  expect(imported.map(tag => tag.path).sort()).toEqual([
    'Plant.Imported.Pressure',
    'Plant.Imported.Temperature'
  ]);
  for (const tag of imported) {
    expect(tag.source).toBe('opc-main');
    expect(tag.dataSourceId).toBe(sourceId);
    expect(tag.address).toBe(tag.communicationBinding?.portableAddress);
    expect(tag.communicationBinding?.schemaId).toBe(bindingSchemaId);
    expect(tag.communicationBinding?.schemaVersion).toBe(bindingSchemaVersion);
  }

  page.once('dialog', async dialog => {
    expect(dialog.type()).toBe('confirm');
    await dialog.accept();
  });
  const applyResponse = page.waitForResponse(response =>
    response.url().includes('/api/engineering/import/json/apply') && response.request().method() === 'POST');
  await page.getByTestId('opcua-import-apply').click();
  await applyResponse;

  expect(appliedCandidate).not.toBeNull();
  expect(appliedCandidate).toEqual(previewCandidate);
  expect(applyVersion).toBe(String(workspace.changeVersion));
});

function field(key: string, valueKind: string, defaultValue: string) {
  return {
    key,
    valueKind,
    required: false,
    displayName: key,
    defaultValue,
    allowedValues: [],
    advanced: false
  };
}
