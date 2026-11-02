import { expect, test } from '@playwright/test';

const projectKey = 'intelligence-process-stage-insights-runtime-test';
const revision = 9;
const stageId = '10000000-0000-0000-0000-000000000001';
const assignmentOne = '20000000-0000-0000-0000-000000000002';
const assignmentTwo = '30000000-0000-0000-0000-000000000003';
const scopeOne = '40000000-0000-0000-0000-000000000004';
const scopeTwo = '50000000-0000-0000-0000-000000000005';
const profileOne = '60000000-0000-0000-0000-000000000006';
const profileTwo = '70000000-0000-0000-0000-000000000007';
const signalIdsOne = [
  '80000000-0000-0000-0000-000000000008',
  '81000000-0000-0000-0000-000000000008'
];
const signalIdsTwo = [
  '82000000-0000-0000-0000-000000000008',
  '83000000-0000-0000-0000-000000000008'
];
const ruleId = 'specific-active-energy-at-recorded-production-rate.v1';
const ruleVersion = '1.0.0';
const catalogPath = '/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog';
const queryPath = '/api/engineering/asset-process-intelligence/analytics/performance/v1/query';

test('process-stage member insights only query explicitly and clear/abort the old member report', async ({ page }) => {
  const catalogRequests: string[] = [];
  const deviationRequests: string[] = [];
  const queries: Array<Record<string, any>> = [];
  let releasePending!: () => void;
  const pendingRelease = new Promise<void>(resolve => { releasePending = resolve; });
  let pendingQueryFinished = false;

  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.addInitScript(() => {
    const trackedWindow = window as typeof window & { __intelligenceAbortedFetches?: string[] };
    trackedWindow.__intelligenceAbortedFetches = [];
    const originalFetch = window.fetch.bind(window);
    window.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
      const path = new URL(url, window.location.href).pathname;
      if (path === '/api/engineering/asset-process-intelligence/analytics/performance/v1/query' && init?.signal) {
        init.signal.addEventListener('abort', () => trackedWindow.__intelligenceAbortedFetches?.push(path), { once: true });
      }
      return originalFetch(input, init);
    }) as typeof window.fetch;
  });
  await page.routeWebSocket('**/ws/tags', () => {});
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/auth/config') return route.fulfill({ json: {
      authenticationEnabled: false, localLoginEnabled: false,
      initialAdministratorRequired: false, initialAdministratorSetupAvailable: false,
      passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
    } });
    if (path === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, hasProjects: true } });
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: []
    } });
    if (path === '/api/product/info') return route.fulfill({ json: {
      productName: 'EliteSCADA', channel: 'Test', version: '0.0.0', displayVersion: 'EliteSCADA Test',
      informationalVersion: 'EliteSCADA Test+process-stage-insights-runtime-test', buildCommit: 'process-stage-insights-runtime-test'
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection() });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === catalogPath) {
      catalogRequests.push(`${request.method()} ${path}`);
      return route.fulfill({ json: performanceCatalog() });
    }
    if (path === queryPath) {
      const body = request.postDataJSON() as Record<string, any>;
      queries.push(body);
      if (queries.length === 2) {
        await pendingRelease;
        try {
          await route.fulfill({ json: performanceResponse(body) });
        } catch {
          // A canceled browser request may no longer accept the held response.
        } finally {
          pendingQueryFinished = true;
        }
        return;
      }
      return route.fulfill({ json: performanceResponse(body) });
    }
    if (path.includes('/analytics/contextual-deviation-vector/v2')) {
      deviationRequests.push(`${request.method()} ${path}`);
      return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
    }
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  const visual = page.getByTestId('runtime-intelligence-process-stage-insights');
  const member = visual.getByTestId('runtime-intelligence-process-stage-insights-member');
  await expect(visual).toBeVisible();
  await expect(member).toHaveValue(assignmentOne);
  await expect(visual.getByText(/Choose a report to set its time window/i)).toBeVisible();
  expect(catalogRequests).toEqual([]);
  expect(queries).toEqual([]);

  await visual.getByTestId('runtime-intelligence-process-stage-insights-performance').click();
  let performance = visual.getByTestId('runtime-intelligence-performance');
  await performance.getByLabel('From (local time)').fill('2026-10-20T10:00');
  await performance.getByLabel('To (local time)').fill('2026-10-21T11:00');
  expect(catalogRequests).toEqual([]);
  expect(queries).toEqual([]);
  await performance.getByRole('button', { name: 'Analyze' }).click();
  await expect(performance.getByTestId('intelligence-performance-result')).toBeVisible();
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({
    scopeKind: 0, scopeId: scopeOne, assignmentId: assignmentOne, profileId: profileOne,
    ruleId, signalIds: signalIdsOne
  });

  await member.selectOption(assignmentTwo);
  await expect(visual.getByTestId('runtime-intelligence-performance')).toHaveCount(0);
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeVisible();
  await visual.getByTestId('runtime-intelligence-process-stage-insights-deviation').click();
  const deviation = visual.getByTestId('runtime-intelligence-contextual-deviation-v2');
  await expect(deviation).toBeVisible();
  await expect(deviation.getByLabel('Reference from (local time)')).toBeVisible();
  expect(deviationRequests).toEqual([]);
  expect(queries).toHaveLength(1);
  await visual.getByTestId('runtime-intelligence-process-stage-insights-performance').click();
  performance = visual.getByTestId('runtime-intelligence-performance');
  await expect(performance.getByTestId('intelligence-performance-result')).toHaveCount(0);
  await performance.getByLabel('From (local time)').fill('2026-10-20T10:00');
  await performance.getByLabel('To (local time)').fill('2026-10-21T11:00');
  await performance.getByRole('button', { name: 'Analyze' }).click();
  await expect.poll(() => queries.length).toBe(2);
  expect(queries[1]).toMatchObject({ scopeKind: 0, scopeId: scopeTwo, assignmentId: assignmentTwo, profileId: profileTwo, signalIds: signalIdsTwo });

  await member.selectOption(assignmentOne);
  await expect(visual.getByTestId('runtime-intelligence-performance')).toHaveCount(0);
  const abortedPaths = await page.evaluate(() =>
    (window as typeof window & { __intelligenceAbortedFetches?: string[] }).__intelligenceAbortedFetches ?? []
  );
  expect(abortedPaths).toContain(queryPath);
  releasePending();
  await expect.poll(() => pendingQueryFinished).toBe(true);
  expect(catalogRequests).toHaveLength(2);
  await visual.getByTestId('runtime-intelligence-process-stage-insights-performance').click();
  await expect(visual.getByTestId('runtime-intelligence-performance').getByTestId('intelligence-performance-result')).toHaveCount(0);
  await visual.getByTestId('runtime-intelligence-process-stage-insights-deviation').click();
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-v2').getByLabel('Reference from (local time)')).toBeVisible();
  expect(deviationRequests).toEqual([]);
  await expect(visual.getByText(/Each report belongs only to the selected Equipment assignment/i)).toBeVisible();
});

test('stage availability v2 keeps usable reports independent and clears results for unavailable members', async ({ page }) => {
  const fullOne = memberConfig(assignmentOne, scopeOne, profileOne, 'Pump one', 0, signalIdsOne);
  const fullTwo = memberConfig(assignmentTwo, scopeTwo, profileTwo, 'Pump two', 1, signalIdsTwo);
  const thirdId = '31000000-0000-0000-0000-000000000003';
  const members = [
    { ...fullOne, availabilityVersion: 2, contextualDeviation: undefined },
    { ...fullTwo, availabilityVersion: 2, performance: undefined },
    { ...fullTwo, availabilityVersion: 2, assignmentId: thirdId, profileId: '71000000-0000-0000-0000-000000000007', order: 2, performance: undefined, contextualDeviation: undefined }
  ];
  const queries = await installStageFixture(page, members);
  await page.goto('/');
  const visual = page.getByTestId('runtime-intelligence-process-stage-insights');
  const member = visual.getByTestId('runtime-intelligence-process-stage-insights-member');
  await expect(visual).toHaveCSS('align-items', 'stretch');
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeEnabled();
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeDisabled();
  await visual.getByTestId('runtime-intelligence-process-stage-insights-performance').click();
  const performance = visual.getByTestId('runtime-intelligence-performance');
  await performance.getByLabel('From (local time)').fill('2026-10-20T10:00');
  await performance.getByLabel('To (local time)').fill('2026-10-21T11:00');
  expect(queries).toEqual([]);
  await performance.getByRole('button', { name: 'Analyze' }).click();
  await expect(performance.getByTestId('intelligence-performance-result')).toBeVisible();
  await member.selectOption(assignmentTwo);
  await expect(visual.getByTestId('runtime-intelligence-performance')).toHaveCount(0);
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeDisabled();
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeEnabled();
  await visual.getByTestId('runtime-intelligence-process-stage-insights-deviation').click();
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-v2')).toBeVisible();
  await member.selectOption(thirdId);
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-v2')).toHaveCount(0);
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeDisabled();
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeDisabled();
  await expect(visual.getByText(/Performance is not configured/)).toBeVisible();
  await expect(visual.getByText(/Contextual deviation is not configured/)).toBeVisible();
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({ assignmentId: assignmentOne, scopeId: scopeOne, profileId: profileOne });
});

for (const invalid of ['legacy-missing-report', 'unknown-version', 'invalid-present-report', 'invalid-range', 'false-reference', 'null-reference', 'legacy-reference'] as const) {
  test(`stage member configuration rejects ${invalid} without analytic reads`, async ({ page }) => {
    const member: any = memberConfig(assignmentOne, scopeOne, profileOne, 'Pump one', 0, signalIdsOne);
    if (invalid === 'legacy-missing-report') delete member.performance;
    if (invalid === 'unknown-version') member.availabilityVersion = 3;
    if (invalid === 'invalid-present-report') { member.availabilityVersion = 2; member.performance = null; }
    if (invalid === 'false-reference') member.referenceEvidence = false;
    if (invalid === 'null-reference') member.referenceEvidence = null;
    if (invalid === 'legacy-reference') { delete member.availabilityVersion; member.referenceEvidence = true; }
    if (invalid === 'invalid-range') { member.availabilityVersion = 2; member.contextualDeviation.firstContextUpperExclusiveCanonical = 0; }
    const queries = await installStageFixture(page, [member]);
    await page.goto('/');
    const visual = page.getByTestId('runtime-intelligence-process-stage-insights');
    await expect(visual.getByRole('alert')).toContainText(/invalid or incomplete/);
    await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-member')).toHaveCount(0);
    expect(queries).toEqual([]);
  });
}

async function installStageFixture(page: import('@playwright/test').Page, members: unknown[]) {
  const queries: Array<Record<string, any>> = [];
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.routeWebSocket('**/ws/tags', () => {});
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/config') return route.fulfill({ json: { authenticationEnabled: false, localLoginEnabled: false, initialAdministratorRequired: false } });
    if (path === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, hasProjects: true } });
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: []
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection(members) });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === catalogPath) return route.fulfill({ json: performanceCatalog() });
    if (path === queryPath) {
      const body = route.request().postDataJSON(); queries.push(body);
      return route.fulfill({ json: performanceResponse(body) });
    }
    if (path.includes('/analytics/')) queries.push({ unexpected: path });
    return route.fulfill({ status: 404, json: {} });
  });
  return queries;
}

function runtimeProjection(membersOverride?: unknown[]) {
  const members = membersOverride ?? [
    memberConfig(assignmentOne, scopeOne, profileOne, 'Pump P-101 · Pump profile', 0, signalIdsOne),
    memberConfig(assignmentTwo, scopeTwo, profileTwo, 'Pump P-102 · Pump profile', 1, signalIdsTwo)
  ];
  return {
    mode: 'engineering', projectKey, projectName: 'Process-stage insights Runtime Test', revision,
    activatedAtUtc: '2026-10-21T00:00:00Z',
    package: {
      schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-10-21T00:00:00Z',
      startupScreenId: '90000000-0000-0000-0000-000000000009',
      runtimePresentation: { historicalPlaybackEnabled: false },
      tags: [], alarms: [], templates: [], equipment: [], dynamos: [], scripts: [],
      scriptVisualEventReferences: [], visualAssets: [], popups: [],
      screens: [{
        id: '90000000-0000-0000-0000-000000000009', key: 'process-stage-insights',
        name: 'Pumping stage insights', elements: [{
          id: '90000000-0000-0000-0000-000000000010', key: 'process-stage-insights-v1',
          type: 'core.intelligenceProcessStageInsightsV1',
          properties: {
            x: 50, y: 60, width: 1250, height: 720,
            intelligenceStageId: stageId, intelligenceStageKey: 'pumping-stage',
            intelligenceStageName: 'Pumping stage', intelligenceStageMembers: JSON.stringify(members)
          }
        }]
      }]
    }
  };
}

function memberConfig(
  assignmentId: string, scopeId: string, profileId: string, displayLabel: string, order: number, signalIds: string[]
) {
  return {
    assignmentId, scopeId, profileId, displayLabel, order,
    performance: { ruleId, ruleVersion, signalIds },
    contextualDeviation: {
      targetSignalIds: signalIds,
      firstContextSignalId: order === 0 ? '84000000-0000-0000-0000-000000000008' : '85000000-0000-0000-0000-000000000008',
      firstContextLowerInclusiveCanonical: 5, firstContextUpperExclusiveCanonical: 20,
      secondContextSignalId: order === 0 ? '86000000-0000-0000-0000-000000000008' : '87000000-0000-0000-0000-000000000008',
      secondContextLowerInclusiveCanonical: 900, secondContextUpperExclusiveCanonical: 2000
    }
  };
}

function performanceCatalog() {
  return {
    contractVersion: 1, catalogVersion: 1, ruleVersion, dataset: 'historian.samples',
    datasetContractVersion: 2, maximumWindowDays: 31, maximumRawRowsAcrossSelectedTags: 50000,
    rules: [{
      id: ruleId, version: ruleVersion, kind: 'derived', formula: 'recorded power / recorded production',
      inputRoles: ['equipment.active_input_power', 'process.net_good_production_volume_rate'], inputUnits: [],
      outputUnit: 'kWh/m³', outputUnitVariants: [], temporalRequirement: 'Exact source timestamps.',
      minimumQualifiedTuples: 2, bounds: 'Power >= 0; production rate > 0.', unavailableStates: []
    }]
  };
}

function performanceResponse(body: Record<string, any>) {
  return {
    contractVersion: 1, catalogVersion: 1, ruleId, ruleVersion, projectKey, activeRevision: revision,
    scopeKind: 0, scopeId: body.scopeId, assignmentId: body.assignmentId,
    equipmentId: body.scopeId, profileId: body.profileId, window: body.window,
    signals: body.signalIds.map((signalId: string, index: number) => ({
      signalId, signalKey: index === 0 ? 'Active input power' : 'Net good production rate',
      roleCode: index === 0 ? 'equipment.active_input_power' : 'process.net_good_production_volume_rate',
      canonicalUnit: index === 0 ? 'W' : 'm³/s'
    })),
    inputs: body.signalIds.map((signalId: string) => ({
      signalId, rawObservationCount: 10, eligibleSynchronizedCount: 8,
      pairedCount: 4, qualifiedCount: 3, qualityCounts: { Good: 8 }, rejectionCounts: { Uncertain: 2 }
    })),
    totalRawObservationCount: 20,
    summary: { status: 'available', unit: 'kWh/m³', reasonCodes: [], qualifiedTupleCount: 3,
      minimum: 1.2, maximum: 1.8, arithmeticMean: 1.5, meanSemantics: 'arithmetic mean over qualified tuples' },
    provenance: { dataset: 'historian.samples', datasetContractVersion: 2,
      timestampPairing: 'exact_synchronized_source_timestamp', acquisitionAlignment: 'unverified',
      operatingRegimeEquivalence: 'unverified', expectedCadence: 'unknown', completeness: 'unknown',
      measurementInterpretation: 'recorded evidence only' }
  };
}

test('stage references query the selected member only and discard a cancelled response on member change', async ({ page }) => {
  const references = [
    memberConfig(assignmentOne, scopeOne, profileOne, 'Pump one', 0, signalIdsOne),
    memberConfig(assignmentTwo, scopeTwo, profileTwo, 'Pump two', 1, signalIdsTwo)
  ].map(({ performance: _performance, contextualDeviation: _deviation, ...member }) => ({ ...member, availabilityVersion: 2, referenceEvidence: true }));
  await installStageFixture(page, references);
  const queries: any[] = [];
  let started!: () => void; const pending = new Promise<void>(resolve => { started = resolve; });
  let release!: () => void; const delayed = new Promise<void>(resolve => { release = resolve; });
  let completed = false;
  await page.route('**/api/engineering/asset-process-intelligence/analytics/reference-evidence/v1/query', async route => {
    const body = route.request().postDataJSON(); queries.push(body);
    const response = { contractVersion: 1, projectKey, activeRevision: revision, scopeId: body.scopeId, profileId: body.profileId,
      referenceWindow: body.referenceWindow, referenceCohort: { completeRequiredNumericCohortCount: 120 },
      signals: [{ signalId: body.profileId === profileOne ? signalIdsOne[0] : signalIdsTwo[0], tagPath: 'Pump.Current', unit: 'A', required: true,
        reference: { rawObservationCount: 120, acceptedSynchronizedCount: 120, rejectedObservationCount: 0 } }] };
    if (queries.length === 2) {
      started(); await delayed;
      try { await route.fulfill({ json: response }); } catch { /* The old member request is deliberately aborted. */ }
      completed = true; return;
    }
    await route.fulfill({ json: response });
  });
  await page.goto('/');
  const visual = page.getByTestId('runtime-intelligence-process-stage-insights');
  const member = visual.getByTestId('runtime-intelligence-process-stage-insights-member');
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeDisabled();
  await expect(visual.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeDisabled();
  await visual.getByTestId('runtime-intelligence-process-stage-insights-reference').click();
  const report = visual.getByTestId('runtime-intelligence-reference-evidence');
  const from = report.getByLabel('Reference start (local time)', { exact: true });
  const to = report.getByLabel('Reference end (local time)', { exact: true });
  const query = report.getByRole('button', { name: 'Query reference', exact: true });
  await from.fill('2026-10-20T10:00'); await to.fill('2026-10-20T11:00');
  expect(queries).toEqual([]); await query.click();
  await expect(report.getByTestId('intelligence-reference-evidence-result')).toBeVisible();
  expect(queries[0]).toMatchObject({ scopeId: scopeOne, profileId: profileOne });
  await expect.poll(() => report.evaluate(element => {
    const container = element.closest('.runtime-intelligence-process-stage-insights')!;
    const footer = container.querySelector('.runtime-intelligence-process-stage-insights__limit')!;
    return footer.getBoundingClientRect().top >= element.getBoundingClientRect().bottom - 1;
  })).toBe(true);
  await from.fill('2026-10-20T10:01'); await query.click(); await pending;
  await member.selectOption(assignmentTwo); await expect(report).toHaveCount(0);
  release(); await expect.poll(() => completed).toBe(true);
  await visual.getByTestId('runtime-intelligence-process-stage-insights-reference').click();
  await expect(report.getByTestId('intelligence-reference-evidence-result')).toHaveCount(0);
  await from.fill('2026-10-20T10:00'); await to.fill('2026-10-20T11:00');
  expect(queries).toHaveLength(2); await query.click();
  await expect(report.getByTestId('intelligence-reference-evidence-result')).toBeVisible();
  expect(queries[2]).toMatchObject({ scopeId: scopeTwo, profileId: profileTwo });
  await member.selectOption(assignmentOne); await expect(report).toHaveCount(0);
});
