import { expect, test, type Page } from '@playwright/test';
const scopeId = '10000000-0000-0000-0000-000000000001';
const profileId = '30000000-0000-0000-0000-000000000003';
const assignmentId = '40000000-0000-0000-0000-000000000004';
const tagId = '50000000-0000-0000-0000-000000000005';
const contexts = [1, 2].map((n, i) => ({ id: `60000000-0000-0000-0000-${String(n).padStart(12, '0')}`, displayLabel: `Operating context ${n}`, version: 3 + i, predicateTagIds: [tagId] }));
const projectKey = 'intelligence-conditions-runtime-test';
const revision = 4;
const queryPath = '/api/engineering/asset-process-intelligence/analytics/experimental-joint-deviation/v1/query';

const signals = [1, 2].map(n => ({ id: `71000000-0000-0000-0000-${String(n).padStart(12,'0')}`, tagId: `72000000-0000-0000-0000-${String(n).padStart(12,'0')}`, label: `Measurement ${n}` }));

test('joint Runtime validates cadence and selection, clears stale and cancelled results and disables historical queries', async ({ page }) => {
  const fixture = await installRuntime(page); await page.goto('/');
  const visual = page.getByTestId('runtime-intelligence-joint-deviation'); const runtime = page.getByTestId('runtime-engineering-application');
  const result = visual.getByTestId('intelligence-joint-deviation-result'); const query = visual.getByRole('button', { name: 'Calculate joint deviation', exact: true });
  const context = visual.getByRole('combobox', { name: 'Saved context', exact: true });
  async function selectTargets() { for(const signal of signals) await visual.getByRole('checkbox', { name: signal.label, exact: true }).check(); }
  await context.selectOption(contexts[0].id); await selectTargets();
  await visual.getByLabel('Reference start', {exact:true}).fill('2026-10-20T10:00'); await visual.getByLabel('Reference end', {exact:true}).fill('2026-10-20T11:00');
  await visual.getByLabel('Evaluation start', {exact:true}).fill('2026-10-20T11:00'); await visual.getByLabel('Evaluation end', {exact:true}).fill('2026-10-20T12:00');
  const cadence=visual.getByLabel('Expected interval between readings (seconds)',{exact:true}); await cadence.fill('60'); await expect(query).toBeDisabled();
  await visual.getByLabel('Evaluation start', {exact:true}).fill('2026-10-20T11:01'); await cadence.fill('0'); await expect(query).toBeDisabled();
  await cadence.fill('60'); expect(fixture.requests).toEqual([]); await query.click(); await expect(result).toContainText('120 / 1 / 24');
  expect(fixture.requests[0]).toMatchObject({ scopeId, assignmentId, expectedProfileVersion: 3, expectedCadenceSeconds: 60, targetSignalIds: signals.map(s=>s.id) });
  await cadence.fill('61'); await expect(result).toHaveCount(0); fixture.mode='late'; await query.click(); await fixture.started();
  await context.selectOption(contexts[1].id); fixture.release(); await expect.poll(()=>fixture.completed).toBe(true); await expect(result).toHaveCount(0); await selectTargets();
  for(const mode of ['stale','threshold','changed','forbidden']) { fixture.mode=mode; await query.click(); await expect(visual.getByRole('alert')).toBeVisible(); await expect(result).toHaveCount(0); }
  fixture.mode='insufficient'; await query.click(); await expect(result).toContainText('Not enough data'); await expect(result.getByRole('table')).toHaveCount(0);
  fixture.mode='valid'; await query.click(); await expect(result).toBeVisible();
  await runtime.getByRole('button',{name:/Historical Playback/}).click(); const playback=page.getByTestId('runtime-playback-overlay');
  await playback.getByRole('button',{name:/Enter Playback/}).click(); await expect(query).toBeDisabled(); await expect(result).toHaveCount(0);
  const count=fixture.requests.length; await playback.getByRole('button',{name:/Return to Live/}).click(); await expect(result).toHaveCount(0); expect(fixture.requests).toHaveLength(count);
});
for(const scenario of ['bad-id','duplicate','zero-version','predicate-values','signal-duplicate','signal-extra']) {
 test(`joint Runtime rejects malformed references ${scenario} without reading analytics`,async({page})=>{
  const fixture=await installRuntime(page,scenario); await page.goto('/'); const visual=page.getByTestId('runtime-intelligence-joint-deviation');
  await expect(visual.getByRole('alert')).toBeVisible(); await expect(visual.getByRole('button',{name:'Calculate joint deviation',exact:true})).toHaveCount(0); expect(fixture.requests).toEqual([]);
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
      if (scenario?.startsWith('signal-')) {
        const invalid = JSON.parse(props.intelligenceJointSignals);
        if (scenario === 'signal-duplicate') invalid[1].tagId = invalid[0].tagId;
        if (scenario === 'signal-extra') invalid[0].minimum = 0;
        props.intelligenceJointSignals = JSON.stringify(invalid);
      }
      return route.fulfill({ json: projection });
    }
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === queryPath) {
      const body = route.request().postDataJSON(); fixture.requests.push(body);
      if (fixture.mode === 'late') { start(); await delayed; try { await route.fulfill({ json: response(body) }); } catch { /* Context change aborts this request. */ } fixture.completed = true; return; }
      if (fixture.mode === 'changed') return route.fulfill({ status: 409, json: { code: 'INTELLIGENCE_OPERATING_CONTEXT_VERSION_MISMATCH' } });
      if (fixture.mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_ENTITLEMENT_REQUIRED' } });
      if (fixture.mode === 'threshold') return route.fulfill({ json: { ...response(body), decisionThreshold: 1 } });
      if (fixture.mode === 'insufficient') return route.fulfill({ json: { ...response(body), referenceFitStatus: 'insufficient_data', scores: [], evaluationScoredCohortCount: 0 } });
      return route.fulfill({ json: { ...response(body), ...(fixture.mode === 'stale' ? { operatingContextProfileVersion: 99 } : {}) } });
    }
    return route.fulfill({ status: 404, json: {} });
  }); return fixture;
}
function response(body: any) {
  return { contractVersion: 1, projectKey, activeRevision: revision, scopeId, assignmentId, measurementProfileId: profileId,
    operatingContextProfileId: body.operatingContextProfileId, operatingContextProfileVersion: body.expectedProfileVersion,
    expectedCadenceSeconds: body.expectedCadenceSeconds, validationStatus: 'experimental_not_field_validated', classificationStatus: 'none', decisionThreshold: null,
    referenceWindow: body.referenceWindow, evaluationWindow: body.evaluationWindow, referenceFitStatus: 'available', referenceFitUnavailableReason: null,
    referenceCompleteCohortCount: 120, evaluationScoredCohortCount: 1, evaluationWarmupCohortCount: 24, omittedEvaluationScores: 0,
    targetSignals: body.targetSignalIds.map((signalId: string) => ({ signalId })),
    scores: [{ sourceTimestampUtc: body.evaluationWindow.fromUtc, score: 2, levelComponent: 1, relationshipComponent: 1, crossSignalInteraction: 0,
      targetContributions: body.targetSignalIds.map((signalId: string) => ({ signalId, level: .5, marginalVariance: .5 })) }] };
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
          type: 'core.intelligenceExperimentalJointDeviationV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            intelligenceScopeId: elementScopeId,
            intelligenceProfileId: profileId, intelligenceAssignmentId: assignmentId, intelligenceOperatingContexts: JSON.stringify(contexts), intelligenceJointSignals: JSON.stringify(signals)
          }
        }]
      }]
    }
  };
}
