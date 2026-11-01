import { expect, test } from '@playwright/test';

const equipmentId = '15000000-0000-0000-0000-000000000301';
const profileId = '15000000-0000-0000-0000-000000000201';
const assignmentId = '15000000-0000-0000-0000-000000000203';
const tagId = '15000000-0000-0000-0000-000000000101';
const stageId = '15000000-0000-0000-0000-000000000204';

test('Intelligence opens a generated screen in the canonical editor without persisting the draft', async ({ page }) => {
  test.setTimeout(90_000);
  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  const writes: string[] = [];
  const dataReads: string[] = [];
  const coverageQueries: string[] = [];
  let intelligenceLicensed = false;
  const profile = {
    id: profileId,
    key: 'pump-process',
    name: 'Pump process profile',
    signals: [{ id: '15000000-0000-0000-0000-000000000202', key: 'flow', signalRole: 'process', tagId, required: true }]
  };
  const tag = {
    id: tagId,
    name: 'Discharge flow',
    path: 'Plant.Pump01.DischargeFlow',
    dataType: 'Double',
    engineeringUnit: 'm³/h',
    readOnly: true
  };

  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) writes.push(`${request.method()} ${path}`);
    if (path === '/api/tags' || path === '/api/historical/data-query') dataReads.push(`${request.method()} ${path}`);
    if (path === '/api/engineering/asset-process-intelligence/analytics/profile-coverage') coverageQueries.push(`${request.method()} ${path}`);

    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false,
      runtime: ['SystemAdmin'],
      workspace: ['EngineeringView', 'EngineeringModify', 'AssetProcessIntelligenceManage']
    } });
    if (path === '/api/auth/config') return route.fulfill({ json: {
      authenticationEnabled: false, localLoginEnabled: false,
      initialAdministratorRequired: false, initialAdministratorSetupAvailable: false
    } });
    if (path === '/api/product/info') return route.fulfill({ json: {
      productName: 'EliteSCADA', channel: 'Test', version: '0.0.0', displayVersion: 'EliteSCADA Test',
      informationalVersion: 'EliteSCADA Test+dashboard-test', buildCommit: 'dashboard-test'
    } });
    if (path === '/api/engineering/lock/status') return route.fulfill({ json: { configured: false, locked: false } });
    if (path === '/api/engineering/workspace') return route.fulfill({ json: {
      projectKey: 'intelligence-dashboard-test', projectName: 'Intelligence dashboard test', baseRevision: 1,
      isDirty: false, changeVersion: 1, tagCount: 1, alarmCount: 0, dataSourceCount: 0,
      templateCount: 0, equipmentCount: 1, dynamoCount: 0, screenCount: 0, popupCount: 0
    } });
    if (path === '/api/engineering/export/json') return route.fulfill({ json: {
      schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-10-21T00:00:00Z',
      tags: [tag], alarms: [], dataSources: [], templates: [],
      equipment: [{ id: equipmentId, key: 'pump-01', name: 'Pump P-101', path: 'Plant.Pump01' }],
      dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
    } });
    if (path === '/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog') return route.fulfill({ json: {
      supportedSemanticContractVersions: [1, 2],
      semanticCatalogs: [{ catalogVersion: 1, roles: [] }, { catalogVersion: 2, roles: [] }],
      controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: [] }]
    } });
    if (path === '/api/engineering/asset-process-intelligence/scopes') return route.fulfill({ json: {
      configuration: {
        version: 2, scopes: [], profiles: [profile],
        assignments: [{ id: assignmentId, equipmentId, profileId }],
        processStages: [{ id: stageId, key: 'pump-train', name: 'Pumping train', order: 0, links: [{ assignmentId, order: 0 }] }],
        operatingContextProfiles: []
      },
      resources: [{ kind: 'Equipment', scopeId: equipmentId, path: 'Plant.Pump01', name: 'Pump P-101' }],
      tags: [tag], licensed: intelligenceLicensed,
      execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0, activePermitCount: 0,
        projectKey: 'intelligence-dashboard-test', activeRevision: 1, workspaceDirty: false }
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: {} });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  page.on('websocket', socket => {
    if (new URL(socket.url()).pathname === '/ws/tags') dataReads.push('WS /ws/tags');
  });

  const openIntelligence = async () => {
    await page.goto('/engineering');
    await expect(page.getByTestId('engineering-lock-management')).toBeVisible();
    const intelligenceNavigation = page.locator('.eng-nav').getByRole('button', { name: /Inteligência de ativos|Asset intelligence|Inteligencia de activos/ });
    await expect(intelligenceNavigation).toBeEnabled();
    const scopesResponse = page.waitForResponse(response => {
      const url = new URL(response.url());
      return url.pathname === '/api/engineering/asset-process-intelligence/scopes' && response.request().method() === 'GET';
    });
    await intelligenceNavigation.click();
    expect((await scopesResponse).status()).toBe(200);
    await expect(page.getByTestId('intelligence-stage2-authoring')).toBeVisible();
  };

  await openIntelligence();
  const intelligence = page.getByTestId('intelligence-stage2-authoring');
  const generate = intelligence.getByTestId(`intelligence-dashboard-assignment-${assignmentId}`);
  await expect(generate).toBeDisabled();

  intelligenceLicensed = true;
  await openIntelligence();
  const licensedIntelligence = page.getByTestId('intelligence-stage2-authoring');
  const licensedGenerate = licensedIntelligence.getByTestId(`intelligence-dashboard-assignment-${assignmentId}`);
  await expect(licensedGenerate).toBeEnabled();
  await licensedGenerate.click();

  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const editor = page.getByTestId('visual-editor-workspace');
  await expect(editor).toBeVisible();
  await expect(editor.getByTestId('visual-editor-generated-drafts').getByRole('button')).toHaveCount(1);
  await expect(editor.getByLabel('Nome de exibição')).toHaveValue(/Inteligência · Pump P-101 · Pump process profile/);
  await expect(editor.locator('.visual-editor-header-status')).toContainText('7 objetos');
  await expect(editor.getByTestId('visual-editor-canonical-layer')).toBeVisible();
  await expect(editor.getByTestId('runtime-intelligence-profile-coverage')).toBeVisible();
  await expect(editor.getByTestId('visual-editor-preview')).toBeEnabled();
  await expect(editor.getByTestId('visual-editor-apply')).toBeDisabled();
  expect(coverageQueries).toEqual([]);

  await openIntelligence();
  const coveragePanel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await coveragePanel.getByRole('button', { name: /Revisar cobertura histórica|Review historical coverage|Revisar cobertura histórica/ }).click();
  await coveragePanel.getByTestId(`intelligence-coverage-create-screen-${profileId}`).click();
  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const coverageEditor = page.getByTestId('visual-editor-workspace');
  await expect(coverageEditor).toBeVisible();
  await expect(coverageEditor.getByLabel('Nome de exibição')).toHaveValue(/Cobertura histórica · Pump P-101 · Pump process profile/);
  await expect(coverageEditor.getByTestId('visual-editor-canonical-layer')).toBeVisible();
  await expect(coverageEditor.getByTestId('visual-editor-apply')).toBeDisabled();

  await openIntelligence();
  const stageCoverage = page.getByTestId(`intelligence-stage-coverage-${stageId}`);
  await expect(stageCoverage).toBeEnabled();
  await stageCoverage.click();
  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const stageCoverageEditor = page.getByTestId('visual-editor-workspace');
  await expect(stageCoverageEditor).toBeVisible();
  await expect(stageCoverageEditor.getByLabel('Nome de exibição')).toHaveValue(/Cobertura da etapa · Pumping train/);
  await expect(stageCoverageEditor.getByTestId('visual-editor-canonical-layer')).toBeVisible();
  await expect(stageCoverageEditor.getByTestId('visual-editor-apply')).toBeDisabled();

  await openIntelligence();
  const guidedStageDashboard = page.getByTestId(`intelligence-guided-stage-dashboard-${stageId}`);
  await expect(guidedStageDashboard).toBeEnabled();
  await expect(page.getByTestId(`intelligence-process-stage-insights-${stageId}`)).toBeDisabled();
  await guidedStageDashboard.click();
  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const guidedStageEditor = page.getByTestId('visual-editor-workspace');
  await expect(guidedStageEditor).toBeVisible();
  await expect(guidedStageEditor.getByTestId('visual-editor-generated-drafts').getByRole('button')).toHaveCount(2);
  await expect(guidedStageEditor.getByLabel('Nome de exibição')).toHaveValue(/Pumping train/);
  await expect(guidedStageEditor.getByTestId('visual-editor-canonical-layer')).toBeVisible();
  await expect(guidedStageEditor.getByTestId('visual-editor-apply')).toBeDisabled();
  expect(coverageQueries).toEqual([]);
  expect(writes).toEqual([]);
  expect(dataReads).toEqual([]);
});
