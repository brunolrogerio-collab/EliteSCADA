import { expect, test, type Page } from '@playwright/test';
const scopeId = '10000000-0000-0000-0000-000000000001';
const profileId = '30000000-0000-0000-0000-000000000003';
const projectKey = 'intelligence-reference-runtime-test';
const revision = 4;
const referencePath = '/api/engineering/asset-process-intelligence/analytics/reference-evidence/v1/query';

test('reference Runtime queries explicitly, rejects stale revisions and clears cancelled results across playback', async ({ page }) => {
  const fixture = await installRuntime(page);
  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  const visual = page.getByTestId('runtime-intelligence-reference-evidence');
  const result = visual.getByTestId('intelligence-reference-evidence-result');
  const query = visual.getByRole('button', { name: 'Query reference', exact: true });
  const from = visual.getByLabel('Reference start (local time)', { exact: true });
  const to = visual.getByLabel('Reference end (local time)', { exact: true });
  await expect(query).toBeVisible();
  await from.fill('2026-10-20T10:00'); await to.fill('2026-10-20T11:00');
  const startLabel = visual.locator('label').filter({ hasText: 'Reference start (local time)' });
  const brightnessGap = () => startLabel.evaluate(element => {
    const mean = (value: string) => {
      const rgb = value.match(/\d+/g)?.slice(0, 3).map(Number) ?? [];
      return rgb.reduce((sum, part) => sum + part, 0) / 3;
    };
    return Math.abs(mean(getComputedStyle(element).color) - mean(getComputedStyle(element.closest('details')!).backgroundColor));
  });
  for (const theme of ['light', 'dark', 'light']) {
    await page.getByRole('combobox', { name: 'Theme', exact: true }).selectOption(theme);
    await expect.poll(brightnessGap).toBeGreaterThan(100);
  }
  expect(fixture.requests).toHaveLength(0);
  await query.click(); await expect(result).toContainText('120 / 120 / 0');
  expect(fixture.requests[0]).toMatchObject({ scopeKind: 0, scopeId, profileId });
  await from.fill('2026-10-20T10:01'); await expect(result).toHaveCount(0);
  fixture.mode = 'late'; await query.click(); await fixture.started();
  await from.fill('2026-10-20T10:02'); fixture.release();
  await expect.poll(() => fixture.completed).toBe(true); await expect(result).toHaveCount(0);
  fixture.mode = 'stale'; await query.click(); await expect(visual.getByRole('alert')).toBeVisible();
  await expect(result).toHaveCount(0);
  fixture.mode = 'forbidden'; await query.click();
  await expect(visual.getByRole('alert')).toContainText('license'); await expect(result).toHaveCount(0);
  fixture.mode = 'valid'; await query.click(); await expect(result).toBeVisible();
  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay');
  await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(query).toBeDisabled(); await expect(result).toHaveCount(0);
  const count = fixture.requests.length;
  await playback.getByRole('button', { name: /Return to Live/ }).click();
  await expect(query).toBeEnabled(); expect(fixture.requests).toHaveLength(count);
  await expect(result).toHaveCount(0);
});

test('invalid reference identities fail closed without analytical reads', async ({ page }) => {
  const fixture = await installRuntime(page, 'invalid');
  await page.goto('/');
  const visual = page.getByTestId('runtime-intelligence-reference-evidence');
  await expect(visual.getByRole('alert')).toBeVisible();
  await expect(visual.getByRole('button', { name: 'Query reference', exact: true })).toHaveCount(0);
  expect(fixture.requests).toEqual([]);
});

async function installRuntime(page: Page, elementScopeId = scopeId) {
  let start!: () => void; const started = new Promise<void>(resolve => { start = resolve; });
  let release!: () => void; const delayed = new Promise<void>(resolve => { release = resolve; });
  const fixture = { requests: [] as any[], mode: 'valid', completed: false, started: () => started, release: () => release() };
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.routeWebSocket('**/ws/tags', () => {});
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/config') return route.fulfill({ json: { authenticationEnabled: false, localLoginEnabled: false, initialAdministratorRequired: false } });
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: []
    } });
    if (path === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, hasProjects: true } });
    if (path === '/api/product/info') return route.fulfill({ json: { productName: 'SCADIVO', channel: 'Test', version: '0.0.0', displayVersion: 'SCADIVO Test', informationalVersion: 'SCADIVO Test', buildCommit: 'reference-test' } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection(elementScopeId) });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === referencePath) {
      const body = route.request().postDataJSON(); fixture.requests.push(body);
      if (fixture.mode === 'late') {
        start(); await delayed;
        try { await route.fulfill({ json: response(body) }); } catch { /* A changed window aborts the request. */ }
        fixture.completed = true; return;
      }
      if (fixture.mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_ENTITLEMENT_REQUIRED' } });
      return route.fulfill({ json: { ...response(body), ...(fixture.mode === 'stale' ? { activeRevision: revision + 1 } : {}) } });
    }
    return route.fulfill({ status: 404, json: {} });
  });
  return fixture;
}
function response(body: any) {
  return { contractVersion: 1, projectKey, activeRevision: revision, scopeId, profileId,
    referenceWindow: body.referenceWindow, referenceCohort: { completeRequiredNumericCohortCount: 120 },
    signals: [{ signalId: '50000000-0000-0000-0000-000000000005', tagPath: 'Pump.Current', unit: 'A', required: true,
      reference: { rawObservationCount: 120, acceptedSynchronizedCount: 120, rejectedObservationCount: 0 } }] };
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
          type: 'core.intelligenceReferenceEvidenceV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            intelligenceScopeId: elementScopeId,
            intelligenceProfileId: profileId
          }
        }]
      }]
    }
  };
}
