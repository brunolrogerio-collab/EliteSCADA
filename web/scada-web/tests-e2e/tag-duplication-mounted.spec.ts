import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const sourceId = '22222222-2222-4222-8222-222222222222';
const tagId = '11111111-1111-4111-8111-111111111111';

test('mounted TAG-D flow duplicates, multi-copies, previews 20 Modbus TAGs and applies through Workspace CAS', async ({ page }) => {
  let workspaceVersion = 12;
  let applyCount = 0;
  let previewCount = 0;
  let currentPackage: any = {
    schema: 'scada.engineering',
    schemaVersion: 20,
    exportedAt: '2026-09-30T10:00:00Z',
    tags: [{
      id: tagId,
      name: 'Motor Speed',
      path: 'Plant.Motor.Speed',
      dataType: 'float',
      source: 'plc-main',
      dataSourceId: sourceId,
      address: 'holding:10',
      engineeringUnit: 'rpm',
      description: 'Original',
      readOnly: false,
      historian: { enabled: true, strategy: 'periodic', periodMilliseconds: 1000 },
      historianCaptureProfileId: '33333333-3333-4333-8333-333333333333',
      metadata: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' },
      communicationBinding: {
        contractVersion: 1,
        schemaId: 'modbus.tcp.engineering',
        schemaVersion: 1,
        portableAddress: 'holding:10',
        settings: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' },
        valueTransform: { contractVersion: 1, byteSwap: true, wordSwap: false }
      },
      runtimeValue: 987.6,
      quality: 'Good',
      timestampUtc: '2026-09-30T10:00:00Z',
      historianRows: [{ value: 987.6 }],
      alarmHistory: [{ state: 'active' }],
      diagnostics: { latencyMilliseconds: 2 }
    }, {
      id: '44444444-4444-4444-8444-444444444444',
      name: 'Motor Current',
      path: 'Plant.Motor.Current',
      dataType: 'float',
      source: 'plc-main',
      dataSourceId: sourceId,
      address: 'holding:40',
      readOnly: true,
      metadata: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' },
      communicationBinding: {
        contractVersion: 1,
        schemaId: 'modbus.tcp.engineering',
        schemaVersion: 1,
        portableAddress: 'holding:40',
        settings: { 'modbus.unitId': '7', 'modbus.valueType': 'Float32' }
      }
    }],
    alarms: [],
    dataSources: [{
      id: sourceId,
      key: 'plc-main',
      name: 'PLC Principal',
      driver: 'modbus.tcp',
      enabled: true,
      settings: {}
    }],
    historianCaptureProfiles: [{
      id: '33333333-3333-4333-8333-333333333333',
      key: 'fast',
      name: 'Fast capture'
    }],
    templates: [], equipment: [], dynamos: [], screens: [], popups: [],
    securityRoles: [], gateways: [], visualAssets: []
  };

  const workspace = () => ({
    projectKey: 'tag-d-mounted',
    projectName: 'TAG-D Mounted',
    baseRevision: 1,
    checkedOutAtUtc: '2026-09-30T10:00:00Z',
    lastSavedAtUtc: '2026-09-30T10:00:00Z',
    isDirty: false,
    changeVersion: workspaceVersion,
    tagCount: currentPackage.tags.length,
    alarmCount: 0,
    dataSourceCount: 1
  });

  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace() }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: currentPackage }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({
    json: {
      dataSourceTypes: [{
        typeKey: 'modbus.tcp',
        displayName: 'Modbus TCP',
        kind: 'communicationDriver',
        capabilities: {
          supportsConnectionTest: true,
          supportsDiscovery: false,
          supportsBrowse: false,
          supportsFileImport: false,
          supportsReconcile: false,
          supportsSharedTransportInfrastructure: false
        },
        configurationSchema: {
          schemaId: 'modbus.tcp.engineering',
          schemaVersion: 1,
          dataSourceFields: [],
          tagBindingFields: []
        },
        tagBindingSchemaId: 'modbus.tcp.engineering',
        tagBindingSchemaVersion: 1
      }]
    }
  }));

  await page.route('**/api/engineering/tag-address/modbus/build', async route => {
    const request = route.request().postDataJSON() as {
      area: 'coil' | 'discrete' | 'holding' | 'input';
      reference: number;
      unitId?: number | null;
      valueType?: string | null;
      wordOrder?: string | null;
      scale?: number | null;
      offset?: number | null;
      bitIndex?: number | null;
    };
    const metadata: Record<string, string> = { 'modbus.area': request.area };
    if (request.unitId !== null && request.unitId !== undefined) metadata['modbus.unitId'] = String(request.unitId);
    if (request.valueType) metadata['modbus.valueType'] = request.valueType;
    if (request.wordOrder) metadata['modbus.wordOrder'] = request.wordOrder;
    if (request.scale !== null && request.scale !== undefined) metadata['modbus.scale'] = String(request.scale);
    if (request.offset !== null && request.offset !== undefined) metadata['modbus.offset'] = String(request.offset);
    await route.fulfill({
      json: {
        address: `${request.area}:${request.reference}`,
        metadata,
        addressSelector: request.bitIndex === null || request.bitIndex === undefined
          ? null
          : { kind: 'bit', index: request.bitIndex },
        writableArea: request.area === 'coil' || request.area === 'holding',
        canonicalReferenceBase: 'zeroBased'
      }
    });
  });

  await page.route('**/api/engineering/import/json/preview', async route => {
    previewCount += 1;
    const candidate = route.request().postDataJSON() as typeof currentPackage;
    const paths = candidate.tags.map((tag: any) => String(tag.path).toLowerCase());
    const duplicatePath = paths.some((path: string, index: number) => paths.indexOf(path) !== index);
    await route.fulfill({
      json: {
        mode: 'Preview',
        createCount: Math.max(0, candidate.tags.length - currentPackage.tags.length),
        updateCount: 0,
        skipCount: currentPackage.tags.length,
        errorCount: duplicatePath ? 1 : 0,
        items: [],
        canApply: !duplicatePath
      }
    });
  });

  await page.route('**/api/engineering/import/json/apply', async route => {
    applyCount += 1;
    expect(route.request().headers()['x-elitescada-workspace-version']).toBe(String(workspaceVersion));
    const candidate = route.request().postDataJSON();
    currentPackage = candidate;
    workspaceVersion += 1;
    await route.fulfill({
      json: {
        mode: 'CreateAndUpdate',
        created: candidate.tags.length - 2,
        updated: 0,
        skipped: 2,
        issues: []
      }
    });
  });

  await page.goto('/engineering/tags');
  await expect(page.getByTestId('tag-duplication')).toBeVisible();
  await expect(page.getByRole('button', { name: /Motor Speed/ }).first()).toBeVisible();

  // Single duplicate: no Working mutation before Preview/Apply and no runtime payload leaks.
  await page.getByTestId('tag-duplicate-selected').click();
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(1);
  expect(previewCount).toBe(0);
  expect(applyCount).toBe(0);
  await expect(page.getByLabel('Path 1')).toHaveValue('Plant.Motor.Speed_copy');

  const singlePreviewRequest = page.waitForRequest(request =>
    request.method() === 'POST' && new URL(request.url()).pathname === '/api/engineering/import/json/preview');
  await page.getByTestId('tag-duplication-preview').click();
  const singleCandidate = (await singlePreviewRequest).postDataJSON() as typeof currentPackage;
  const duplicate = singleCandidate.tags.find((tag: any) => tag.path === 'Plant.Motor.Speed_copy');
  expect(duplicate).toBeTruthy();
  expect(duplicate.id).not.toBe(tagId);
  expect(duplicate.dataSourceId).toBe(sourceId);
  expect(duplicate.communicationBinding.valueTransform).toEqual({ contractVersion: 1, byteSwap: true, wordSwap: false });
  expect(duplicate.historianCaptureProfileId).toBe('33333333-3333-4333-8333-333333333333');
  expect(duplicate.runtimeValue).toBeUndefined();
  expect(duplicate.quality).toBeUndefined();
  expect(duplicate.timestampUtc).toBeUndefined();
  expect(duplicate.historianRows).toBeUndefined();
  expect(duplicate.alarmHistory).toBeUndefined();
  expect(duplicate.diagnostics).toBeUndefined();
  expect(singleCandidate.tags.find((tag: any) => tag.id === tagId).path).toBe('Plant.Motor.Speed');
  expect(applyCount).toBe(0);

  // Multi-selection reuses the primary TAG list rather than a second entity selector.
  await page.getByRole('button', { name: 'Selecionar vários' }).click();
  await page.getByRole('button', { name: /Motor Current/ }).first().click();
  await expect(page.getByTestId('tag-duplication-selected-count')).toHaveText('2');
  await page.getByTestId('tag-copy-selected').click();
  await expect(page.getByTestId('tag-clipboard-count')).toHaveText('2');
  await page.getByRole('button', { name: 'Concluir seleção' }).click();
  await page.getByTestId('tag-paste').click();
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(2);
  const pastedIds = await page.getByTestId('tag-generated-row').locator('code').allTextContents();
  expect(new Set(pastedIds).size).toBe(2);
  expect(pastedIds).not.toContain(tagId);

  // Required first sequence: 20 canonical Modbus TAGs, editable Preview before Apply.
  await page.getByRole('button', { name: /Motor Speed/ }).first().click();
  await page.getByTestId('tag-sequence-toggle').click();
  await page.getByTestId('tag-sequence-generate').click();
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(20);
  await expect(page.getByLabel('Nome 1')).toHaveValue('Motor Speed_1');
  await expect(page.getByLabel('Nome 20')).toHaveValue('Motor Speed_20');
  await expect(page.getByLabel('Path 1')).toHaveValue('Plant.Motor.Speed_1');
  await expect(page.getByLabel('Endereço 1')).toHaveValue('holding:11');
  await expect(page.getByLabel('Endereço 20')).toHaveValue('holding:30');
  expect(applyCount).toBe(0);

  // Collision is visible locally and blocks server Preview.
  const previewsBeforeCollision = previewCount;
  await page.getByLabel('Path 1').fill('Plant.Motor.Speed');
  await expect(page.getByTestId('tag-duplication-collisions')).toContainText('Plant.Motor.Speed');
  await expect(page.getByTestId('tag-duplication-preview')).toBeDisabled();
  expect(previewCount).toBe(previewsBeforeCollision);

  await page.getByLabel('Path 1').fill('Plant.Motor.Speed_1');
  await page.getByLabel('Endereço 1').fill('holding:10');
  await expect(page.getByTestId('tag-duplication-collisions')).toContainText('holding:10');
  await expect(page.getByTestId('tag-duplication-preview')).toBeDisabled();
  await page.getByLabel('Endereço 1').fill('holding:11');

  const sequencePreviewRequest = page.waitForRequest(request =>
    request.method() === 'POST' && new URL(request.url()).pathname === '/api/engineering/import/json/preview');
  await page.getByTestId('tag-duplication-preview').click();
  const sequenceCandidate = (await sequencePreviewRequest).postDataJSON() as typeof currentPackage;
  expect(sequenceCandidate.tags.filter((tag: any) => tag.path.startsWith('Plant.Motor.Speed_')).length).toBe(20);
  expect(sequenceCandidate.tags.find((tag: any) => tag.path === 'Plant.Motor.Speed_20').address).toBe('holding:30');
  expect(applyCount).toBe(0);

  // Apply is the first mutation and uses the exact validated Workspace version.
  await expect(page.getByTestId('tag-duplication-apply')).toBeEnabled();
  await page.getByTestId('tag-duplication-apply').click();
  await page.waitForLoadState('domcontentloaded');
  expect(applyCount).toBe(1);

  // Reopen after Apply comes from the persisted canonical export, not panel/session state.
  await expect(page.getByRole('button', { name: /Motor Speed_20/ }).first()).toBeVisible();
  expect(currentPackage.tags.find((tag: any) => tag.path === 'Plant.Motor.Speed').id).toBe(tagId);
  expect(currentPackage.tags.find((tag: any) => tag.path === 'Plant.Motor.Speed_20')).toBeTruthy();
});
