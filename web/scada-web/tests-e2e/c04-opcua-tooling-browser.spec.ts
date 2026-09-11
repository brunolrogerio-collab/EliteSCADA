import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const sourceId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

const model = {
  schema: 'elitescada-engineering',
  schemaVersion: 15,
  exportedAt: '2026-09-03T00:00:00Z',
  tags: [
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'Existing OPC TAG',
      path: 'Plant.OPC.Existing',
      dataType: 'double',
      source: 'opc-main',
      dataSourceId: sourceId,
      address: 'node=ns%3D2%3Bs%3DExisting',
      readOnly: true
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
      settings: { endpointUrl: 'opc.tcp://127.0.0.1:4840', securityMode: 'None' }
    }
  ],
  templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
};

const workspace = {
  projectKey: 'c04-opcua-tools', projectName: 'C04 OPC UA Tools', baseRevision: 1,
  checkedOutAtUtc: '2026-09-03T00:00:00Z', lastSavedAtUtc: '2026-09-03T00:00:00Z',
  isDirty: false, changeVersion: 4, tagCount: 1, alarmCount: 0, dataSourceCount: 1,
  templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
  securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
};

const catalog = {
  dataSourceTypes: [
    {
      typeKey: 'opc-ua', displayName: 'OPC UA', kind: 'communicationDriver',
      capabilities: {
        supportsConnectionTest: true, supportsDiscovery: true, supportsBrowse: true,
        supportsFileImport: false, supportsReconcile: true, supportsSharedTransportInfrastructure: false
      },
      configurationSchema: {
        schemaId: 'elitescada.driver.opc-ua', schemaVersion: 2,
        dataSourceFields: [], tagBindingFields: []
      },
      tagBindingSchemaId: 'elitescada.driver.opc-ua', tagBindingSchemaVersion: 2
    }
  ]
};

test('OPC UA Engineering UI tests configured connection and shows sanitized discovery candidates', async ({ page }) => {
  let testCalls = 0;
  let discoverCalls = 0;

  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: model }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({ json: catalog }));
  await page.route(`**/api/engineering/data-sources/${sourceId}/driver-tools/connection-test`, async route => {
    testCalls++;
    expect(route.request().method()).toBe('POST');
    await route.fulfill({
      json: {
        succeeded: true,
        sanitizedEndpoint: 'opc.tcp://plc.example:4840',
        observedIdentity: 'urn:example:server',
        observedProperties: { productUri: 'urn:example:product' },
        issues: []
      }
    });
  });
  await page.route(`**/api/engineering/data-sources/${sourceId}/driver-tools/discover`, async route => {
    discoverCalls++;
    expect(route.request().method()).toBe('POST');
    expect(route.request().postDataJSON()).toEqual({ maximumResults: 100 });
    await route.fulfill({
      json: [
        {
          candidateId: 'urn:example:server|opc.tcp://plc.example:4840',
          stableIdentity: 'urn:example:server',
          displayName: 'Example OPC UA Server',
          sanitizedEndpoint: 'opc.tcp://plc.example:4840',
          suggestedSettings: { endpointUrl: 'opc.tcp://plc.example:4840' },
          metadata: {},
          issues: []
        }
      ]
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Plant\.OPC\.Existing/ }).first().click();
  await expect(page.getByTestId('opcua-tag-browser')).toBeVisible();

  await page.getByTestId('opcua-test-connection').click();
  await expect(page.getByTestId('opcua-connection-result')).toContainText('opc.tcp://plc.example:4840');
  await expect(page.getByTestId('opcua-connection-result')).toContainText('urn:example:server');

  await page.getByTestId('opcua-discover').click();
  await expect(page.getByTestId('opcua-discovery-results')).toContainText('Example OPC UA Server');
  await expect(page.getByTestId('opcua-discovery-results')).toContainText('opc.tcp://plc.example:4840');

  expect(testCalls).toBe(1);
  expect(discoverCalls).toBe(1);
});
