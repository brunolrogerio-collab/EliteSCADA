import { expect, test } from '@playwright/test';

const scopeId = '10000000-0000-0000-0000-000000000001';
const assignmentId = '20000000-0000-0000-0000-000000000002';
const profileId = '30000000-0000-0000-0000-000000000003';
const signalIds = [
  '40000000-0000-0000-0000-000000000004',
  '50000000-0000-0000-0000-000000000005'
];
const ruleId = 'specific-active-energy-at-recorded-production-rate.v1';
const ruleVersion = '1.0.0';
const projectKey = 'intelligence-performance-runtime-test';
const revision = 4;

test('Performance V1B is transient, explicitly queried and unavailable in historical playback', async ({ page }) => {
  const catalogRequests: string[] = [];
  const queries: Array<Record<string, any>> = [];
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
      informationalVersion: 'EliteSCADA Test+intelligence-runtime-test', buildCommit: 'intelligence-runtime-test'
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection() });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === '/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog') {
      catalogRequests.push(`${request.method()} ${path}`);
      return route.fulfill({ json: performanceCatalog() });
    }
    if (path === '/api/engineering/asset-process-intelligence/analytics/performance/v1/query') {
      const body = request.postDataJSON() as Record<string, any>;
      queries.push(body);
      return route.fulfill({ json: performanceResponse(body) });
    }
    if (path === '/api/runtime/historical-playback/resolve') {
      const body = request.postDataJSON() as { screenKey: string; popupKeys: string[] };
      return route.fulfill({ json: { screenKey: body.screenKey, popupKeys: body.popupKeys ?? [], tags: [] } });
    }
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  const indicator = page.getByTestId('runtime-intelligence-performance');
  await expect(indicator).toBeVisible();
  await expect(indicator.getByRole('button', { name: 'Analyze' })).toBeVisible();
  expect(catalogRequests).toEqual([]);
  expect(queries).toEqual([]);

  await indicator.getByLabel('From (local time)').fill('2026-10-20T10:00');
  await indicator.getByLabel('To (local time)').fill('2026-10-21T11:00');
  expect(catalogRequests).toEqual([]);
  expect(queries).toEqual([]);

  await indicator.getByRole('button', { name: 'Analyze' }).click();
  await expect(indicator.getByTestId('intelligence-performance-result')).toBeVisible();
  expect(catalogRequests).toEqual([
    'GET /api/engineering/asset-process-intelligence/analytics/performance/v1/catalog'
  ]);
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({
    scopeKind: 0,
    scopeId,
    assignmentId,
    profileId,
    ruleId,
    signalIds
  });
  expect(Date.parse(queries[0].window.fromUtc)).toBeLessThan(Date.parse(queries[0].window.toUtc));
  await expect(indicator.getByRole('heading', { name: 'Active input power' })).toBeVisible();
  await expect(indicator.getByRole('heading', { name: 'Good production volume rate' })).toBeVisible();
  await expect(indicator.getByText('Sufficient data')).toBeVisible();
  await expect(indicator).toContainText(/Reading quality is not suitable for the calculation/);
  await expect(indicator).toContainText(/Communication failure: 2/);
  await expect(indicator).toContainText(/Combines readings with the same source time marked as synchronized/);
  await expect(indicator).not.toContainText('internal-signal-ref');
  await expect(indicator).not.toContainText(signalIds[0]);
  await expect(indicator).not.toContainText('quality_not_good');
  await expect(indicator).not.toContainText('exact_synchronized_source_timestamp');
  await expect(indicator.getByText(/not a diagnosis or field-validated efficiency claim/i)).toBeVisible();

  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay');
  await expect(playback).toBeVisible();
  await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(indicator.getByTestId('intelligence-performance-result')).toHaveCount(0);
  await expect(indicator.getByText(/Performance queries are unavailable during historical playback/i)).toBeVisible();
  await expect(indicator.getByRole('button', { name: 'Analyze' })).toHaveCount(0);
  expect(catalogRequests).toHaveLength(1);
  expect(queries).toHaveLength(1);

  await playback.getByRole('button', { name: /Return to Live/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'live');
  await expect(indicator.getByRole('button', { name: 'Analyze' })).toBeVisible();
  await expect(indicator.getByTestId('intelligence-performance-result')).toHaveCount(0);
  expect(catalogRequests).toHaveLength(1);
  expect(queries).toHaveLength(1);
});

function runtimeProjection() {
  return {
    mode: 'engineering',
    projectKey,
    projectName: 'Intelligence Performance Runtime Test',
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
        key: 'intelligence-performance',
        name: 'Recorded performance',
        elements: [{
          id: '90000000-0000-0000-0000-000000000010',
          key: 'performance-v1b',
          type: 'core.intelligencePerformanceV1B',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            intelligenceScopeId: scopeId,
            intelligenceAssignmentId: assignmentId,
            intelligenceProfileId: profileId,
            intelligenceRuleId: ruleId,
            intelligenceRuleVersion: ruleVersion,
            intelligenceSignalIds: JSON.stringify(signalIds)
          }
        }]
      }]
    }
  };
}

function performanceCatalog() {
  return {
    contractVersion: 1, catalogVersion: 1, ruleVersion, dataset: 'historian.samples',
    datasetContractVersion: 2, maximumWindowDays: 31, maximumRawRowsAcrossSelectedTags: 50000,
    rules: [{
      id: ruleId, version: ruleVersion, kind: 'derived',
      formula: 'active_input_power_W / net_good_production_rate_m3_s / 3,600,000',
      inputRoles: ['equipment.active_input_power', 'process.net_good_production_volume_rate'],
      inputUnits: [], outputUnit: 'kWh/m³', outputUnitVariants: [],
      temporalRequirement: 'Exact equality of distinct synchronized source timestamps; no interpolation or assumed cadence.',
      minimumQualifiedTuples: 2, bounds: 'Power >= 0 W; production rate > 0.', unavailableStates: []
    }]
  };
}

function performanceResponse(body: Record<string, any>) {
  return {
    contractVersion: 1, catalogVersion: 1, ruleId, ruleVersion,
    projectKey, activeRevision: revision,
    scopeKind: 'Equipment', scopeId: body.scopeId, assignmentId: body.assignmentId,
    equipmentId: body.scopeId, profileId: body.profileId, window: body.window,
    signals: [
      { signalId: signalIds[0], signalKey: 'internal-signal-ref-001', roleCode: 'equipment.active_input_power', canonicalUnit: 'W' },
      { signalId: signalIds[1], signalKey: 'internal-signal-ref-002', roleCode: 'process.net_good_production_volume_rate', canonicalUnit: 'm³/s' }
    ],
    inputs: signalIds.map(signalId => ({
      signalId, rawObservationCount: 10, eligibleSynchronizedCount: 8,
      pairedCount: 4, qualifiedCount: 3, qualityCounts: { Good: 8, BadCommunication: 2 }, rejectionCounts: { quality_not_good: 2 }
    })),
    totalRawObservationCount: 20,
    summary: { status: 'available', unit: 'kWh/m³', reasonCodes: ['quality_not_good'], qualifiedTupleCount: 3,
      minimum: 1.2, maximum: 1.8, arithmeticMean: 1.5, meanSemantics: 'arithmetic mean over qualified tuples' },
    provenance: { dataset: 'historian.samples', datasetContractVersion: 2,
      timestampPairing: 'exact_synchronized_source_timestamp', acquisitionAlignment: 'unverified',
      operatingRegimeEquivalence: 'unverified', expectedCadence: 'unknown', completeness: 'unknown',
      measurementInterpretation: 'derived from recorded active input power and recorded net-good production rate' }
  };
}
