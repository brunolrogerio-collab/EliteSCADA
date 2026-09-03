import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const workspace = {
  projectKey: 'c04-opcua-source', projectName: 'C04 OPC UA Source', baseRevision: 1,
  checkedOutAtUtc: '2026-09-03T00:00:00Z', lastSavedAtUtc: '2026-09-03T00:00:00Z',
  isDirty: false, changeVersion: 5, tagCount: 0, alarmCount: 0, dataSourceCount: 0,
  templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
  securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
};

const emptyPackage = {
  schema: 'scada.engineering', schemaVersion: 15, exportedAt: '2026-09-03T00:00:00Z',
  tags: [], alarms: [], dataSources: [], templates: [], equipment: [], dynamos: [], screens: [], popups: [],
  securityRoles: [], gateways: [], visualAssets: []
};

function field(
  key: string,
  valueKind: string,
  required: boolean,
  displayName: string,
  defaultValue: string | null = null,
  allowedValues: string[] = []
) {
  return {
    key, valueKind, required, displayName, defaultValue, allowedValues,
    minimum: null, maximum: null, advanced: false,
    displayNameResourceKey: `driver.opcua.datasource.${key}.label`
  };
}

const catalog = {
  dataSourceTypes: [{
    typeKey: 'opc-ua', displayName: 'OPC UA', kind: 'communicationDriver',
    capabilities: {
      supportsConnectionTest: true, supportsDiscovery: true, supportsBrowse: true,
      supportsFileImport: false, supportsReconcile: true, supportsSharedTransportInfrastructure: false
    },
    configurationSchema: {
      schemaId: 'elitescada.driver.opc-ua', schemaVersion: 2,
      dataSourceFields: [
        field('endpointUrl', 'string', true, 'Endpoint URL'),
        field('securityMode', 'enum', true, 'Security mode', 'SignAndEncrypt', ['None', 'Sign', 'SignAndEncrypt']),
        field('securityPolicyUri', 'string', true, 'Security policy URI'),
        field('serverApplicationUri', 'string', false, 'Approved server ApplicationUri'),
        field('serverCertificateSha256', 'string', false, 'Approved server certificate SHA-256'),
        field('authenticationMode', 'enum', true, 'Authentication mode', 'Anonymous', ['Anonymous', 'UserName', 'Certificate']),
        field('passwordSecretReference', 'secretReference', false, 'Password secret reference')
      ],
      tagBindingFields: []
    },
    tagBindingSchemaId: 'elitescada.driver.opc-ua', tagBindingSchemaVersion: 2
  }]
};

test('new OPC UA Source can discover, choose security settings and test the draft before Preview/Apply', async ({ page }) => {
  let discoveryRequest: any = null;
  let connectionRequest: any = null;
  let previewCandidate: any = null;
  let applyCalls = 0;

  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: emptyPackage }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({ json: catalog }));
  await page.route('**/api/engineering/driver-tools/discover', async route => {
    discoveryRequest = await route.request().postDataJSON();
    await route.fulfill({
      json: [{
        candidateId: 'endpoint-1',
        stableIdentity: 'urn:example:server|opc.tcp://plc.example:4840|SignAndEncrypt|http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256',
        displayName: 'Example Secure OPC UA Server',
        sanitizedEndpoint: 'opc.tcp://plc.example:4840',
        suggestedSettings: {
          endpointUrl: 'opc.tcp://plc.example:4840',
          securityMode: 'SignAndEncrypt',
          securityPolicyUri: 'http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256',
          serverApplicationUri: 'urn:example:server',
          serverCertificateSha256: 'AABBCCDDEEFF',
          authenticationMode: 'Anonymous',
          passwordSecretReference: 'must-not-cross-discovery-boundary',
          unknownSetting: 'must-be-ignored'
        },
        metadata: {}, issues: []
      }]
    });
  });
  await page.route('**/api/engineering/driver-tools/connection-test', async route => {
    connectionRequest = await route.request().postDataJSON();
    await route.fulfill({
      json: {
        succeeded: true,
        sanitizedEndpoint: 'opc.tcp://plc.example:4840',
        observedIdentity: 'urn:example:server', observedProperties: {}, issues: []
      }
    });
  });
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewCandidate = await route.request().postDataJSON();
    await route.fulfill({
      json: {
        mode: 'Preview', createCount: 1, updateCount: 0, skipCount: 0,
        errorCount: 0, items: [], canApply: true
      }
    });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    applyCalls++;
    await route.fulfill({ json: { mode: 'Apply', created: 1, updated: 0, skipped: 0, issues: [] } });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();
  const sourceEditor = page.getByTestId('schema-data-source-editor');
  await sourceEditor.getByRole('button', { name: 'Nova Data Source' }).click();
  await sourceEditor.getByRole('textbox', { name: 'Nome' }).fill('OPC UA Principal');
  await sourceEditor.getByRole('textbox', { name: 'Chave' }).fill('opc-main');
  await sourceEditor.getByTestId('data-source-type').selectOption('opc-ua');

  await expect(sourceEditor.getByTestId('opcua-source-discovery-assistant')).toBeVisible();
  await sourceEditor.getByTestId('opcua-source-discovery-url').fill('opc.tcp://discovery.example:4840');
  await sourceEditor.getByTestId('opcua-source-discover').click();

  expect(discoveryRequest).toMatchObject({
    dataSource: {
      sourceKey: 'opc-main', sourceName: 'OPC UA Principal', driverType: 'opc-ua'
    },
    parameters: { discoveryUrl: 'opc.tcp://discovery.example:4840' },
    maximumResults: 100
  });
  expect(discoveryRequest.dataSource.settings.endpointUrl).toBeUndefined();

  await expect(sourceEditor.getByTestId('opcua-source-discovery-results')).toContainText('Example Secure OPC UA Server');
  await expect(sourceEditor.getByTestId('opcua-source-discovery-results')).toContainText('SignAndEncrypt');
  await expect(sourceEditor.getByTestId('opcua-source-discovery-results')).toContainText('AABBCCDDEEFF');
  await sourceEditor.getByTestId('opcua-source-use-endpoint-1').click();

  await expect(sourceEditor.getByTestId('data-source-setting-endpointUrl')).toHaveValue('opc.tcp://plc.example:4840');
  await expect(sourceEditor.getByTestId('data-source-setting-securityMode')).toHaveValue('SignAndEncrypt');
  await expect(sourceEditor.getByTestId('data-source-setting-securityPolicyUri')).toHaveValue('http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256');
  await expect(sourceEditor.getByTestId('data-source-setting-serverCertificateSha256')).toHaveValue('AABBCCDDEEFF');
  await expect(sourceEditor.getByTestId('data-source-setting-authenticationMode')).toHaveValue('Anonymous');

  await sourceEditor.getByTestId('opcua-source-test').click();
  await expect(sourceEditor.getByTestId('opcua-source-test-result')).toContainText('opc.tcp://plc.example:4840');
  expect(connectionRequest).toMatchObject({
    sourceKey: 'opc-main', sourceName: 'OPC UA Principal', driverType: 'opc-ua',
    settings: {
      endpointUrl: 'opc.tcp://plc.example:4840',
      securityMode: 'SignAndEncrypt',
      securityPolicyUri: 'http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256',
      serverCertificateSha256: 'AABBCCDDEEFF',
      authenticationMode: 'Anonymous'
    },
    secretReferences: {}
  });
  expect(connectionRequest.settings.passwordSecretReference).toBeUndefined();
  expect(connectionRequest.settings.unknownSetting).toBeUndefined();

  const previewResponse = page.waitForResponse(response =>
    response.url().includes('/api/engineering/import/json/preview') && response.request().method() === 'POST');
  await sourceEditor.getByTestId('data-source-preview').click();
  await previewResponse;
  expect(previewCandidate).not.toBeNull();
  expect(previewCandidate.dataSources).toHaveLength(1);
  expect(previewCandidate.dataSources[0]).toMatchObject({
    key: 'opc-main', driver: 'opc-ua',
    settings: {
      endpointUrl: 'opc.tcp://plc.example:4840',
      securityMode: 'SignAndEncrypt',
      securityPolicyUri: 'http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256',
      serverCertificateSha256: 'AABBCCDDEEFF',
      authenticationMode: 'Anonymous'
    }
  });
  expect(previewCandidate.dataSources[0].settings.passwordSecretReference).toBeUndefined();
  expect(previewCandidate.dataSources[0].settings.unknownSetting).toBeUndefined();
  expect(applyCalls).toBe(0);
});
