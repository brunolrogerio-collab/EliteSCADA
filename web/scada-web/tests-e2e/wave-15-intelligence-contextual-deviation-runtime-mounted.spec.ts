import { expect, test, type Locator } from '@playwright/test';

const scopeId = '10000000-0000-0000-0000-000000000001';
const assignmentId = '20000000-0000-0000-0000-000000000002';
const profileId = '30000000-0000-0000-0000-000000000003';
const targetIds = [
  '40000000-0000-0000-0000-000000000004',
  '50000000-0000-0000-0000-000000000005'
];
const firstContextId = '60000000-0000-0000-0000-000000000006';
const secondContextId = '70000000-0000-0000-0000-000000000007';
const projectKey = 'intelligence-contextual-deviation-runtime-test';
const revision = 7;
const apiRoot = '/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v2';

test('contextual deviation V2 is explicit, chronological, transient and validates authorization and response identity', async ({ page }) => {
  const queries: Array<Record<string, any>> = [];
  let resolveSecondQueryStarted!: () => void;
  const secondQueryStarted = new Promise<void>(resolve => { resolveSecondQueryStarted = resolve; });
  let releaseSecondQuery!: () => void;
  const secondQueryRelease = new Promise<void>(resolve => { releaseSecondQuery = resolve; });
  let secondQueryCompleted = false;

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
      informationalVersion: 'EliteSCADA Test+contextual-deviation-runtime-test', buildCommit: 'contextual-deviation-runtime-test'
    } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection() });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === `${apiRoot}/catalog`) return route.fulfill({ json: {
      contractVersion: 2, catalogVersion: 2, ruleId: 'contextual-deviation-vector.v2', ruleVersion: '2.0.0',
      maximumWindowDays: 31, minimumTargetSignals: 2, maximumTargetSignals: 8, contextSignalCount: 2
    } });
    if (path === `${apiRoot}/query`) {
      const body = request.postDataJSON() as Record<string, any>;
      queries.push(body);
      if (queries.length === 2) {
        resolveSecondQueryStarted();
        await secondQueryRelease;
        try { await route.fulfill({ json: deviationResponse(body) }); }
        catch { /* Runtime cancellation may already have aborted this route. */ }
        secondQueryCompleted = true;
        return;
      }
      if (queries.length === 3) return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_EXECUTION_PERMIT_REQUIRED' } });
      if (queries.length === 4) return route.fulfill({ json: { ...deviationResponse(body), projectKey: 'stale-project' } });
      return route.fulfill({ json: deviationResponse(body) });
    }
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  const visual = page.getByTestId('runtime-intelligence-contextual-deviation-v2');
  await expect(visual).toBeVisible();
  const analyze = visual.getByRole('button', { name: 'Analyze' });
  await expect(analyze).toBeDisabled();
  await expect(visual.getByLabel('Reference from (local time)')).toHaveValue('');
  expect(queries).toEqual([]);

  await setWindow(visual, 'Reference from (local time)', 'Reference to (local time)', 'Evaluation from (local time)', 'Evaluation to (local time)',
    '2026-10-20T10:00', '2026-10-20T11:00', '2026-10-20T11:01', '2026-10-20T12:01');
  expect(queries).toEqual([]);
  await analyze.click();
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-result')).toBeVisible();
  expect(queries).toHaveLength(1);
  expect(queries[0]).toMatchObject({
    scopeKind: 0, scopeId, assignmentId, targetSignalIds: targetIds,
    firstContextSignalId: firstContextId,
    firstContextLowerInclusiveCanonical: 5,
    firstContextUpperExclusiveCanonical: 25,
    secondContextSignalId: secondContextId,
    secondContextLowerInclusiveCanonical: 100,
    secondContextUpperExclusiveCanonical: 220
  });
  expect(Date.parse(queries[0].referenceWindow.fromUtc)).toBeLessThan(Date.parse(queries[0].referenceWindow.toUtc));
  expect(Date.parse(queries[0].referenceWindow.toUtc)).toBeLessThan(Date.parse(queries[0].evaluationWindow.fromUtc));
  expect(Date.parse(queries[0].evaluationWindow.fromUtc)).toBeLessThan(Date.parse(queries[0].evaluationWindow.toUtc));
  await expect(visual.getByRole('heading', { name: 'Motor current' })).toBeVisible();
  await expect(visual.getByText('Reference median', { exact: true })).toHaveCount(2);
  await expect(visual.getByText(/No combined anomaly/)).toBeVisible();

  await visual.getByLabel('Evaluation from (local time)').fill('2026-10-20T12:00');
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-result')).toHaveCount(0);
  await setWindow(visual, 'Reference from (local time)', 'Reference to (local time)', 'Evaluation from (local time)', 'Evaluation to (local time)',
    '2026-10-20T10:00', '2026-10-20T11:00', '2026-10-20T11:01', '2026-10-20T12:01');
  await visual.getByRole('button', { name: 'Analyze' }).click();
  await secondQueryStarted;

  await runtime.getByRole('button', { name: /Historical Playback/ }).click();
  const playback = page.getByTestId('runtime-playback-overlay');
  await expect(playback).toBeVisible();
  await playback.getByRole('button', { name: /Enter Playback/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(visual.getByText(/unavailable during playback/i)).toBeVisible();
  await expect(visual.getByRole('button', { name: 'Analyze' })).toHaveCount(0);
  expect(queries).toHaveLength(2);
  releaseSecondQuery();
  await expect.poll(() => secondQueryCompleted).toBe(true);
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-result')).toHaveCount(0);

  await playback.getByRole('button', { name: /Return to Live/ }).click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'live');
  await expect(visual.getByRole('button', { name: 'Analyze' })).toBeVisible();
  await setWindow(visual, 'Reference from (local time)', 'Reference to (local time)', 'Evaluation from (local time)', 'Evaluation to (local time)',
    '2026-10-21T14:00', '2026-10-21T15:00', '2026-10-21T15:01', '2026-10-21T16:01');
  await visual.getByRole('button', { name: 'Analyze' }).click();
  await expect(visual.getByRole('alert')).toContainText('An active Intelligence execution permit is required');
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-result')).toHaveCount(0);

  await setWindow(visual, 'Reference from (local time)', 'Reference to (local time)', 'Evaluation from (local time)', 'Evaluation to (local time)',
    '2026-10-21T18:00', '2026-10-21T19:00', '2026-10-21T19:01', '2026-10-21T20:01');
  await visual.getByRole('button', { name: 'Analyze' }).click();
  await expect(visual.getByRole('alert')).toContainText('does not match this Screen configuration');
  await expect(visual.getByTestId('runtime-intelligence-contextual-deviation-result')).toHaveCount(0);
  expect(queries).toHaveLength(4);
});

async function setWindow(
  visual: Locator,
  referenceFromLabel: string,
  referenceToLabel: string,
  evaluationFromLabel: string,
  evaluationToLabel: string,
  referenceFrom: string,
  referenceTo: string,
  evaluationFrom: string,
  evaluationTo: string
) {
  await visual.getByLabel(referenceFromLabel).fill(referenceFrom);
  await visual.getByLabel(referenceToLabel).fill(referenceTo);
  await visual.getByLabel(evaluationFromLabel).fill(evaluationFrom);
  await visual.getByLabel(evaluationToLabel).fill(evaluationTo);
}

function runtimeProjection() {
  return {
    mode: 'engineering',
    projectKey,
    projectName: 'Contextual Deviation Runtime Test',
    revision,
    activatedAtUtc: '2026-10-21T00:00:00Z',
    package: {
      schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-10-21T00:00:00Z',
      startupScreenId: '90000000-0000-0000-0000-000000000009',
      runtimePresentation: { historicalPlaybackEnabled: true },
      tags: [], alarms: [], templates: [], equipment: [], dynamos: [], scripts: [], scriptVisualEventReferences: [], visualAssets: [], popups: [],
      screens: [{
        id: '90000000-0000-0000-0000-000000000009', key: 'contextual-deviation', name: 'Contextual deviation',
        elements: [{
          id: '90000000-0000-0000-0000-000000000010', key: 'deviation-v2', type: 'core.intelligenceContextualDeviationV2',
          properties: {
            x: 50, y: 60, width: 1250, height: 720,
            intelligenceScopeId: scopeId,
            intelligenceAssignmentId: assignmentId,
            intelligenceProfileId: profileId,
            intelligenceSignalIds: JSON.stringify(targetIds),
            intelligenceFirstContextSignalId: firstContextId,
            intelligenceFirstContextLowerInclusiveCanonical: 5,
            intelligenceFirstContextUpperExclusiveCanonical: 25,
            intelligenceSecondContextSignalId: secondContextId,
            intelligenceSecondContextLowerInclusiveCanonical: 100,
            intelligenceSecondContextUpperExclusiveCanonical: 220
          }
        }]
      }]
    }
  };
}

function signal(signalId: string, signalKey: string) {
  return {
    signalId, signalKey, tagId: `80000000-0000-0000-0000-${signalId.slice(-12)}`,
    tagPath: `Plant.Pump.${signalKey}`, engineeringUnit: 'A', controlledUnitCode: 'unit.electric_current.ampere', canonicalUnit: 'A'
  };
}

function deviationResponse(body: Record<string, any>) {
  const targets = body.targetSignalIds.map((id: string, index: number) => signal(id, index === 0 ? 'Motor current' : 'Motor power'));
  const firstContext = signal(body.firstContextSignalId, 'Motor speed');
  const secondContext = signal(body.secondContextSignalId, 'Discharge pressure');
  return {
    contractVersion: 2, catalogVersion: 2, ruleId: 'contextual-deviation-vector.v2', ruleVersion: '2.0.0',
    projectKey, activeRevision: revision, scopeKind: 0, scopeId: body.scopeId, assignmentId: body.assignmentId,
    equipmentId: body.scopeId, profileId,
    targetSignals: targets,
    contexts: [
      { signal: firstContext, lowerInclusiveCanonical: body.firstContextLowerInclusiveCanonical, upperExclusiveCanonical: body.firstContextUpperExclusiveCanonical,
        canonicalUnit: 'A', completeReferenceVectorCount: 40, referenceInRangeVectorCount: 35, completeEvaluationVectorCount: 20, evaluationInRangeVectorCount: 18 },
      { signal: secondContext, lowerInclusiveCanonical: body.secondContextLowerInclusiveCanonical, upperExclusiveCanonical: body.secondContextUpperExclusiveCanonical,
        canonicalUnit: 'A', completeReferenceVectorCount: 40, referenceInRangeVectorCount: 35, completeEvaluationVectorCount: 20, evaluationInRangeVectorCount: 18 }
    ],
    referenceWindow: body.referenceWindow, evaluationWindow: body.evaluationWindow,
    completeReferenceVectorCount: 40, jointContextReferenceVectorCount: 35, completeEvaluationVectorCount: 20,
    jointContextEvaluationVectorCount: 18, returnedEvaluationVectorCount: 1, omittedEvaluationVectorCount: 19,
    targetStatistics: targets.map((target: ReturnType<typeof signal>) => ({ signal: target, referenceMedian: 12, referenceMad: 1,
      completeReferenceVectorCount: 40, jointContextReferenceVectorCount: 35, completeEvaluationVectorCount: 20,
      returnedJointContextEvaluationValueCount: 1, status: 'available', scoreUnavailableReason: null })),
    evaluationVectors: [{
      sourceTimestampUtc: body.evaluationWindow.fromUtc,
      firstContextObservedTimestampUtc: body.evaluationWindow.fromUtc,
      firstContextValueCanonical: 10, firstContextInRange: true,
      secondContextObservedTimestampUtc: body.evaluationWindow.fromUtc,
      secondContextValueCanonical: 120, secondContextInRange: true, inJointContext: true,
      targets: targets.map((target: ReturnType<typeof signal>) => ({ targetSignalId: target.signalId, value: 14,
        rawDifferenceFromReferenceMedian: 2, robustScore: 1.3489795, scoreUnavailableReason: null }))
    }],
    totalRawObservationCount: 200,
    provenance: {
      dataset: 'historian.samples.v2', datasetContractVersion: 2,
      queryWindowTimestampBasis: 'Historian observation timestamp', vectorTimestampBasis: 'Exact source timestamp match',
      sourceTimestampEvidenceRequirement: 'Good finite synchronized', contextQualification: 'Two declared canonical ranges',
      regimeEquivalence: 'not_assessed', completeness: 'unknown',
      interpretation: 'Independent descriptive target statistics; no diagnosis.'
    }
  };
}
