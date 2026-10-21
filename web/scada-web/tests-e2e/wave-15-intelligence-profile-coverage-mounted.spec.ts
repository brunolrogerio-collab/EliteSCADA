import { expect, test, type Page } from '@playwright/test';

const equipmentId = '15000000-0000-0000-0000-000000000301';
const profileId = '15000000-0000-0000-0000-000000000201';
const assignmentId = '15000000-0000-0000-0000-000000000203';
const currentTagId = '15000000-0000-0000-0000-000000000101';
const speedTagId = '15000000-0000-0000-0000-000000000102';

test('profile coverage runs only on explicit request and renders canonical quality, alignment and provenance', async ({ page }) => {
  const fixture = await installWorkspace(page);
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await expect(panel).toBeVisible();
  expect(fixture.requests).toEqual([]);

  await panel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
  expect(fixture.requests).toEqual([]);
  const from = panel.getByLabel('De (horário local)');
  const to = panel.getByLabel('Até (horário local)');
  await from.fill('2026-10-20T10:00');
  await to.fill('2026-10-21T11:00');
  expect(fixture.requests).toEqual([]);

  await panel.getByRole('button', { name: 'Analisar cobertura' }).click();
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toBeVisible();
  expect(fixture.requests).toHaveLength(1);
  expect(fixture.requests[0]).toMatchObject({ scopeKind: 0, scopeId: equipmentId, profileId, bucketSeconds: 900 });
  expect(Date.parse(fixture.requests[0].window.fromUtc)).toBeLessThan(Date.parse(fixture.requests[0].window.toUtc));
  expect(Date.parse(fixture.requests[0].window.toUtc) - Date.parse(fixture.requests[0].window.fromUtc)).toBe(25 * 60 * 60 * 1000);
  await expect(panel.getByText(/Sobreposição de timestamps exatos do Historian observada/)).toBeVisible();
  await expect(panel.getByRole('heading', { name: 'Corrente · obrigatório' })).toBeVisible();
  const qualityCounts = panel.getByText('Good: 2 · Uncertain: 1');
  await expect(qualityCounts).toHaveCount(2);
  await expect(qualityCounts.first()).toBeVisible();
  await expect(panel.getByText('não comprova sincronização da aquisição de origem')).toBeVisible();
  await expect(panel.getByText('unsupported_without_a_canonical_semantic_and_unit-dimension-contract')).toBeVisible();
  await expect.poll(() => fixture.dataReads).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);

  await from.fill('2026-10-20T09:00');
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  expect(fixture.requests).toHaveLength(1);
});

for (const scenario of [
  { name: 'without Intelligence entitlement', options: { licensed: false }, message: 'É necessária a licença' },
  { name: 'without TrendUse', options: { trendUse: false }, message: 'É necessário TrendUse' },
  { name: 'with an inactive scope', options: { active: false }, message: 'Ative este escopo' },
  { name: 'without an armed permit', options: { armed: false }, message: 'não está armada' }
]) {
  test(`profile coverage fails closed ${scenario.name}`, async ({ page }) => {
    const fixture = await installWorkspace(page, scenario.options);
    await openIntelligence(page);
    const panel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
    await panel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
    await expect(panel.getByText(scenario.message, { exact: false })).toBeVisible();
    await expect(panel.getByRole('button', { name: 'Analisar cobertura' })).toBeDisabled();
    expect(fixture.requests).toEqual([]);
  });
}

test('profile coverage preserves unsupported and insufficient signal states without filling unknown values', async ({ page }) => {
  const fixture = await installWorkspace(page, { insufficient: true });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await panel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
  await panel.getByRole('button', { name: 'Analisar cobertura' }).click();
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toBeVisible();
  await expect(panel.getByText('Dados de sinais obrigatórios insuficientes')).toBeVisible();
  await expect(panel.getByText('Tipo de TAG não numérico não suportado')).toBeVisible();
  await expect(panel.getByText('Sem amostras numéricas Good')).toBeVisible();
  await expect(panel.getByText('desconhecido', { exact: true })).toHaveCount(4);
  expect(fixture.requests).toHaveLength(1);
});

test('profile coverage is disabled for local unsaved profile edits and unpublished Working changes', async ({ page }) => {
  const localFixture = await installWorkspace(page);
  await openIntelligence(page);
  const workspace = page.getByTestId('asset-process-intelligence-workspace');
  await workspace.getByLabel('Nome de exibição').first().fill('Perfil ainda não salvo');
  const localPanel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await localPanel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
  await expect(localPanel.getByText('Salve ou descarte as alterações locais do perfil', { exact: false })).toBeVisible();
  await expect(localPanel.getByRole('button', { name: 'Analisar cobertura' })).toBeDisabled();
  expect(localFixture.requests).toEqual([]);

  const dirtyPage = await page.context().newPage();
  const dirtyFixture = await installWorkspace(dirtyPage, { workspaceDirty: true });
  await openIntelligence(dirtyPage);
  const dirtyPanel = dirtyPage.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await dirtyPanel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
  await expect(dirtyPanel.getByText('Publique ou descarte alterações de Engineering Working', { exact: false })).toBeVisible();
  await expect(dirtyPanel.getByRole('button', { name: 'Analisar cobertura' })).toBeDisabled();
  expect(dirtyFixture.requests).toEqual([]);
});

test('profile coverage rejects invalid window locally and presents bounded-query errors without stale results', async ({ page }) => {
  const fixture = await installWorkspace(page, { failCode: 'INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE' });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-profile-coverage-${profileId}`);
  await panel.getByRole('button', { name: 'Revisar cobertura histórica' }).click();
  await panel.getByLabel('De (horário local)').fill('2026-10-21T12:00');
  await panel.getByLabel('Até (horário local)').fill('2026-10-21T11:00');
  await expect(panel.getByRole('button', { name: 'Analisar cobertura' })).toBeDisabled();
  await expect(panel.getByRole('alert')).toContainText('O fim deve ser posterior');
  expect(fixture.requests).toEqual([]);

  await panel.getByLabel('De (horário local)').fill('2026-10-20T10:00');
  await panel.getByLabel('Até (horário local)').fill('2026-10-21T11:00');
  await panel.getByRole('button', { name: 'Analisar cobertura' }).click();
  await expect(panel.getByRole('alert')).toContainText('INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE');
  expect(fixture.requests).toHaveLength(1);
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
});

async function openIntelligence(page: Page) {
  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /Inteligência de ativos|Asset intelligence|Inteligencia de activos/ }).click();
}

async function installWorkspace(page: Page, options: {
  licensed?: boolean;
  trendUse?: boolean;
  active?: boolean;
  armed?: boolean;
  failCode?: string;
  insufficient?: boolean;
  workspaceDirty?: boolean;
} = {}) {
  const requests: Array<Record<string, any>> = [];
  const dataReads: string[] = [];
  const otherWrites: string[] = [];
  const profile = {
    id: profileId,
    key: 'pump-drive',
    name: 'Perfil da bomba',
    signals: [
      { id: '15000000-0000-0000-0000-000000000211', key: 'Corrente', signalRole: 'eléctrico', tagId: currentTagId, required: true },
      { id: '15000000-0000-0000-0000-000000000212', key: 'Velocidade', signalRole: 'mecânico', tagId: speedTagId, required: true }
    ]
  };
  const tags = [
    { id: currentTagId, name: 'Corrente', path: 'Planta.Bomba01.Corrente', dataType: 'Double', engineeringUnit: 'A', readOnly: true },
    { id: speedTagId, name: 'Velocidade', path: 'Planta.Bomba01.Velocidade', dataType: 'Double', engineeringUnit: 'rpm', readOnly: true }
  ];
  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (path === '/api/engineering/asset-process-intelligence/analytics/profile-coverage') {
      const body = request.postDataJSON() as Record<string, any>;
      requests.push(body);
      if (options.failCode) return route.fulfill({ status: 413, json: { code: options.failCode, error: 'window too dense' } });
      return route.fulfill({ json: coverageResponse(body, options.insufficient === true) });
    }
    if (path === '/api/tags' || path === '/api/historical/data-query') dataReads.push(`${request.method()} ${path}`);
    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) otherWrites.push(`${request.method()} ${path}`);
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false,
      runtime: options.trendUse === false ? ['View'] : ['View', 'TrendUse'],
      workspace: ['EngineeringView', 'EngineeringModify', 'AssetProcessIntelligenceManage']
    } });
    if (path === '/api/auth/config') return route.fulfill({ json: {
      authenticationEnabled: false, localLoginEnabled: false,
      initialAdministratorRequired: false, initialAdministratorSetupAvailable: false
    } });
    if (path === '/api/product/info') return route.fulfill({ json: {
      productName: 'EliteSCADA', channel: 'Test', version: '0.0.0', displayVersion: 'EliteSCADA Test',
      informationalVersion: 'EliteSCADA Test+coverage-test', buildCommit: 'coverage-test'
    } });
    if (path === '/api/engineering/lock/status') return route.fulfill({ json: { configured: false, locked: false } });
    if (path === '/api/engineering/workspace') return route.fulfill({ json: {
      projectKey: 'intelligence-coverage-test', projectName: 'Intelligence coverage test', baseRevision: 4,
      isDirty: false, changeVersion: 4, tagCount: 2, alarmCount: 0, dataSourceCount: 0,
      templateCount: 0, equipmentCount: 1, dynamoCount: 0, screenCount: 0, popupCount: 0
    } });
    if (path === '/api/engineering/export/json') return route.fulfill({ json: {
      schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-10-21T00:00:00Z',
      tags, alarms: [], dataSources: [], templates: [],
      equipment: [{ id: equipmentId, key: 'pump-01', name: 'Bomba P-101', path: 'Planta.Bomba01' }],
      dynamos: [], screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
    } });
    if (path === '/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog') return route.fulfill({ json: {
      supportedSemanticContractVersions: [1, 2],
      semanticCatalogs: [{ catalogVersion: 1, roles: [] }, { catalogVersion: 2, roles: [] }],
      controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: [] }]
    } });
    if (path === '/api/engineering/asset-process-intelligence/scopes') return route.fulfill({ json: {
      configuration: {
        version: 2,
        scopes: [{ kind: 'Equipment', scopeId: equipmentId, active: options.active !== false, executionArmed: options.armed !== false }],
        profiles: [profile], assignments: [{ id: assignmentId, equipmentId, profileId }], processStages: [], operatingContextProfiles: []
      },
      resources: [{ kind: 'Equipment', scopeId: equipmentId, path: 'Planta.Bomba01', name: 'Bomba P-101' }],
      tags, licensed: options.licensed !== false,
      execution: { stage: 'Stage3B', workerRegistered: true, queueDepth: 0, activePermitCount: 1,
        projectKey: 'intelligence-coverage-test', activeRevision: 4, workspaceDirty: options.workspaceDirty === true }
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: {} });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  page.on('websocket', socket => {
    if (new URL(socket.url()).pathname === '/ws/tags') dataReads.push('WS /ws/tags');
  });
  return { requests, dataReads, otherWrites };
}

function coverageResponse(body: Record<string, any>, insufficient = false) {
  const summary = {
    rawObservationCount: 3, acceptedSampleCount: 2, rejectedSampleCount: 1,
    qualityCounts: { Good: 2, Uncertain: 1 }, validSampleCount: 2,
    minimum: 10, maximum: 20, sampleMean: 15, firstObserved: null, lastObserved: null,
    delta: 10, trend: [], completeness: 'unknown', expectedSampleCount: null
  };
  const response = {
    contractVersion: 1, ruleCatalogVersion: 1, ruleVersion: '1.0.0', projectKey: 'intelligence-coverage-test', activeRevision: 4,
    scopeKind: 'Equipment', scopeId: body.scopeId, profileId: body.profileId, profileKey: 'pump-drive', profileName: 'Perfil da bomba',
    window: body.window,
    signals: [
      { signalId: '15000000-0000-0000-0000-000000000211', signalKey: 'Corrente', signalRoleMetadata: 'eléctrico', required: true,
        tagId: currentTagId, tagPath: 'Planta.Bomba01.Corrente', tagDataType: 'Double', unit: 'A', status: 'descriptive_trend_available', summary },
      { signalId: '15000000-0000-0000-0000-000000000212', signalKey: 'Velocidade', signalRoleMetadata: 'mecânico', required: true,
        tagId: speedTagId, tagPath: 'Planta.Bomba01.Velocidade', tagDataType: 'Double', unit: 'rpm', status: 'descriptive_trend_available', summary }
    ],
    temporalAlignment: {
      requiredSignalCount: 2, requiredSignalsWithGoodSamples: 2, exactHistorianTimestampIntersectionCount: 2,
      status: 'exact_historian_timestamp_overlap_observed',
      timestampBasis: 'distinct exact Historian event timestamps; not source acquisition time',
      sourceAcquisitionAlignment: 'unverified', expectedCadence: 'unknown', completeness: 'unknown'
    },
    provenance: {
      dataset: 'historian.samples', datasetContractVersion: 1, ruleVersion: '1.0.0',
      signalRoleInterpretation: 'metadata only; SignalRole is not interpreted', unitConversion: 'none; each descriptive trend retains the canonical TAG unit',
      processKpiDisposition: 'unsupported_without_a_canonical_semantic_and_unit-dimension-contract'
    }
  };
  if (insufficient) {
    response.temporalAlignment = {
      requiredSignalCount: 2, requiredSignalsWithGoodSamples: 0, exactHistorianTimestampIntersectionCount: 0,
      status: 'insufficient_required_signal_data', timestampBasis: 'distinct exact Historian event timestamps; not source acquisition time',
      sourceAcquisitionAlignment: 'unverified', expectedCadence: 'unknown', completeness: 'unknown'
    };
    response.signals = [
      { ...response.signals[0], tagDataType: 'Boolean', unit: null, status: 'unsupported_non_numeric_tag_type', summary: null },
      { ...response.signals[1], status: 'insufficient_no_good_numeric_samples', summary: {
        ...summary, rawObservationCount: 1, acceptedSampleCount: 0, rejectedSampleCount: 1,
        qualityCounts: { BadCommunication: 1 }, validSampleCount: 0, minimum: null, maximum: null,
        sampleMean: null, completeness: 'unknown', expectedSampleCount: null
      } }
    ];
  }
  return response;
}
