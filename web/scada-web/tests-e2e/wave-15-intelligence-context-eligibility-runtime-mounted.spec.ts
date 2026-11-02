import { expect, test, type Page } from '@playwright/test';
const scopeId = '10000000-0000-0000-0000-000000000001';
const profileId = '30000000-0000-0000-0000-000000000003';
const assignmentId = '40000000-0000-0000-0000-000000000004';
const tagId = '50000000-0000-0000-0000-000000000005';
const contexts = [1, 2].map((n, i) => ({ id: `60000000-0000-0000-0000-${String(n).padStart(12, '0')}`, displayLabel: `Operating context ${n}`, version: 3 + i, predicateTagIds: [tagId] }));
const projectKey = 'intelligence-conditions-runtime-test';
const revision = 4;
const queryPath = '/api/engineering/asset-process-intelligence/analytics/context-eligibility/v1/query';

test('conditions Runtime uses the saved version and clears cancelled, stale and historical results', async ({ page }) => {
  const fixture = await installRuntime(page);
  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  const visual = page.getByTestId('runtime-intelligence-context-eligibility');
  const context = visual.getByRole('combobox', { name: 'Saved context', exact: true });
  const result = visual.getByTestId('intelligence-context-eligibility-result');
  const query = visual.getByRole('button', { name: 'Query conditions', exact: true });
  const from = visual.getByLabel('Period start (local time)', { exact: true });
  const to = visual.getByLabel('Period end (local time)', { exact: true });
  await context.selectOption(contexts[0].id);
  await from.fill('2026-10-20T10:00'); await to.fill('2026-10-20T11:00');
  expect(fixture.requests).toHaveLength(0);
  await query.click(); await expect(result).toContainText('120 / 120 / 90');
  expect(fixture.requests[0]).toMatchObject({ scopeKind: 0, scopeId, assignmentId, operatingContextProfileId: contexts[0].id, expectedProfileVersion: 3 });
  await from.fill('2026-10-20T10:01'); await expect(result).toHaveCount(0);
  fixture.mode = 'late'; await query.click(); await fixture.started();
  await context.selectOption(contexts[1].id); fixture.release();
  await expect.poll(() => fixture.completed).toBe(true); await expect(result).toHaveCount(0);
  fixture.mode = 'stale'; await query.click(); await expect(visual.getByRole('alert')).toBeVisible(); await expect(result).toHaveCount(0);
  fixture.mode = 'changed'; await query.click(); await expect(visual.getByRole('alert')).toContainText('context or its measurements changed');
  fixture.mode = 'forbidden'; await query.click(); await expect(visual.getByRole('alert')).toContainText('license');
  fixture.mode = 'valid'; await query.click(); await expect(result).toBeVisible();
  expect(fixture.requests.at(-1)).toMatchObject({ operatingContextProfileId: contexts[1].id, expectedProfileVersion: 4 });
  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay'); await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(query).toBeDisabled(); await expect(result).toHaveCount(0);
  const count = fixture.requests.length;
  await playback.getByRole('button', { name: /Return to Live/ }).click();
  await expect(result).toHaveCount(0); expect(fixture.requests).toHaveLength(count);
});

for (const scenario of ['bad-id', 'duplicate', 'zero-version', 'predicate-values']) {
  test(`invalid context reference catalog ${scenario} fails closed without reads`, async ({ page }) => {
    const fixture = await installRuntime(page, scenario); await page.goto('/');
    const visual = page.getByTestId('runtime-intelligence-context-eligibility');
    await expect(visual.getByRole('alert')).toBeVisible(); expect(fixture.requests).toEqual([]);
    await expect(visual.getByRole('button', { name: 'Query conditions', exact: true })).toHaveCount(0);
  });
}
async function installRuntime(page: Page, scenario?: string) {
  let start!: () => void; const started = new Promise<void>(resolve => { start = resolve; });
  let release!: () => void; const delayed = new Promise<void>(resolve => { release = resolve; });
  const fixture = { requests: [] as any[], mode: 'valid', completed: false, started: () => started, release: () => release() };
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.routeWebSocket('**/ws/tags', () => {});
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/config') return route.fulfill({ json: { authenticationEnabled: false, localLoginEnabled: false, initialAdministratorRequired: false } });
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: { authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 }, authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: [] } });
    if (path === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, hasProjects: true } });
    if (path === '/api/product/info') return route.fulfill({ json: { productName: 'SCADIVO', channel: 'Test', version: '0.0.0', displayVersion: 'SCADIVO Test', informationalVersion: 'SCADIVO Test', buildCommit: 'conditions-test' } });
    if (path === '/api/runtime/application') {
      const projection = runtimeProjection(); const props = projection.package.screens[0].elements[0].properties;
      if (scenario) {
        const invalid: any[] = JSON.parse(props.intelligenceOperatingContexts);
        if (scenario === 'bad-id') invalid[0].id = 'wrong';
        if (scenario === 'duplicate') invalid.push(invalid[0]);
        if (scenario === 'zero-version') invalid[0].version = 0;
        if (scenario === 'predicate-values') invalid[0].predicates = [{ minimum: 20 }];
        props.intelligenceOperatingContexts = JSON.stringify(invalid);
      }
      return route.fulfill({ json: projection });
    }
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === queryPath) {
      const body = route.request().postDataJSON(); fixture.requests.push(body);
      if (fixture.mode === 'late') { start(); await delayed; try { await route.fulfill({ json: response(body) }); } catch { /* Context change aborts this request. */ } fixture.completed = true; return; }
      if (fixture.mode === 'changed') return route.fulfill({ status: 409, json: { code: 'INTELLIGENCE_OPERATING_CONTEXT_VERSION_MISMATCH' } });
      if (fixture.mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_ENTITLEMENT_REQUIRED' } });
      return route.fulfill({ json: { ...response(body), ...(fixture.mode === 'stale' ? { profileVersion: 99 } : {}) } });
    }
    return route.fulfill({ status: 404, json: {} });
  }); return fixture;
}
function response(body: any) {
  return { contractVersion: 1, projectKey, activeRevision: revision, assignmentId, measurementProfileId: profileId,
    operatingContextProfileId: body.operatingContextProfileId, profileVersion: body.expectedProfileVersion,
    referenceWindow: body.referenceWindow,
    predicates: [{ tagId, referenceAcceptedCount: 120, referenceMatchCount: 90, comparisonAcceptedCount: null, comparisonMatchCount: null, status: 'available' }],
    signals: [{ signalId: '70000000-0000-0000-0000-000000000007', tagId, tagPath: 'Pump.Current', engineeringUnit: 'A',
      reference: { rawObservationCount: 120, acceptedSynchronizedUniqueCount: 120, contextEligibleObservationCount: 90, status: 'available', rejectionCounts: {} } }] };
}
function runtimeProjection(elementScopeId = scopeId) {
  return {
    mode: 'engineering',
    projectKey,
    projectName: 'Intelligence Profile Coverage Runtime Test',
    revision,
    activatedAtUtc: '2026-10-21T00:00:00Z',
    package: {
      schema: 'scada.engineering',
      schemaVersion: 20,
      exportedAt: '2026-10-21T00:00:00Z',
      startupScreenId: '90000000-0000-0000-0000-000000000009',
      runtimePresentation: { historicalPlaybackEnabled: true },
      tags: [], alarms: [], templates: [], equipment: [], dynamos: [], scripts: [],
      scriptVisualEventReferences: [], visualAssets: [], popups: [],
      screens: [{
        id: '90000000-0000-0000-0000-000000000009',
        key: 'intelligence-profile-coverage',
        name: 'Historical profile coverage',
        elements: [{
          id: '90000000-0000-0000-0000-000000000010',
          key: 'profile-coverage-v1',
          type: 'core.intelligenceContextEligibilityV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            intelligenceScopeId: elementScopeId,
            intelligenceProfileId: profileId, intelligenceAssignmentId: assignmentId, intelligenceOperatingContexts: JSON.stringify(contexts)
          }
        }]
      }]
    }
  };
}
