import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const libraryId = '11111111-1111-4111-8111-111111111111';
const screenId = '22222222-2222-4222-8222-222222222222';
const dynamoId = '33333333-3333-4333-8333-333333333333';

const descriptor = {
  libraryId,
  name: 'Process Library',
  version: '2.1.0',
  contentSha256: 'a'.repeat(64),
  resourceCount: 2,
  byteLength: 4096
};

const libraryResources = [
  {
    resourceId: screenId,
    kind: 'screen',
    sourceKey: 'screen.operator',
    displayName: 'Operator Overview',
    payloadPath: `resources/screen/${screenId}.json`,
    dependencies: [{ kind: 'dynamo', resourceId: dynamoId }]
  },
  {
    resourceId: dynamoId,
    kind: 'dynamo',
    sourceKey: 'dynamo.pump',
    displayName: 'Pump Dynamo',
    payloadPath: `resources/dynamo/${dynamoId}.json`,
    dependencies: []
  }
];

function workspace(changeVersion: number, isDirty: boolean) {
  return {
    projectKey: 'c25-library-ui',
    projectName: 'C25 Library UI',
    baseRevision: 7,
    checkedOutAtUtc: '2026-09-06T12:00:00Z',
    lastSavedAtUtc: '2026-09-06T12:00:00Z',
    isDirty,
    changeVersion,
    tagCount: 0,
    alarmCount: 0,
    dataSourceCount: 0,
    templateCount: 0,
    equipmentCount: 0,
    dynamoCount: isDirty ? 1 : 0,
    screenCount: isDirty ? 1 : 0,
    popupCount: 0,
    securityRoleCount: 0,
    commandCount: 0,
    visualAssetCount: 0
  };
}

function engineeringPackage(withIncorporatedScreen: boolean) {
  return {
    schema: 'scada.engineering',
    schemaVersion: 10,
    exportedAt: '2026-09-06T12:00:00Z',
    tags: [],
    alarms: [],
    dataSources: [],
    templates: [],
    equipment: [],
    dynamos: withIncorporatedScreen ? [{
      id: dynamoId,
      key: 'dynamo.pump',
      name: 'Pump Dynamo',
      elements: [],
      metadata: {
        'elitescada.reusable.origin.libraryId': libraryId,
        'elitescada.reusable.origin.libraryVersion': '2.1.0',
        'elitescada.reusable.origin.resourceId': dynamoId,
        'elitescada.reusable.origin.resourceKind': 'dynamo',
        'elitescada.reusable.origin.payloadSha256': 'b'.repeat(64)
      }
    }] : [],
    screens: withIncorporatedScreen ? [{
      id: screenId,
      key: 'screen.operator',
      name: 'Operator Overview',
      elements: [{ key: 'pump', type: 'dynamo', dynamoKey: 'dynamo.pump' }],
      metadata: {
        'elitescada.reusable.origin.libraryId': libraryId,
        'elitescada.reusable.origin.libraryVersion': '2.1.0',
        'elitescada.reusable.origin.resourceId': screenId,
        'elitescada.reusable.origin.resourceKind': 'screen',
        'elitescada.reusable.origin.payloadSha256': 'c'.repeat(64)
      }
    }] : [],
    popups: [],
    securityRoles: [],
    commands: [],
    gateways: [],
    scripts: [],
    visualAssets: []
  };
}

async function installUnlockedLibraryContract(page: Page) {
  let changeVersion = 4;
  let dirty = false;
  let incorporated = false;
  let associated = false;
  let incorporateRequests = 0;
  let exportRequest: Record<string, unknown> | null = null;

  await page.route('**/api/engineering/lock/status', route => route.fulfill({
    json: { configured: true, locked: false }
  }));
  await page.route('**/api/engineering/workspace', route => route.fulfill({
    json: workspace(changeVersion, dirty)
  }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({
    json: engineeringPackage(incorporated)
  }));
  await page.route('**/api/engineering/libraries', route => {
    if (route.request().method() !== 'GET') return route.fallback();
    return route.fulfill({ json: associated ? [descriptor] : [] });
  });
  await page.route('**/api/engineering/libraries/associate', async route => {
    expect(route.request().headers()['content-type']).toContain('application/vnd.elitescada.resource-library');
    associated = true;
    return route.fulfill({
      json: { association: descriptor, added: true, workingChanged: false }
    });
  });
  await page.route(`**/api/engineering/libraries/${libraryId}/resources`, route => route.fulfill({
    json: { library: descriptor, resources: libraryResources }
  }));
  await page.route(`**/api/engineering/libraries/${libraryId}/resources/${screenId}/incorporate`, async route => {
    incorporateRequests++;
    expect(route.request().headers()['x-elitescada-workspace-version']).toBe('4');
    expect(route.request().postDataJSON()).toEqual({ kind: 'screen' });
    incorporated = true;
    dirty = true;
    changeVersion = 5;
    return route.fulfill({
      json: {
        libraryId,
        resourceId: screenId,
        kind: 'screen',
        incorporated: true,
        created: 2,
        deduplicated: 0,
        closureCount: 2,
        changeVersion
      }
    });
  });
  await page.route(`**/api/engineering/libraries/${libraryId}`, route => {
    if (route.request().method() !== 'DELETE') return route.fallback();
    associated = false;
    return route.fulfill({ json: { libraryId, workingChanged: false } });
  });
  await page.route('**/api/engineering/libraries/export', async route => {
    exportRequest = route.request().postDataJSON() as Record<string, unknown>;
    return route.fulfill({
      status: 200,
      headers: {
        'content-type': 'application/vnd.elitescada.resource-library',
        'content-disposition': 'attachment; filename="Process-Library.escadalib"'
      },
      body: 'escadalib-e2e'
    });
  });

  return {
    get changeVersion() { return changeVersion; },
    get dirty() { return dirty; },
    get incorporateRequests() { return incorporateRequests; },
    get exportRequest() { return exportRequest; }
  };
}

test('library association stays non-mutating, selective use advances Working, and disassociation keeps project-owned provenance', async ({ page }) => {
  const contract = await installUnlockedLibraryContract(page);

  await page.goto('/engineering/libraries');
  await expect(page.getByTestId('reusable-library-workspace')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Bibliotecas reutilizáveis' })).toBeVisible();
  await expect(page.getByTestId('reusable-library-boundary')).toContainText('Associar não é importar');
  await expect(page.getByTestId('reusable-library-workspace')).toContainText('Working: v4');

  await page.getByTestId('reusable-library-associate-input').setInputFiles({
    name: 'process.escadalib',
    mimeType: 'application/vnd.elitescada.resource-library',
    buffer: Buffer.from('catalog-only-e2e')
  });

  await expect(page.getByRole('status')).toContainText('Biblioteca associada sem alterar o Working.');
  await expect(page.getByTestId('reusable-library-workspace')).toContainText('Working: v4');
  expect(contract.changeVersion).toBe(4);
  expect(contract.dirty).toBe(false);

  const operator = page.getByTestId('reusable-library-resource').filter({ hasText: 'Operator Overview' });
  await expect(operator).toContainText('Dependências: 1');
  await expect(operator).toContainText(dynamoId);
  await operator.getByTestId('reusable-library-use').click();

  await expect(page.getByTestId('reusable-library-workspace')).toContainText('Working: v5');
  await expect(page.getByText('Alterações não salvas')).toBeVisible();
  expect(contract.incorporateRequests).toBe(1);
  expect(contract.changeVersion).toBe(5);
  expect(contract.dirty).toBe(true);

  const provenance = page.getByTestId('reusable-library-provenance');
  await expect(provenance).toContainText('Operator Overview');
  await expect(provenance).toContainText(libraryId);
  await expect(provenance).toContainText('origem apenas informativa');

  const exportPanel = page.getByTestId('reusable-library-export');
  const screenExport = exportPanel.locator('label').filter({ hasText: 'Operator Overview' }).getByRole('checkbox');
  await screenExport.check();
  const downloadPromise = page.waitForEvent('download');
  await exportPanel.getByRole('button', { name: 'Exportar .escadalib' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('Process-Library.escadalib');
  expect(contract.exportRequest).toMatchObject({
    name: 'EliteSCADA Library',
    version: '1.0.0',
    resources: [{ kind: 'screen', resourceId: screenId }]
  });

  await page.getByTestId('reusable-library-disassociate').click();
  await expect(page.getByRole('status')).toContainText('Biblioteca desassociada.');
  await expect(page.getByText('Nenhuma biblioteca reutilizável está associada.')).toBeVisible();
  await expect(provenance).toContainText('Operator Overview');
  await expect(page.getByTestId('reusable-library-workspace')).toContainText('Working: v5');
});

test('direct Libraries route stays hidden and never calls library APIs while Engineering Lock is active', async ({ page }) => {
  let libraryRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({
    json: { configured: true, locked: true }
  }));
  await page.route('**/api/engineering/libraries**', route => {
    libraryRequests++;
    return route.fulfill({ status: 403, json: { error: 'Engineering is locked.' } });
  });

  await page.goto('/engineering/libraries');

  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Engineering bloqueado' })).toBeVisible();
  await expect(page.getByTestId('reusable-library-workspace')).toHaveCount(0);
  expect(libraryRequests).toBe(0);
});
