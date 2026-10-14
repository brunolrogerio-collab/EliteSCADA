import { expect, test, type Page } from '@playwright/test';
import { buildMaterializationPackage } from '../src/engineering/homeBuildingWorkspace.logic';

const sourceId = 'e4827754-9884-4751-9ac1-4073bb11d7e7';
const locationId = '099c2645-9233-421a-9fd9-1e3b9e3f4d01';
const secondSourceId = 'd8ce1d20-b0e3-487c-963f-6da0b0eacaf1';
const initialWorkspace = {
  projectKey: 'home-building-e2e', projectName: 'Home Building E2E', baseRevision: 1,
  checkedOutAtUtc: '2026-10-13T00:00:00Z', lastSavedAtUtc: '2026-10-13T00:00:00Z',
  isDirty: false, changeVersion: 12, tagCount: 0, alarmCount: 0, dataSourceCount: 1,
  templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
  securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
};

const source = {
  id: sourceId, key: 'ha-main', name: 'Home Assistant', driver: 'home-assistant', enabled: true,
  settings: { host: 'ha.local', port: '8123' }, secretReferences: { accessToken: 'host-material-slot-v1:configured' }
};

const packageView = {
  schema: 'scada.engineering', schemaVersion: 23, exportedAt: '2026-10-13T00:00:00Z',
  tags: [], alarms: [], dataSources: [source], templates: [], equipment: [], locations: [
    { id: locationId, name: 'Apartment', kind: 'Building' }
  ], commands: [], dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
};

const inventoryPackage = {
  ...packageView,
  dataSources: [source, {
    id: secondSourceId, key: 'zigbee-main', name: 'Zigbee Coordinator',
    driver: 'zigbee2mqtt', enabled: true, settings: {}, secretReferences: {}
  }],
  locations: [
    ...packageView.locations,
    { id: '6ae04841-005f-4cbc-a0fb-345c70f84cbb', name: 'Utility room', kind: 'AreaRoom', parentLocationId: locationId }
  ],
  equipment: [
    {
      id: '29a5a3b2-4a58-47d5-b95c-96552906714d', path: 'Home/Shared/Light', name: 'Multi-bound light',
      locationId, sourceBindings: [
        { dataSourceId: secondSourceId, stableDeviceIdentity: 'zigbee-ieee-1', role: 'secondary' },
        { dataSourceId: sourceId, stableDeviceIdentity: 'ha-registry-1', role: 'primary' }
      ], capabilities: []
    },
    {
      id: '250fbb58-bc6c-4ddf-b0d4-e1ad8ff0f752', path: 'Home/Unassigned/Sensor', name: 'Unassigned sensor',
      locationId: null, sourceBindings: [
        { dataSourceId: sourceId, stableDeviceIdentity: 'ha-registry-2', role: 'primary' }
      ], capabilities: []
    }
  ]
};

const catalog = {
  dataSourceTypes: [{
    typeKey: 'home-assistant', displayName: 'Home Assistant local bridge', kind: 'communicationDriver',
    description: 'Selected local Home Assistant entities.',
    capabilities: { supportsConnectionTest: true, supportsDiscovery: true, supportsBrowse: false, supportsFileImport: false, supportsReconcile: false, supportsSharedTransportInfrastructure: false },
    configurationSchema: {
      schemaId: 'elitescada.driver.home-assistant', schemaVersion: 1,
      dataSourceFields: [],
      tagBindingFields: [{ key: 'address', valueKind: 'string', required: true, displayName: 'Selected entity', defaultValue: null, allowedValues: [], advanced: false }]
    },
    tagBindingSchemaId: 'elitescada.driver.home-assistant', tagBindingSchemaVersion: 1,
    integrationDomains: ['building', 'residential', 'ioT'], connectionModel: 'directNetwork',
    externalDependencies: [{ kind: 'builtIn', requirement: null, description: null }]
  }]
};

const discoveryCandidate = {
  candidateId: 'ha-candidate-kitchen-light', stableIdentity: 'ha-device-registry-42', displayName: 'Kitchen Light',
  sanitizedEndpoint: 'ws://ha.local:8123', issues: [],
  materialization: {
    equipment: {
      candidateId: 'ha-equipment-kitchen-light', path: 'HomeAssistant/ha-main/device-kitchen-light', name: 'Kitchen Light',
      stableDeviceIdentity: 'ha-device-registry-42', sourceRole: 'primary', locationPlaceholder: 'HA Area:kitchen',
      capabilities: [{ capabilityId: 'kitchen-light.onoff', kind: 'OnOff', version: 1, metadata: {}, bindings: [
        { role: 'state', tagCandidateId: 'ha-tag-kitchen-light-state' },
        { role: 'turnOn', commandCandidateId: 'ha-command-kitchen-light-on' }
      ] }], metadata: { manufacturer: 'Example' }
    },
    tags: [{
      candidateId: 'ha-tag-kitchen-light-state', name: 'Kitchen Light state', path: 'HomeAssistant/kitchen-light/state',
      dataType: 'boolean', portableAddress: 'entity:light.kitchen:state', readOnly: false,
      metadata: { address: 'entity:light.kitchen:state', writeServiceDomain: 'light', writeService: 'turn_on' }
    }],
    commands: [{
      candidateId: 'ha-command-kitchen-light-on', key: 'light.kitchen.turn_on', name: 'Turn kitchen light on',
      value: 'true', targetTagCandidateId: 'ha-tag-kitchen-light-state', metadata: {}
    }]
  }
};

async function installApi(page: Page, previewCanApply = true, initialPackageView = packageView) {
  let previewBody: Record<string, any> | null = null;
  let applyBody: Record<string, any> | null = null;
  let applyVersion: string | null = null;
  let applyCalls = 0;
  let discoveryCalls = 0;
  let exportCalls = 0;
  let workspace = {
    ...initialWorkspace,
    dataSourceCount: initialPackageView.dataSources.length,
    equipmentCount: initialPackageView.equipment.length,
    tagCount: initialPackageView.tags.length,
    commandCount: initialPackageView.commands?.length ?? 0
  };
  let currentPackage = structuredClone(initialPackageView);

  await page.route('**/api/**', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
  await page.route('**/api/product/info', route => route.fulfill({ json: { displayVersion: 'e2e', buildCommit: 'local' } }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: { subjectId: 'home-building-e2e', username: 'home-building-e2e', displayName: 'Home Building E2E', roles: ['developer'], identityProvider: 'local' } }));
  await page.route('**/api/auth/config', route => route.fulfill({ json: { authenticationEnabled: true } }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: ['View', 'SystemAdmin'],
    workspace: ['EngineeringView', 'EngineeringModify', 'SystemAdmin']
  } }));
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: { configured: false, locked: false } }));
  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace }));
  await page.route('**/api/engineering/export/json', route => {
    exportCalls++;
    return route.fulfill({ json: currentPackage });
  });
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({ json: catalog }));
  await page.route(`**/api/engineering/data-sources/${sourceId}/driver-tools/discover`, async route => {
    discoveryCalls++;
    expect(route.request().method()).toBe('POST');
    await route.fulfill({ json: [discoveryCandidate] });
  });
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewBody = await route.request().postDataJSON();
    const previewItems = [
      ...(previewBody.locations ?? []).map((item: any) => ({ entityKind: 'Location', entityKey: item.id ?? item.name, operation: item.id === locationId ? 'Update' : 'Create', issues: [] })),
      ...(previewBody.equipment ?? []).map((item: any) => ({ entityKind: 'Equipment', entityKey: item.path, operation: 'Create', issues: [] })),
      ...(previewBody.tags ?? []).map((item: any) => ({ entityKind: 'Tag', entityKey: item.path, operation: 'Create', issues: [] })),
      ...(previewBody.commands ?? []).map((item: any) => ({ entityKind: 'Command', entityKey: item.key, operation: 'Create', issues: [] }))
    ];
    await route.fulfill({ json: previewCanApply
      ? { mode: 'Preview', createCount: 3, updateCount: 0, skipCount: 0, errorCount: 0, items: previewItems, canApply: true }
      : { mode: 'Preview', createCount: 0, updateCount: 0, skipCount: 0, errorCount: 1, items: [{ entityKind: 'Equipment', entityKey: 'HomeAssistant/kitchen-light', operation: 'Create', issues: [{ code: 'EQUIPMENT_LOCATION_NOT_FOUND', message: 'Location reference is not in the package.', entityKind: 'Equipment', entityKey: 'HomeAssistant/kitchen-light', isError: true }] }], canApply: false }
    });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    applyCalls++;
    applyBody = await route.request().postDataJSON();
    applyVersion = route.request().headers()['x-elitescada-workspace-version'] ?? null;
    if (applyVersion !== String(workspace.changeVersion)) {
      await route.fulfill({ status: 409, json: { error: 'Engineering workspace version changed.' } });
      return;
    }
    currentPackage = { ...currentPackage, ...applyBody };
    workspace = { ...workspace, changeVersion: workspace.changeVersion + 1, isDirty: true };
    await route.fulfill({ json: { mode: 'Apply', created: 3, updated: 0, skipped: 0, issues: [] } });
  });
  return {
    get previewBody() { return previewBody; },
    get applyBody() { return applyBody; },
    get applyVersion() { return applyVersion; },
    get applyCalls() { return applyCalls; },
    get discoveryCalls() { return discoveryCalls; },
    get exportCalls() { return exportCalls; },
    get currentPackage() { return currentPackage; }
  };
}

test('device discovery stays read-only until canonical Preview succeeds, then applies equipment, TAG, command, capability and location', async ({ page }) => {
  const calls = await installApi(page);
  await page.goto('/engineering/homeBuilding');
  await page.getByTestId('home-building-workspace').waitFor();
  await page.getByTestId('home-building-tab-devices').click();
  await page.getByTestId('home-building-discover').click();
  const candidateCard = page.getByTestId('home-building-candidate-ha-candidate-kitchen-light');
  await expect(candidateCard).toBeVisible();
  await expect(candidateCard).not.toContainText('ha-device-registry-42');
  const candidateDiagnostics = page.getByTestId('home-building-candidate-diagnostics-ha-candidate-kitchen-light');
  await expect(candidateDiagnostics).toHaveJSProperty('open', false);
  await candidateDiagnostics.locator('summary').click();
  await expect(candidateDiagnostics.getByTestId('home-building-candidate-identity')).toHaveText('ha-device-registry-42');
  expect(calls.discoveryCalls).toBe(1);
  expect(calls.applyCalls).toBe(0);

  await candidateCard.click();
  const selectedDiagnostics = page.getByTestId('home-building-selected-device-diagnostics');
  await expect(selectedDiagnostics).toHaveJSProperty('open', false);
  await selectedDiagnostics.locator('summary').click();
  await expect(selectedDiagnostics.getByTestId('home-building-selected-device-identity')).toHaveText('ha-device-registry-42');
  await page.getByTestId('home-building-device-location').selectOption(locationId);
  await page.getByTestId('home-building-device-preview').click();
  await expect(page.getByTestId('home-building-preview-summary')).toContainText('Preview is valid');
  expect(calls.applyCalls).toBe(0);
  const previewEquipment = calls.previewBody?.equipment?.find((item: any) => item.path === 'HomeAssistant/ha-main/device-kitchen-light');
  expect(previewEquipment).toMatchObject({ locationId, name: 'Kitchen Light', sourceBindings: [{ dataSourceId: sourceId, stableDeviceIdentity: 'ha-device-registry-42' }] });
  expect(calls.previewBody?.tags?.[0]).toMatchObject({
    dataSourceId: sourceId, address: 'entity:light.kitchen:state',
    communicationBinding: { schemaId: 'elitescada.driver.home-assistant', portableAddress: 'entity:light.kitchen:state' }
  });
  expect(calls.previewBody?.commands?.[0]).toMatchObject({ key: 'light.kitchen.turn_on', targetTagPath: 'HomeAssistant/kitchen-light/state' });
  expect(previewEquipment.capabilities[0].bindings).toHaveLength(2);
  const previewItems = page.getByTestId('home-building-preview-items');
  await expect(previewItems).toContainText('Equipment · HomeAssistant/ha-main/device-kitchen-light');
  await expect(previewItems).toContainText('Tag · HomeAssistant/kitchen-light/state');
  await expect(previewItems).toContainText('Command · light.kitchen.turn_on');
  await expect(previewItems).toContainText('OnOff (kitchen-light.onoff)');
  await expect(previewItems).toContainText('Create');

  await page.getByTestId('home-building-device-apply').click();
  await expect.poll(() => calls.applyCalls).toBe(1);
  await expect.poll(() => calls.exportCalls).toBeGreaterThan(2);
  expect(calls.applyVersion).toBe('12');
  expect(calls.applyBody?.equipment).toEqual(calls.previewBody?.equipment);
  expect(calls.currentPackage.equipment).toEqual(calls.applyBody?.equipment);
  expect(calls.currentPackage.tags).toEqual(calls.applyBody?.tags);
  expect(calls.currentPackage.commands).toEqual(calls.applyBody?.commands);
});

test('invalid device Preview is visible and blocks Apply without changing Engineering', async ({ page }) => {
  const calls = await installApi(page, false);
  await page.goto('/engineering/homeBuilding');
  await page.getByTestId('home-building-tab-devices').click();
  await page.getByTestId('home-building-discover').click();
  await page.getByTestId('home-building-candidate-ha-candidate-kitchen-light').click();
  await page.getByTestId('home-building-device-preview').click();
  await expect(page.getByTestId('home-building-preview-summary')).toContainText('Preview has errors');
  await expect(page.getByTestId('home-building-device-apply')).toBeDisabled();
  expect(calls.previewBody).not.toBeNull();
  expect(calls.applyCalls).toBe(0);
});

test('Locations uses canonical JSON Preview/Apply with stable identity and workspace-version guard', async ({ page }) => {
  const calls = await installApi(page);
  await page.goto('/engineering/homeBuilding');
  await page.getByTestId('home-building-tab-locations').click();
  await page.getByTestId('home-building-location-name').fill('Plant room');
  await page.getByTestId('home-building-location-kind').selectOption('AreaRoom');
  await page.getByTestId('home-building-location-parent').selectOption(locationId);
  await page.getByTestId('home-building-location-preview').click();
  await expect(page.getByTestId('home-building-preview-summary')).toContainText('Preview is valid');
  expect(calls.previewBody?.locations).toContainEqual(expect.objectContaining({ name: 'Plant room', kind: 'AreaRoom', parentLocationId: locationId }));
  await page.getByTestId('home-building-location-apply').click();
  await expect.poll(() => calls.applyCalls).toBe(1);
  expect(calls.applyVersion).toBe('12');
  expect(calls.currentPackage.locations).toContainEqual(expect.objectContaining({ name: 'Plant room', kind: 'AreaRoom', parentLocationId: locationId }));
});

test('registered devices are grouped and filtered by canonical location and every actual integration binding', async ({ page }) => {
  await installApi(page, true, inventoryPackage);
  await page.goto('/engineering/homeBuilding');
  await page.getByTestId('home-building-tab-devices').click();

  const integrationGroups = page.getByTestId('home-building-integration-group');
  const homeAssistantGroup = integrationGroups.filter({ hasText: 'Home Assistant · home-assistant' });
  const zigbeeGroup = integrationGroups.filter({ hasText: 'Zigbee Coordinator · zigbee2mqtt' });
  await expect(homeAssistantGroup.getByText('Multi-bound light')).toBeVisible();
  await expect(zigbeeGroup.getByText('Multi-bound light')).toBeVisible();
  await expect(homeAssistantGroup).toHaveCount(2);
  const registeredCard = homeAssistantGroup.getByTestId('home-building-registered-device').filter({ hasText: 'Multi-bound light' });
  await expect(registeredCard).toHaveCount(1);
  await expect(registeredCard.locator(':scope > span')).not.toContainText('ha-registry-1');
  const registeredDiagnostics = registeredCard.getByTestId('home-building-registered-device-diagnostics');
  await expect(registeredDiagnostics).toHaveJSProperty('open', false);
  await registeredDiagnostics.locator('summary').click();
  await expect(registeredDiagnostics).toContainText('ha-registry-1');

  await page.getByTestId('home-building-device-integration-filter').selectOption(sourceId);
  await page.getByTestId('home-building-device-location-filter').selectOption(locationId);
  await expect(page.getByTestId('home-building-registered-device')).toHaveCount(1);
  await expect(page.getByTestId('home-building-registered-device')).toContainText('ha-registry-1');
  await expect(page.getByTestId('home-building-registered-device')).not.toContainText('Unassigned sensor');
});

test('device candidate identities are scoped by integration and stable device, preserving unrelated TAGs and commands', () => {
  const manualTag = {
    id: '11111111-1111-4111-8111-111111111111', name: 'Manual state', path: 'Manual/Keep/State',
    dataType: 'boolean', source: source.key, dataSourceId: sourceId, address: 'manual:state', readOnly: false
  };
  const manualCommand = {
    id: '22222222-2222-4222-8222-222222222222', key: 'switch.turn_on', name: 'Operator command',
    kind: 'WriteTagValue', value: 'false', targetTagId: manualTag.id, targetTagPath: manualTag.path,
    equipmentPath: 'Manual/Keep', enabled: true, metadata: { owner: 'operator' }
  };
  const base = { ...structuredClone(packageView), tags: [manualTag], commands: [manualCommand] };
  const candidateFor = (device: string, identity: string) => {
    const materialization = structuredClone(discoveryCandidate.materialization);
    materialization.equipment = {
      ...materialization.equipment,
      candidateId: `equipment-${device}`,
      name: device,
      path: `HomeAssistant/ha-main/${device}`,
      stableDeviceIdentity: identity,
      capabilities: []
    };
    materialization.tags = [{
      ...materialization.tags[0], candidateId: 'temperature', name: `${device} temperature`,
      path: `HomeAssistant/${device}/temperature`
    }];
    materialization.commands = [{
      ...materialization.commands[0], candidateId: 'turn-on', key: 'switch.turn_on',
      name: `${device} switch on`, targetTagCandidateId: 'temperature'
    }];
    return { ...discoveryCandidate, candidateId: `discovery-${device}`, stableIdentity: identity, materialization };
  };
  let generatedId = 0;
  const idFactory = () => `00000000-0000-4000-8000-${String(++generatedId).padStart(12, '0')}`;
  const firstCandidate = candidateFor('DeviceA', 'stable-device-a');
  const secondCandidate = candidateFor('DeviceB', 'stable-device-b');
  const first = buildMaterializationPackage(base, source, catalog.dataSourceTypes[0], firstCandidate, {
    equipmentPath: 'HomeAssistant/ha-main/DeviceA', idFactory
  });
  const both = buildMaterializationPackage(first, source, catalog.dataSourceTypes[0], secondCandidate, {
    equipmentPath: 'HomeAssistant/ha-main/DeviceB', idFactory
  });

  expect(both.commands).toContainEqual(manualCommand);
  expect(both.tags).toContainEqual(manualTag);
  expect(both.tags?.filter(tag => tag.metadata?.['homeBuilding.dataSourceId'] === sourceId)).toHaveLength(2);
  expect(both.tags?.find(tag => tag.path === 'HomeAssistant/DeviceA/temperature')?.metadata?.['homeBuilding.stableDeviceIdentity']).toBe('stable-device-a');
  expect(both.tags?.find(tag => tag.path === 'HomeAssistant/DeviceB/temperature')?.metadata?.['homeBuilding.stableDeviceIdentity']).toBe('stable-device-b');
  const materializedCommands = both.commands?.filter(command => command.metadata?.['homeBuilding.dataSourceId'] === sourceId) ?? [];
  expect(materializedCommands).toHaveLength(2);
  expect(new Set(materializedCommands.map(command => command.key.toLowerCase())).size).toBe(2);
  expect(materializedCommands.map(command => command.metadata?.['homeBuilding.stableDeviceIdentity']).sort()).toEqual(['stable-device-a', 'stable-device-b']);

  const refreshed = buildMaterializationPackage(both, source, catalog.dataSourceTypes[0], firstCandidate, {
    equipmentPath: 'HomeAssistant/ha-main/DeviceA', idFactory
  });
  expect(refreshed.tags).toHaveLength(3);
  expect(refreshed.commands).toHaveLength(3);
  expect(refreshed.commands).toContainEqual(manualCommand);
  expect(refreshed.tags?.find(tag => tag.path === 'HomeAssistant/DeviceB/temperature')?.metadata?.['homeBuilding.stableDeviceIdentity']).toBe('stable-device-b');
});
