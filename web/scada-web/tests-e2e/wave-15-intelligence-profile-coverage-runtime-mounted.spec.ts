import { expect, test } from '@playwright/test';

const scopeId = '10000000-0000-0000-0000-000000000001';
const profileId = '30000000-0000-0000-0000-000000000003';
const projectKey = 'intelligence-profile-coverage-runtime-test';
const revision = 4;
const coveragePath = '/api/engineering/asset-process-intelligence/analytics/profile-coverage';

test('profile coverage is Runtime-only, explicitly queried and transient on input change or authorization failure', async ({ page }) => {
  const queries: Array<Record<string, any>> = [];
  let resolveSecondQueryStarted!: () => void;
  const secondQueryStarted = new Promise<void>(resolve => { resolveSecondQueryStarted = resolve; });
  let releaseSecondQuery!: () => void;
  const secondQueryRelease = new Promise<void>(resolve => { releaseSecondQuery = resolve; });
  let secondQueryCompleted = false;
  const authorizationCodes = [
    'INTELLIGENCE_ENTITLEMENT_REQUIRED',
    'INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS',
    'INTELLIGENCE_EXECUTION_PERMIT_REQUIRED'
  ];
  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'en'));
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
      authenticationEnabled: false,
      runtime: ['View', 'TrendUse'],
      workspace: []
    } });
    if (path === '/api/product/info') return route.fulfill({ json: {
      productName: 'EliteSCADA', channel: 'Test', version: '0.0.0', displayVersion: 'EliteSCADA Test',
      informationalVersion: 'EliteSCADA Test+profile-coverage-runtime-test', buildCommit: 'profile-coverage-runtime-test'
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection() });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === coveragePath) {
      const body = request.postDataJSON() as Record<string, any>;
      queries.push(body);
      if (queries.length === 2) {
        resolveSecondQueryStarted();
        await secondQueryRelease;
        try { await route.fulfill({ json: coverageResponse(body) }); }
        catch { /* The Runtime abort is the behavior under test; the client may already have cancelled this route. */ }
        secondQueryCompleted = true;
        return;
      }
      if (queries.length >= 3 && queries.length <= 5) {
        return route.fulfill({ status: 403, json: { code: authorizationCodes[queries.length - 3] } });
      }
      if (queries.length === 6) return route.fulfill({ json: { ...coverageResponse(body), projectKey: 'stale-project' } });
      return route.fulfill({ json: coverageResponse(body) });
    }
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  const visual = page.getByTestId('runtime-intelligence-profile-coverage');
  await expect(visual).toBeVisible();
  await expect(visual.getByRole('button', { name: 'Analyze coverage' })).toBeVisible();
  expect(queries).toEqual([]);

  await visual.getByLabel('From (local time)').fill('2026-10-20T10:00');
  await visual.getByLabel('To (local time)').fill('2026-10-21T11:00');
  expect(queries).toEqual([]);
  await visual.getByRole('button', { name: 'Analyze coverage' }).click();
  await expect(visual.getByTestId('intelligence-profile-coverage-result')).toBeVisible();
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({ scopeKind: 0, scopeId, profileId, bucketSeconds: 900 });
  expect(Date.parse(queries[0].window.fromUtc)).toBeLessThan(Date.parse(queries[0].window.toUtc));
  await expect(visual.getByText('Motor current')).toBeVisible();
  await expect(visual.getByText('Plant.Pump.Current')).toBeVisible();
  await expect(visual.getByText('Historian event time')).toBeVisible();
  await expect(visual.getByText(/Good: 8/)).toBeVisible();
  await expect(visual.getByText('historian.samples')).toBeVisible();

  await visual.getByLabel('From (local time)').fill('2026-10-20T12:00');
  await visual.getByLabel('To (local time)').fill('2026-10-20T13:00');
  await visual.getByRole('button', { name: 'Analyze coverage' }).click();
  await secondQueryStarted;

  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay');
  await expect(playback).toBeVisible();
  await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(visual.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  await expect(visual.getByText(/Coverage queries are unavailable during historical playback/i)).toBeVisible();
  await expect(visual.getByRole('button', { name: 'Analyze coverage' })).toHaveCount(0);
  expect(queries).toHaveLength(2);
  releaseSecondQuery();
  await expect.poll(() => secondQueryCompleted).toBe(true);
  await expect(visual.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);

  await playback.getByRole('button', { name: /Return to Live/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'live');
  await expect(visual.getByRole('button', { name: 'Analyze coverage' })).toBeVisible();

  await visual.getByLabel('From (local time)').fill('2026-10-21T12:00');
  await expect(visual.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  await expect(visual.getByRole('button', { name: 'Analyze coverage' })).toBeDisabled();
  expect(queries).toHaveLength(2);

  await visual.getByLabel('From (local time)').fill('2026-09-01T00:00');
  await visual.getByLabel('To (local time)').fill('2026-10-21T00:00');
  await expect(visual.getByRole('button', { name: 'Analyze coverage' })).toBeDisabled();
  expect(queries).toHaveLength(2);

  await visual.getByLabel('To (local time)').fill('2026-10-21T13:00');
  for (const [index, code] of authorizationCodes.entries()) {
    const fromHour = String(14 + index * 2).padStart(2, '0');
    const toHour = String(15 + index * 2).padStart(2, '0');
    await visual.getByLabel('From (local time)').fill(`2026-10-21T${fromHour}:00`);
    await visual.getByLabel('To (local time)').fill(`2026-10-21T${toHour}:00`);
    await visual.getByRole('button', { name: 'Analyze coverage' }).click();
    await expect(visual.getByRole('alert')).toContainText(code);
    await expect(visual.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  }

  await visual.getByLabel('From (local time)').fill('2026-10-22T10:00');
  await visual.getByLabel('To (local time)').fill('2026-10-22T11:00');
  await visual.getByRole('button', { name: 'Analyze coverage' }).click();
  await expect(visual.getByRole('alert')).toContainText('response did not match this profile and time window');
  await expect(visual.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  expect(queries).toHaveLength(6);
});

function runtimeProjection() {
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
          type: 'core.intelligenceProfileCoverageV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            intelligenceScopeId: scopeId,
            intelligenceProfileId: profileId
          }
        }]
      }]
    }
  };
}

function coverageResponse(body: Record<string, any>) {
  return {
    contractVersion: 1,
    ruleCatalogVersion: 1,
    ruleVersion: '1.0.0',
    projectKey,
    activeRevision: revision,
    scopeKind: 'Equipment',
    scopeId: body.scopeId,
    profileId: body.profileId,
    profileKey: 'pump-profile',
    profileName: 'Pump profile',
    window: body.window,
    signals: [{
      signalId: '40000000-0000-0000-0000-000000000004',
      signalKey: 'Motor current',
      signalRoleMetadata: 'current',
      required: true,
      tagId: '50000000-0000-0000-0000-000000000005',
      tagPath: 'Plant.Pump.Current',
      tagDataType: 'Double',
      unit: 'A',
      status: 'descriptive_trend_available',
      summary: {
        rawObservationCount: 10, acceptedSampleCount: 8, rejectedSampleCount: 2,
        qualityCounts: { Good: 8, Uncertain: 2 }, validSampleCount: 8,
        minimum: 10, maximum: 12, sampleMean: 11, completeness: 'unknown', expectedSampleCount: null
      }
    }],
    temporalAlignment: {
      requiredSignalCount: 1,
      requiredSignalsWithGoodSamples: 1,
      exactHistorianTimestampIntersectionCount: 8,
      status: 'single_required_signal_no_alignment_comparison',
      timestampBasis: 'Historian event time',
      sourceAcquisitionAlignment: 'unverified',
      expectedCadence: 'unknown',
      completeness: 'unknown'
    },
    provenance: {
      dataset: 'historian.samples',
      datasetContractVersion: 2,
      ruleVersion: '1.0.0',
      signalRoleInterpretation: 'not interpreted',
      unitConversion: 'none',
      processKpiDisposition: 'not calculated'
    }
  };
}
