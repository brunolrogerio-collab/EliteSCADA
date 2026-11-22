import { expect, test, type Locator } from '@playwright/test';

const projectKey = 'intelligence-process-stage-coverage-runtime-test';
const revision = 8;
const stageId = '10000000-0000-0000-0000-000000000001';
const assignmentOne = '20000000-0000-0000-0000-000000000002';
const assignmentTwo = '30000000-0000-0000-0000-000000000003';
const scopeOne = '40000000-0000-0000-0000-000000000004';
const scopeTwo = '50000000-0000-0000-0000-000000000005';
const profileOne = '60000000-0000-0000-0000-000000000006';
const profileTwo = '70000000-0000-0000-0000-000000000007';
const coveragePath = '/api/engineering/asset-process-intelligence/analytics/profile-coverage';

test('process-stage coverage selects one member, queries explicitly and rejects mismatched assignment provenance', async ({ page }) => {
  const queries: Array<Record<string, any>> = [];
  let resolveThirdQuery!: () => void;
  const thirdQueryStarted = new Promise<void>(resolve => { resolveThirdQuery = resolve; });
  let releaseThirdQuery!: () => void;
  const thirdQueryRelease = new Promise<void>(resolve => { releaseThirdQuery = resolve; });
  let thirdQueryCompleted = false;

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
      authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: []
    } });
    if (path === '/api/product/info') return route.fulfill({ json: {
      productName: 'EliteSCADA', channel: 'Test', version: '0.0.0', displayVersion: 'EliteSCADA Test',
      informationalVersion: 'EliteSCADA Test+process-stage-coverage-runtime-test', buildCommit: 'process-stage-coverage-runtime-test'
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection() });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === coveragePath) {
      const body = request.postDataJSON() as Record<string, any>;
      queries.push(body);
      if (queries.length === 3) {
        resolveThirdQuery();
        await thirdQueryRelease;
        try { await route.fulfill({ json: coverageResponse(body, body.scopeId === scopeOne ? assignmentOne : assignmentTwo) }); }
        catch { /* The Runtime should abort this pending request when playback begins. */ }
        thirdQueryCompleted = true;
        return;
      }
      const assignment = queries.length === 1 ? assignmentOne : assignmentOne;
      return route.fulfill({ json: coverageResponse(body, assignment) });
    }
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  const visual = page.getByTestId('runtime-intelligence-process-stage-coverage');
  await expect(visual).toBeVisible();
  const member = visual.getByTestId('runtime-intelligence-process-stage-member');
  const coverage = visual.getByTestId('runtime-intelligence-profile-coverage');
  await expect(member).toHaveValue(assignmentOne);
  await expect(coverage.getByRole('button', { name: 'Analyze coverage' })).toBeVisible();
  expect(queries).toEqual([]);

  await setWindow(coverage, 'From (local time)', 'To (local time)', '2026-10-20T10:00', '2026-10-20T11:00');
  expect(queries).toEqual([]);
  await coverage.getByRole('button', { name: 'Analyze coverage' }).click();
  await expect(coverage.getByTestId('intelligence-profile-coverage-result')).toBeVisible();
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({ scopeKind: 0, scopeId: scopeOne, profileId: profileOne });
  await expect(coverage.getByText('Pump P-101 · Pump profile')).toBeVisible();

  await member.selectOption(assignmentTwo);
  const secondCoverage = visual.getByTestId('runtime-intelligence-profile-coverage');
  await expect(secondCoverage.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  await setWindow(secondCoverage, 'From (local time)', 'To (local time)', '2026-10-21T10:00', '2026-10-21T11:00');
  await secondCoverage.getByRole('button', { name: 'Analyze coverage' }).click();
  await expect(secondCoverage.getByRole('alert')).toContainText('response did not match this profile and time window');
  expect(queries).toHaveLength(2);
  expect(queries[1]).toMatchObject({ scopeKind: 0, scopeId: scopeTwo, profileId: profileTwo });
  await expect(secondCoverage.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);

  await member.selectOption(assignmentOne);
  const pendingCoverage = visual.getByTestId('runtime-intelligence-profile-coverage');
  await setWindow(pendingCoverage, 'From (local time)', 'To (local time)', '2026-10-22T10:00', '2026-10-22T11:00');
  await pendingCoverage.getByRole('button', { name: 'Analyze coverage' }).click();
  await thirdQueryStarted;
  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay');
  await expect(playback).toBeVisible();
  await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(pendingCoverage.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  await expect(pendingCoverage.getByText(/unavailable during historical playback/i)).toBeVisible();
  releaseThirdQuery();
  await expect.poll(() => thirdQueryCompleted).toBe(true);
  await expect(pendingCoverage.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  expect(queries).toHaveLength(3);
  await expect(visual.getByText(/No process KPI/)).toBeVisible();
});

async function setWindow(visual: Locator, fromLabel: string, toLabel: string, from: string, to: string) {
  await visual.getByLabel(fromLabel).fill(from);
  await visual.getByLabel(toLabel).fill(to);
}

function runtimeProjection() {
  const members = [
    { assignmentId: assignmentOne, scopeId: scopeOne, profileId: profileOne, displayLabel: 'Pump P-101 · Pump profile', order: 0 },
    { assignmentId: assignmentTwo, scopeId: scopeTwo, profileId: profileTwo, displayLabel: 'Pump P-102 · Pump profile', order: 1 }
  ];
  return {
    mode: 'engineering', projectKey, projectName: 'Process-stage coverage Runtime Test', revision,
    activatedAtUtc: '2026-10-21T00:00:00Z',
    package: {
      schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-10-21T00:00:00Z',
      startupScreenId: '90000000-0000-0000-0000-000000000009',
      runtimePresentation: { historicalPlaybackEnabled: true },
      tags: [], alarms: [], templates: [], equipment: [], dynamos: [], scripts: [],
      scriptVisualEventReferences: [], visualAssets: [], popups: [],
      screens: [{
        id: '90000000-0000-0000-0000-000000000009', key: 'process-stage-coverage', name: 'Pumping stage coverage',
        elements: [{
          id: '90000000-0000-0000-0000-000000000010', key: 'process-stage-coverage-v1',
          type: 'core.intelligenceProcessStageCoverageV1',
          properties: {
            x: 50, y: 60, width: 1250, height: 720,
            intelligenceStageId: stageId,
            intelligenceStageKey: 'pumping-stage',
            intelligenceStageName: 'Pumping stage',
            intelligenceStageMembers: JSON.stringify(members)
          }
        }]
      }]
    }
  };
}

function coverageResponse(body: Record<string, any>, provenanceAssignmentId: string) {
  return {
    contractVersion: 1, ruleCatalogVersion: 1, ruleVersion: '1.0.0',
    projectKey, activeRevision: revision, scopeKind: 'Equipment', scopeId: body.scopeId,
    profileId: body.profileId, profileKey: 'pump-profile', profileName: 'Pump profile', window: body.window,
    signals: [{
      signalId: '80000000-0000-0000-0000-000000000008', signalKey: 'Motor current', signalRoleMetadata: 'current',
      required: true, tagId: '81000000-0000-0000-0000-000000000008', tagPath: 'Plant.Pump.Current',
      tagDataType: 'Double', unit: 'A', status: 'descriptive_trend_available', summary: {
        rawObservationCount: 10, acceptedSampleCount: 8, rejectedSampleCount: 2,
        qualityCounts: { Good: 8, Uncertain: 2 }, validSampleCount: 8,
        minimum: 10, maximum: 12, sampleMean: 11, completeness: 'unknown', expectedSampleCount: null
      }
    }],
    temporalAlignment: {
      requiredSignalCount: 1, requiredSignalsWithGoodSamples: 1, exactHistorianTimestampIntersectionCount: 8,
      status: 'single_required_signal_no_alignment_comparison', timestampBasis: 'Historian event time',
      sourceAcquisitionAlignment: 'unverified', expectedCadence: 'unknown', completeness: 'unknown'
    },
    provenance: {
      dataset: 'historian.samples', datasetContractVersion: 2, ruleVersion: '1.0.0',
      assignments: [{ assignmentId: provenanceAssignmentId, equipmentId: body.scopeId, profileId: body.profileId,
        signalId: '80000000-0000-0000-0000-000000000008', signalKey: 'Motor current' }],
      signalRoleInterpretation: 'metadata only', unitConversion: 'none',
      processKpiDisposition: 'unsupported_without_a_canonical_semantic_and_unit-dimension-contract'
    }
  };
}
