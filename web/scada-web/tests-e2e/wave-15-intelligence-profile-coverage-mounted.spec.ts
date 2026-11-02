import { expect, test, type Page } from '@playwright/test';
import { currentFrequencySignals } from '../src/engineering/intelligenceCurrentFrequencySignals';

const currentFrequencyUi = {
  'pt-BR': ['Medição de corrente', 'Medição de frequência', 'Frequência mínima (Hz)', 'Frequência máxima (Hz)', 'Início do período principal (horário local)', 'Fim do período principal (horário local)', 'Início do período de comparação (horário local)', 'Fim do período de comparação (horário local)', 'Comparar corrente', 'Comparação disponível'],
  en: ['Current measurement', 'Frequency measurement', 'Minimum frequency (Hz)', 'Maximum frequency (Hz)', 'Primary period start (local time)', 'Primary period end (local time)', 'Comparison period start (local time)', 'Comparison period end (local time)', 'Compare current', 'Comparison available'],
  es: ['Medición de corriente', 'Medición de frecuencia', 'Frecuencia mínima (Hz)', 'Frecuencia máxima (Hz)', 'Inicio del período principal (hora local)', 'Fin del período principal (hora local)', 'Inicio del período de comparación (hora local)', 'Fin del período de comparación (hora local)', 'Comparar corriente', 'Comparación disponible']
};
async function prepareCurrentFrequency(page: Page, locale: keyof typeof currentFrequencyUi = 'pt-BR') {
  const panel = page.getByTestId(`intelligence-current-frequency-${assignmentId}`), labels = currentFrequencyUi[locale];
  await panel.locator('summary').click();
  await panel.getByRole('combobox', { name: labels[0], exact: true }).selectOption('15000000-0000-0000-0000-000000000211');
  await panel.getByRole('combobox', { name: labels[1], exact: true }).selectOption('15000000-0000-0000-0000-000000000212');
  for (const [index, value] of [[2,'49'],[3,'51'],[4,'2026-10-22T10:00'],[5,'2026-10-22T11:00'],[6,'2026-10-20T10:00'],[7,'2026-10-20T11:00']] as const) await panel.getByLabel(labels[index], { exact: true }).fill(value);
  return panel;
}
function currentFrequencyResponse(body: Record<string, any>) {
  const evidence = (window: any) => ({ window, current: { rawObservationCount: 5, acceptedSynchronizedSampleCount: 4, rejectedObservationCount: 1, rejectionCounts: { quality_not_good: 1 } },
    frequency: { rawObservationCount: 4, acceptedSynchronizedSampleCount: 4, rejectedObservationCount: 0, rejectionCounts: {} },
    exactSourceTimestampPairCount: 4, frequencyInRangePairCount: 3, frequencyOutOfRangePairCount: 1, selectedPairCount: 3,
    reusedSourceTimestampPairCount: 0, unmatchedCurrentAcceptedSampleCount: 0, unmatchedFrequencyAcceptedSampleCount: 0 });
  return { contractVersion: 1, catalogVersion: 1, ruleId: 'current-at-recorded-frequency.compare.v1', ruleVersion: '1.0.0',
    projectKey: 'intelligence-coverage-test', activeRevision: 4, scopeKind: 'Equipment', scopeId: equipmentId, equipmentId, assignmentId, profileId,
    currentSignal: { signalId: body.currentSignalId, tagId: currentTagId, roleCode: 'pump.electrical_current', currentBasis: 'line_rms', signalKind: null, frequencyReference: null, controlledUnitCode: 'unit.electric_current.ampere', canonicalUnit: 'A' },
    frequencySignal: { signalId: body.frequencySignalId, tagId: speedTagId, roleCode: 'pump.drive_frequency', currentBasis: null, signalKind: 'measured_output', frequencyReference: 'drive_electrical', controlledUnitCode: 'unit.frequency.hertz', canonicalUnit: 'Hz' },
    minimumFrequencyHz: body.minimumFrequencyHz, maximumFrequencyHz: body.maximumFrequencyHz, primary: evidence(body.primaryWindow), comparison: evidence(body.comparisonWindow), totalRawObservationCount: 18,
    metric: { status: 'available', reasonCodes: [] as string[], primaryMeanCurrentA: 6 as number | null, comparisonMeanCurrentA: 5 as number | null, differenceA: 1 as number | null, percentChange: 20 as number | null, percentChangeStatus: 'available', percentChangeReasonCode: null as string | null },
    provenance: { dataset: 'historian.samples.v2', datasetContractVersion: 2, physicalSimultaneity: 'unverified; exact recorded-time equality only', operatingRegimeEquivalence: 'unverified; frequency alone is insufficient', expectedCadence: 'unknown', completeness: 'unknown' } };
}

for (const locale of ['pt-BR','en','es'] as const) test(`current-frequency comparison shows named measurements, evidence and limits on mobile in ${locale}`, async ({ page }) => {
  const fixture = await installWorkspace(page, { currentFrequencyCase: 'ready' }); let calls = 0;
  await page.route('**/analytics/current-frequency-comparison/v1/query', async route => {
    calls++; const body = route.request().postDataJSON();
    expect(body).toMatchObject({ scopeKind: 0, scopeId: equipmentId, assignmentId, minimumFrequencyHz: 49, maximumFrequencyHz: 51 });
    expect(body).not.toHaveProperty('profileId');
    await route.fulfill({ json: currentFrequencyResponse(body) });
  });
  await page.setViewportSize({ width: 390, height: 844 }); await openIntelligence(page, locale);
  expect(calls).toBe(0); const panel = await prepareCurrentFrequency(page, locale);
  expect(calls).toBe(0); await panel.getByRole('button', { name: currentFrequencyUi[locale][8], exact: true }).click();
  const result = panel.getByTestId('intelligence-current-frequency-result'); await expect(result).toContainText(currentFrequencyUi[locale][9]);
  await expect(result).toContainText('6 A'); await expect(result).toContainText('5 A'); await expect(result).toContainText('1 A'); await expect(result).toContainText('20 %');
  await expect(result).toContainText('3'); expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  expect(fixture.otherWrites).toEqual([]); expect(fixture.dataReads).toEqual([]); expect(calls).toBe(1);
});

for (const variant of ['setpoint','semantic-v2','unit-v2','free-text-unit','rpm','bad-qualifier','duplicate-tag','duplicate-id','ambiguous-tag','boolean']) test(`current-frequency refuses ${variant} without a historical query`, async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: variant }); let calls = 0;
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => { calls++; return route.fulfill({ json: {} }); });
  await openIntelligence(page); const panel = page.getByTestId(`intelligence-current-frequency-${assignmentId}`); await panel.locator('summary').click();
  await expect(panel.getByRole('button', { name: 'Comparar corrente', exact: true })).toBeDisabled();
  await expect(panel.getByText(/Este perfil precisa/)).toBeVisible(); expect(calls).toBe(0);
});

test('current-frequency requires equal disjoint periods in either order, inclusive range and explicit values', async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready' }); let calls = 0;
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => { calls++; return route.fulfill({ json: currentFrequencyResponse(route.request().postDataJSON()) }); });
  await openIntelligence(page); const panel = await prepareCurrentFrequency(page), labels = currentFrequencyUi['pt-BR'];
  const submit = panel.getByRole('button', { name: 'Comparar corrente', exact: true });
  for (const [start,end] of [['2026-10-22T11:00','2026-10-22T12:00'], ['2026-10-22T10:30','2026-10-22T11:30'], ['2026-10-20T10:00','2026-10-20T12:00'], ['2026-08-01T10:00','2026-10-20T11:00']]) {
    await panel.getByLabel(labels[6]).fill(start); await panel.getByLabel(labels[7]).fill(end); await expect(submit).toBeDisabled();
  }
  expect(calls).toBe(0);
  for (const [start,end] of [['2026-10-20T10:00','2026-10-20T11:00'], ['2026-10-24T10:00','2026-10-24T11:00']]) {
    await panel.getByLabel(labels[6]).fill(start); await panel.getByLabel(labels[7]).fill(end); await submit.click(); await expect(panel.getByTestId('intelligence-current-frequency-result')).toBeVisible();
  }
  expect(calls).toBe(2); await panel.getByLabel(labels[2]).fill('52'); await expect(submit).toBeDisabled();
  await panel.getByLabel(labels[2]).fill(''); await expect(submit).toBeDisabled();
  await panel.getByLabel(labels[2]).fill('51'); await expect(submit).toBeEnabled();
  await expect(panel.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);
});

for (const variant of ['revision','assignment','tag','basis','unit','window','provenance','dataset','fabricated-zero']) test(`current-frequency discards mismatched ${variant} response`, async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready' });
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => {
    const payload = currentFrequencyResponse(route.request().postDataJSON());
    if (variant === 'revision') payload.activeRevision++;
    if (variant === 'assignment') payload.assignmentId = profileId;
    if (variant === 'tag') payload.currentSignal.tagId = speedTagId;
    if (variant === 'basis') payload.frequencySignal.frequencyReference = 'shaft_rotational';
    if (variant === 'unit') payload.currentSignal.canonicalUnit = 'mA';
    if (variant === 'window') payload.primary.window = { fromUtc: '2026-01-01T00:00:00Z', toUtc: '2026-01-01T01:00:00Z' };
    if (variant === 'provenance') payload.provenance.operatingRegimeEquivalence = 'verified';
    if (variant === 'dataset') payload.provenance.dataset = 'historian.samples';
    if (variant === 'fabricated-zero') { payload.metric.status = 'insufficient_data'; payload.metric.differenceA = 0; }
    return route.fulfill({ json: payload });
  });
  await openIntelligence(page); const panel = await prepareCurrentFrequency(page); await panel.getByRole('button', { name: 'Comparar corrente', exact: true }).click();
  await expect(panel.getByRole('alert')).toBeVisible(); await expect(panel.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);
});

test('current-frequency preserves unavailable values and explains zero comparison mean', async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready' }); let calls = 0;
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => {
    const payload = currentFrequencyResponse(route.request().postDataJSON()); calls++;
    if (calls === 1) payload.metric = { status: 'insufficient_data', reasonCodes: ['fewer_than_two_primary_frequency_conditioned_pairs'], primaryMeanCurrentA: null, comparisonMeanCurrentA: null, differenceA: null, percentChange: null, percentChangeStatus: 'unavailable', percentChangeReasonCode: 'fewer_than_two_primary_frequency_conditioned_pairs' };
    else payload.metric = { ...payload.metric, comparisonMeanCurrentA: 0, differenceA: 6, percentChange: null, percentChangeStatus: 'unsupported', percentChangeReasonCode: 'comparison_mean_zero_percent_change_unavailable' };
    return route.fulfill({ json: payload });
  });
  await openIntelligence(page); const panel = await prepareCurrentFrequency(page), submit = panel.getByRole('button', { name: 'Comparar corrente', exact: true });
  await submit.click(); const result = panel.getByTestId('intelligence-current-frequency-result'); await expect(result).toContainText('menos de dois pares');
  await expect(result.locator('dd')).toHaveText(['Não disponível','Não disponível','Não disponível','Não disponível']);
  await submit.click(); await expect(result).toContainText('percentual não pode ser calculado');
  await expect(result.locator('dd')).toHaveText(['6 A','0 A','6 A','Não disponível']);
});

test('current-frequency ignores late responses after cancel and changed parameters, then allows retry after denial', async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready' }); let calls = 0, completed = 0, release: (() => void) | undefined;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/analytics/current-frequency-comparison/v1/query', async route => {
    calls++; const body = route.request().postDataJSON();
    if (calls <= 2) await gate;
    if (calls === 3) await route.fulfill({ status: 403, json: { code: 'AUTHORIZATION_DENIED' } });
    else await route.fulfill({ json: currentFrequencyResponse(body) }).catch(() => {});
    completed++;
  });
  await openIntelligence(page); const panel = await prepareCurrentFrequency(page), submit = panel.getByRole('button', { name: 'Comparar corrente', exact: true });
  await submit.click(); await expect.poll(() => calls).toBe(1); await panel.getByRole('button', { name: 'Cancelar consulta' }).click();
  await submit.click(); await expect.poll(() => calls).toBe(2); await panel.getByLabel('Frequência máxima (Hz)').fill('52'); release?.();
  await expect.poll(() => completed).toBe(2); await expect(panel.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);
  await submit.click(); await expect(panel.getByRole('alert')).toBeVisible(); await expect(panel.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);
  await submit.click(); await expect(panel.getByTestId('intelligence-current-frequency-result')).toBeVisible();
});

test('current-frequency eligibility distinguishes shaft frequency, current basis and units without guessing from labels', async () => {
  const profile = { id: profileId, key: 'motor', name: 'Motor', signals: [
    { id: currentTagId, key: 'Current', tagId: currentTagId, required: true, signalRole: 'unrelated legacy text', semanticBinding: { catalogVersion: 1, roleCode: 'pump.electrical_current', currentBasis: 'phase_rms' } },
    { id: speedTagId, key: 'Frequency', tagId: speedTagId, required: true, signalRole: 'rpm', semanticBinding: { catalogVersion: 1, roleCode: 'pump.drive_frequency', signalKind: 'measured_output', frequencyReference: 'shaft_rotational' } }] };
  const tags = [{ id: currentTagId, name: 'Measured current', path: 'Motor.I', dataType: 'Double', engineeringUnit: 'mA', controlledEngineeringUnit: { catalogVersion: 1, code: 'unit.electric_current.ampere' }, readOnly: true },
    { id: speedTagId, name: 'Shaft', path: 'Motor.F', dataType: 'Double', engineeringUnit: 'rpm', controlledEngineeringUnit: { catalogVersion: 1, code: 'unit.frequency.hertz' }, readOnly: true }];
  expect(currentFrequencySignals(profile, tags).map(signal => [signal.kind,signal.basis])).toEqual([['current','phase_rms'],['frequency','shaft_rotational']]);
});

const equipmentId = '15000000-0000-0000-0000-000000000301';
const profileId = '15000000-0000-0000-0000-000000000201';
const assignmentId = '15000000-0000-0000-0000-000000000203';
const currentTagId = '15000000-0000-0000-0000-000000000101';
const speedTagId = '15000000-0000-0000-0000-000000000102';
const firstContextSignalId = '15000000-0000-0000-0000-000000000213';
const secondContextSignalId = '15000000-0000-0000-0000-000000000214';
const firstContextTagId = '15000000-0000-0000-0000-000000000105';
const secondContextTagId = '15000000-0000-0000-0000-000000000106';

for (const mode of ['reference', 'context'] as const) {
  test(`${mode} comparison rejects overlapping periods and ignores a cancelled late response`, async ({ page }) => {
    await installWorkspace(page, { operatingContexts: true });
    let calls = 0;
    let completed = 0;
    let release: (() => void) | undefined;
    const gate = new Promise<void>(resolve => { release = resolve; });
    const endpoint = mode === 'reference' ? 'reference-evidence' : 'context-eligibility';
    await page.route(`**/api/engineering/asset-process-intelligence/analytics/${endpoint}/v1/query`, async route => {
      calls++;
      const request = route.request().postDataJSON();
      const evidence = { rawObservationCount: 15, acceptedSynchronizedCount: 12, rejectedObservationCount: 3,
        acceptedSynchronizedUniqueCount: 12, contextEligibleObservationCount: 8, status: 'context_match_available' };
      const payload = { contractVersion: 1, projectKey: 'intelligence-coverage-test', activeRevision: 4,
        scopeId: equipmentId, profileId, assignmentId, measurementProfileId: profileId,
        operatingContextProfileId: request.operatingContextProfileId, profileVersion: 3,
        referenceWindow: request.referenceWindow, comparisonWindow: request.comparisonWindow,
        referenceCohort: { completeRequiredNumericCohortCount: 12 },
        comparisonCohort: request.comparisonWindow ? { completeRequiredNumericCohortCount: 12 } : null,
        predicates: [{ tagId: speedTagId, referenceAcceptedCount: 12, referenceMatchCount: 8, comparisonAcceptedCount: 12, comparisonMatchCount: 8, status: 'context_match_available' }],
        signals: [{ signalId: firstContextSignalId, tagId: firstContextTagId, tagPath: 'Pump.Current', unit: 'A', engineeringUnit: 'A',
          required: true, reference: evidence, comparison: request.comparisonWindow ? evidence : null }] };
      if (calls === 2) await gate;
      await route.fulfill({ json: payload }).catch(() => {});
      completed++;
    });
    await openIntelligence(page);
    const panel = page.getByTestId(mode === 'reference' ? `intelligence-reference-evidence-${profileId}` : `intelligence-context-eligibility-${assignmentId}`);
    await panel.locator('summary').click();
    if (mode === 'context') await panel.getByLabel('Contexto salvo').selectOption({ label: 'Bomba em operação' });
    await panel.getByLabel(mode === 'reference' ? 'Início da referência (horário local)' : 'Início do período (horário local)').fill('2026-10-20T10:00');
    await panel.getByLabel(mode === 'reference' ? 'Fim da referência (horário local)' : 'Fim do período (horário local)').fill('2026-10-21T10:00');
    const submit = panel.getByRole('button', { name: mode === 'reference' ? 'Consultar referência' : 'Consultar condições', exact: true });
    await panel.getByLabel('Comparar com outro período').check();
    await expect(submit).toBeDisabled();
    await panel.getByLabel('Início da comparação (horário local)').fill('2026-10-21T09:00');
    await panel.getByLabel('Fim da comparação (horário local)').fill('2026-10-22T10:00');
    await expect(submit).toBeDisabled();
    expect(calls).toBe(0);
    await panel.getByLabel('Início da comparação (horário local)').fill('2026-10-21T10:00');
    await expect(submit).toBeDisabled();
    await panel.getByLabel('Início da comparação (horário local)').fill('2026-10-21T10:01');
    await submit.click();
    const result = panel.getByTestId(mode === 'reference' ? 'intelligence-reference-evidence-result' : 'intelligence-context-eligibility-result');
    await expect(result).toContainText('Comparação');
    await panel.getByLabel('Comparar com outro período').uncheck();
    await expect(result).toHaveCount(0);
    expect(calls).toBe(1);
    await submit.click();
    await expect.poll(() => calls).toBe(2);
    await panel.getByRole('button', { name: 'Cancelar consulta' }).click();
    release?.();
    await expect.poll(() => completed).toBe(2);
    await expect(result).toHaveCount(0);
    await expect(submit).toBeEnabled();
  });
}

test('joint deviation selects named signals and rejects an unexpected decision threshold', async ({ page }) => {
  await installWorkspace(page, { jointReady: true });
  let calls = 0;
  let invalidThreshold = false;
  await page.route('**/api/engineering/asset-process-intelligence/analytics/experimental-joint-deviation/v1/query', async route => {
    calls++;
    const request = route.request().postDataJSON();
    expect(request).toMatchObject({ scopeKind: 0, scopeId: equipmentId, assignmentId, expectedProfileVersion: 3, expectedCadenceSeconds: 60 });
    expect(request.targetSignalIds).toHaveLength(2);
    await route.fulfill({ json: { contractVersion: 1, projectKey: 'intelligence-coverage-test', activeRevision: 4,
      scopeId: equipmentId, assignmentId, measurementProfileId: profileId,
      operatingContextProfileId: request.operatingContextProfileId, operatingContextProfileVersion: 3,
      expectedCadenceSeconds: 60, validationStatus: 'experimental_not_field_validated', classificationStatus: 'none',
      decisionThreshold: invalidThreshold ? 1 : null, referenceWindow: request.referenceWindow, evaluationWindow: request.evaluationWindow,
      referenceFitStatus: 'available', referenceCompleteCohortCount: 120, evaluationScoredCohortCount: 1,
      evaluationWarmupCohortCount: 24, omittedEvaluationScores: 0,
      targetSignals: request.targetSignalIds.map((signalId: string) => ({ signalId })),
      scores: [{ sourceTimestampUtc: request.evaluationWindow.fromUtc, score: 2, levelComponent: 1, relationshipComponent: 1,
        crossSignalInteraction: 0, targetContributions: request.targetSignalIds.map((signalId: string) => ({ signalId, level: 0.5, marginalVariance: 0.5 })) }] } });
  });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-joint-deviation-${assignmentId}`);
  await panel.getByText('Desvio conjunto experimental', { exact: true }).click();
  await expect(panel.getByRole('button', { name: 'Calcular desvio conjunto' })).toBeDisabled();
  await panel.getByLabel('Contexto salvo').selectOption({ label: 'Bomba em operação' });
  await panel.getByLabel('Corrente', { exact: true }).check();
  await panel.getByLabel('Velocidade', { exact: true }).check();
  await panel.getByLabel('Intervalo esperado entre leituras (segundos)').fill('60');
  await panel.getByLabel('Início da referência', { exact: true }).fill('2026-10-19T10:00');
  await panel.getByLabel('Fim da referência', { exact: true }).fill('2026-10-20T10:00');
  await panel.getByLabel('Início da avaliação', { exact: true }).fill('2026-10-20T10:00');
  await panel.getByLabel('Fim da avaliação', { exact: true }).fill('2026-10-21T10:00');
  await expect(panel.getByRole('button', { name: 'Calcular desvio conjunto' })).toBeDisabled();
  await panel.getByLabel('Início da avaliação', { exact: true }).fill('2026-10-20T10:01');
  expect(calls).toBe(0);
  await panel.getByRole('button', { name: 'Calcular desvio conjunto' }).click();
  await expect(panel.getByTestId('intelligence-joint-deviation-result')).toContainText('Referência ajustada para esta consulta');
  await expect(panel.getByRole('table')).toContainText('Componente de relação');
  await panel.getByLabel('Fim da avaliação', { exact: true }).fill('2026-10-21T11:00');
  await expect(panel.getByTestId('intelligence-joint-deviation-result')).toHaveCount(0);
  invalidThreshold = true;
  await panel.getByRole('button', { name: 'Calcular desvio conjunto' }).click();
  await expect(panel.getByRole('alert')).toBeVisible();
  await expect(panel.getByTestId('intelligence-joint-deviation-result')).toHaveCount(0);
});

test('context eligibility uses the saved version, explains mismatches and discards stale responses', async ({ page }) => {
  await installWorkspace(page, { operatingContexts: true });
  let calls = 0;
  let stale = false;
  await page.route('**/api/engineering/asset-process-intelligence/analytics/context-eligibility/v1/query', async route => {
    calls++;
    const request = route.request().postDataJSON();
    expect(request).toMatchObject({ scopeKind: 0, scopeId: equipmentId, assignmentId,
      operatingContextProfileId: '15000000-0000-0000-0000-000000000401', expectedProfileVersion: 3 });
    await route.fulfill({ json: { contractVersion: 1, projectKey: 'intelligence-coverage-test', activeRevision: 4,
      assignmentId, measurementProfileId: profileId, operatingContextProfileId: request.operatingContextProfileId,
      profileVersion: stale ? 2 : 3, referenceWindow: request.referenceWindow,
      predicates: [{ tagId: speedTagId, referenceAcceptedCount: 12, referenceMatchCount: 0, comparisonAcceptedCount: null, comparisonMatchCount: null, status: 'predicate_mismatch' }],
      signals: [{ signalId: firstContextSignalId, tagId: firstContextTagId, tagPath: 'Planta.Bomba01.Corrente', engineeringUnit: 'A',
        reference: { rawObservationCount: 15, acceptedSynchronizedUniqueCount: 12, contextEligibleObservationCount: 0,
          status: 'predicate_mismatch', rejectionCounts: {} } }] } });
  });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-context-eligibility-${assignmentId}`);
  await panel.getByText('Conferir condições do contexto', { exact: true }).click();
  await panel.getByLabel('Contexto salvo').selectOption({ label: 'Bomba em operação' });
  await panel.getByLabel('Início do período (horário local)').fill('2026-10-20T10:00');
  await panel.getByLabel('Fim do período (horário local)').fill('2026-10-21T10:00');
  expect(calls).toBe(0);
  await panel.getByRole('button', { name: 'Consultar condições', exact: true }).click();
  await expect(panel.getByTestId('intelligence-context-eligibility-result')).toContainText('As condições selecionadas não foram atendidas');
  await expect(panel.getByTestId('intelligence-context-eligibility-result')).toContainText('15 / 12 / 0');
  await expect(panel.getByTestId('intelligence-context-eligibility-result')).toContainText('Leituras aceitas / que atendem à condição: 12 / 0');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  await panel.getByLabel('Início do período (horário local)').fill('2026-10-20T11:00');
  await expect(panel.getByTestId('intelligence-context-eligibility-result')).toHaveCount(0);
  stale = true;
  await panel.getByRole('button', { name: 'Consultar condições', exact: true }).click();
  await expect(panel.getByRole('alert')).toBeVisible();
  await expect(panel.getByTestId('intelligence-context-eligibility-result')).toHaveCount(0);
  expect(calls).toBe(2);
});

test('reference evidence queries explicitly, clears changed periods and rejects another revision', async ({ page }) => {
  await installWorkspace(page);
  let requests = 0;
  let revision = 4;
  await page.route('**/api/engineering/asset-process-intelligence/analytics/reference-evidence/v1/query', async route => {
    requests++;
    const request = route.request().postDataJSON();
    expect(request).toMatchObject({ scopeKind: 0, scopeId: equipmentId, profileId });
    await route.fulfill({ json: { contractVersion: 1, projectKey: 'intelligence-coverage-test', activeRevision: revision,
      scopeId: equipmentId, profileId, referenceWindow: request.referenceWindow,
      referenceCohort: { completeRequiredNumericCohortCount: null, completeSelectedNumericCohortCount: 12,
        requiredReasons: ['required_signal_unsupported_non_numeric_tag_type'] },
      signals: [{ signalId: firstContextSignalId, tagPath: 'Pump.Current', unit: 'A', required: true,
        reference: { rawObservationCount: 15, acceptedSynchronizedCount: 12, rejectedObservationCount: 3 } }] } });
  });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-reference-evidence-${profileId}`);
  await panel.getByText('Conferir dados da referência', { exact: true }).click();
  expect(requests).toBe(0);
  await panel.getByLabel('Início da referência (horário local)').fill('2026-10-20T10:00');
  await panel.getByLabel('Fim da referência (horário local)').fill('2026-10-21T10:00');
  await panel.getByRole('button', { name: 'Consultar referência', exact: true }).click();
  await expect(panel.getByTestId('intelligence-reference-evidence-result')).toContainText('15 / 12 / 3');
  await expect(panel.getByTestId('intelligence-reference-evidence-result')).toContainText('Um sinal obrigatório não é numérico');
  await expect(panel.getByTestId('intelligence-reference-evidence-result')).toContainText('Leituras simultâneas dos sinais numéricos do perfil: 12');
  expect(requests).toBe(1);
  await panel.getByLabel('Início da referência (horário local)').fill('2026-10-20T11:00');
  await expect(panel.getByTestId('intelligence-reference-evidence-result')).toHaveCount(0);
  expect(requests).toBe(1);
  revision = 5;
  await panel.getByRole('button', { name: 'Consultar referência', exact: true }).click();
  await expect(panel.getByRole('alert')).toBeVisible();
  await expect(panel.getByTestId('intelligence-reference-evidence-result')).toHaveCount(0);
});

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
  const qualityCounts = panel.getByText('Boa: 2 · Incerta: 1');
  await expect(qualityCounts).toHaveCount(2);
  await expect(qualityCounts.first()).toBeVisible();
  await expect(panel.getByText('não comprovam que os sensores mediram no mesmo instante')).toBeVisible();
  await expect(panel.getByText(/não existe uma definição validada para combinar esses sinais em um KPI/)).toBeVisible();
  await expect.poll(() => fixture.dataReads).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);

  await from.fill('2026-10-20T09:00');
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
  expect(fixture.requests).toHaveLength(1);
});

test('Performance V1B runs only on explicit request and binds its transient result to the canonical selection', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-performance-${profileId}`);
  await expect(panel).toBeVisible();
  expect(fixture.performanceCatalogRequests).toEqual([]);
  expect(fixture.performanceRequests).toEqual([]);

  await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await expect(panel.getByLabel('Regra de desempenho')).toBeVisible();
  expect(fixture.performanceCatalogRequests).toEqual(['GET /api/engineering/asset-process-intelligence/analytics/performance/v1/catalog']);
  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByLabel('De (horário local)').fill('2026-10-20T10:00');
  await panel.getByLabel('Até (horário local)').fill('2026-10-21T11:00');
  expect(fixture.performanceRequests).toEqual([]);

  await panel.getByRole('button', { name: 'Analisar desempenho' }).click();
  await expect(panel.getByTestId('intelligence-performance-result')).toBeVisible();
  expect(fixture.performanceRequests).toHaveLength(1);
  expect(fixture.performanceRequests[0]).toMatchObject({
    scopeKind: 0, scopeId: equipmentId, assignmentId, profileId,
    ruleId: 'specific-active-energy-at-recorded-production-rate.v1',
    signalIds: ['15000000-0000-0000-0000-000000000211', '15000000-0000-0000-0000-000000000212']
  });
  expect(Date.parse(fixture.performanceRequests[0].window.fromUtc)).toBeLessThan(Date.parse(fixture.performanceRequests[0].window.toUtc));
  expect(Date.parse(fixture.performanceRequests[0].window.toUtc) - Date.parse(fixture.performanceRequests[0].window.fromUtc)).toBe(25 * 60 * 60 * 1000);
  await expect(panel.getByText('Potência ativa de entrada')).toBeVisible();
  await expect(panel.getByText('Volume de produção aprovada por tempo')).toBeVisible();
  await expect(panel.getByText(/não um diagnóstico nem uma eficiência validada em campo/)).toBeVisible();
  expect(fixture.dataReads).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);

  await panel.getByLabel('Regra de desempenho').selectOption('');
  await expect(panel.getByTestId('intelligence-performance-result')).toHaveCount(0);
  expect(fixture.performanceRequests).toHaveLength(1);

  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByLabel('De (horário local)').fill('2026-10-20T09:00');
  await expect(panel.getByTestId('intelligence-performance-result')).toHaveCount(0);
  expect(fixture.performanceRequests).toHaveLength(1);
});

test('eligible Performance V1B rule creates a canonical Runtime screen draft without querying Historian or persisting', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-performance-${profileId}`);
  await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByTestId(`intelligence-performance-create-screen-${profileId}`).click();

  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const editor = page.getByTestId('visual-editor-workspace');
  await expect(editor).toBeVisible();
  await expect(editor.getByLabel('Nome de exibição')).toHaveValue(/Desempenho registrado · Bomba P-101/);
  await expect(editor.getByTestId('visual-editor-canonical-layer').getByTestId('runtime-intelligence-performance')).toBeVisible();
  await expect(editor.getByText(/Esta referência só é interativa no Runtime ao vivo/)).toBeVisible();
  await expect(editor.getByTestId('visual-editor-apply')).toBeDisabled();
  expect(fixture.performanceCatalogRequests).toHaveLength(1);
  expect(fixture.performanceRequests).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
  expect(fixture.dataReads).toEqual([]);
});

test('two measurements can generate performance and coverage without contextual sensors', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true });
  await openIntelligence(page);
  const performance = page.getByTestId(`intelligence-performance-${profileId}`);
  await performance.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await performance.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByTestId('intelligence-dashboard-source').selectOption(`equipment-profile:${assignmentId}`);
  await expect(generator.getByRole('checkbox', { name: 'Desempenho', exact: true })).toBeChecked();
  await expect(generator.getByRole('checkbox', { name: 'Desvio contextual', exact: true })).toBeDisabled();
  await expect(generator.getByText(/Este desvio precisa de pelo menos quatro medições distintas/)).toBeVisible();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  await expect(editor.getByTestId('visual-editor-generated-drafts').getByRole('button')).toHaveCount(2);
  expect(fixture.performanceRequests).toEqual([]);
  expect(fixture.requests).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
});

test('central stage composer creates coverage and partial member analyses without configuring unavailable reports', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true, stageReady: true });
  await openIntelligence(page);
  const performance = page.getByTestId(`intelligence-performance-${profileId}`);
  await performance.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await performance.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
  await expect(generator.getByRole('checkbox', { name: 'Cobertura por equipamento', exact: true })).toBeChecked();
  await expect(generator.getByRole('checkbox', { name: 'Análises por equipamento', exact: true })).toBeChecked();
  await expect(generator.getByText(/Desempenho: Disponível · Desvio contextual: Não configurado/)).toBeVisible();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(3);
  await drafts.last().click();
  const insights = editor.getByTestId('runtime-intelligence-process-stage-insights');
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeEnabled();
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeDisabled();
  expect(fixture.performanceRequests).toEqual([]);
  expect(fixture.requests).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
});

test('guided dashboard opens four independently editable linked drafts without querying or saving', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true, guidedDashboardReady: true });
  await openIntelligence(page);

  const performance = page.getByTestId(`intelligence-performance-${profileId}`);
  await performance.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await performance.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');

  const deviation = page.getByTestId(`intelligence-contextual-deviation-${assignmentId}`);
  await deviation.locator('summary').click();
  await deviation.getByRole('checkbox').nth(0).check();
  await deviation.getByRole('checkbox').nth(1).check();
  const contextSelectors = deviation.locator('.asset-process-intelligence__deviation-contexts select');
  await contextSelectors.nth(0).selectOption(firstContextSignalId);
  await contextSelectors.nth(1).selectOption(secondContextSignalId);
  const ranges = deviation.locator('.asset-process-intelligence__deviation-contexts input');
  await expect(deviation.getByLabel('Máximo (não incluído) (kPa)', { exact: true })).toBeVisible();
  await ranges.nth(0).fill('0');
  await ranges.nth(1).fill('100');
  await ranges.nth(2).fill('0');
  await ranges.nth(3).fill('100');

  const guidedGenerate = page.getByTestId(`intelligence-guided-dashboard-assignment-${assignmentId}`);
  await ranges.nth(3).fill('1e308');
  await expect(guidedGenerate).toBeDisabled();
  await contextSelectors.nth(1).selectOption('');
  await expect(ranges.nth(2)).toHaveValue('');
  await expect(ranges.nth(3)).toHaveValue('');
  await contextSelectors.nth(1).selectOption(secondContextSignalId);
  await ranges.nth(2).fill('0');
  await ranges.nth(3).fill('100,5');
  await expect(guidedGenerate).toBeEnabled();
  await guidedGenerate.click();

  await expect(page).toHaveURL(/\/engineering\/screens$/);
  const editor = page.getByTestId('visual-editor-workspace');
  await expect(editor.getByTestId('visual-editor-generated-drafts').getByRole('button')).toHaveCount(4);
  await editor.getByTestId('visual-editor-generated-draft-intelligence-coverage-bomba-p-101-perfil-da-bomba').click();
  await expect(editor.getByTestId('runtime-intelligence-profile-coverage')).toBeVisible();
  expect(fixture.performanceCatalogRequests).toHaveLength(1);
  expect(fixture.performanceRequests).toEqual([]);
  expect(fixture.requests).toEqual([]);
  expect(fixture.dataReads).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);

  const dialogs: string[] = [];
  page.on('dialog', async dialog => { dialogs.push(dialog.message()); await dialog.dismiss(); });
  await editor.getByTestId(`visual-editor-generated-draft-intelligence-performance-bomba-p-101-perfil-da-bomba`).click();
  await expect(editor.getByTestId('runtime-intelligence-performance')).toBeVisible();
  expect(dialogs).toEqual([]);
  expect(fixture.performanceRequests).toEqual([]);
  expect(fixture.requests).toEqual([]);
  expect(fixture.dataReads).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
  let candidate: { screens: Array<{ key: string; elements?: Array<{ actions?: Array<{ targetKey?: string }> }> }> } | undefined;
  await page.route('**/api/engineering/import/json/preview', async route => {
    candidate = route.request().postDataJSON();
    await route.fulfill({ json: { canApply: true, createCount: 4, updateCount: 0, skipCount: 0, errorCount: 0, items: [] } });
  });
  await editor.getByTestId('visual-editor-preview').click();
  await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
  expect(candidate?.screens).toHaveLength(4);
  const deviationDraft = candidate!.screens.find(screen => screen.key.includes('contextual-deviation')) as any;
  const contextProperties = deviationDraft.elements.find((element: any) => element.properties.intelligenceSecondContextUpperExclusiveCanonical !== undefined).properties;
  expect(contextProperties.intelligenceFirstContextUpperExclusiveCanonical).toBe(100);
  expect(contextProperties.intelligenceSecondContextUpperExclusiveCanonical).toBe(100500);
  const keys = new Set(candidate!.screens.map(screen => screen.key));
  for (const screen of candidate!.screens) {
    for (const element of screen.elements ?? []) {
      for (const action of element.actions ?? []) if (action.targetKey) expect(keys.has(action.targetKey)).toBe(true);
    }
  }
});

test('Performance V1B fails closed on ambiguous profile roles and unsupported semantic bindings', async ({ page }) => {
  const ambiguousFixture = await installWorkspace(page, { performanceReady: true, duplicatePerformanceRole: true });
  await openIntelligence(page);
  const ambiguousPanel = page.getByTestId(`intelligence-performance-${profileId}`);
  await ambiguousPanel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  const ambiguousRule = ambiguousPanel.locator('option[value="specific-active-energy-at-recorded-production-rate.v1"]');
  await expect(ambiguousRule).toHaveAttribute('disabled', '');
  await expect(ambiguousRule).toContainText('Vários sinais do perfil correspondem a um papel obrigatório');
  expect(ambiguousFixture.performanceRequests).toEqual([]);

  const unsupportedPage = await page.context().newPage();
  const unsupportedFixture = await installWorkspace(unsupportedPage, { performanceReady: true, semanticCatalogAvailable: false });
  await openIntelligence(unsupportedPage);
  const unsupportedPanel = unsupportedPage.getByTestId(`intelligence-performance-${profileId}`);
  await unsupportedPanel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  const unsupportedRule = unsupportedPanel.locator('option[value="specific-active-energy-at-recorded-production-rate.v1"]');
  await expect(unsupportedRule).toHaveAttribute('disabled', '');
  await expect(unsupportedRule).toContainText('A semântica tipada ou os qualificadores canônicos não correspondem');
  expect(unsupportedFixture.performanceRequests).toEqual([]);

  const unsupportedUnitPage = await page.context().newPage();
  const unsupportedUnitFixture = await installWorkspace(unsupportedUnitPage, { performanceReady: true, unsupportedPerformanceUnit: true });
  await openIntelligence(unsupportedUnitPage);
  const unsupportedUnitPanel = unsupportedUnitPage.getByTestId(`intelligence-performance-${profileId}`);
  await unsupportedUnitPanel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  const unsupportedUnitRule = unsupportedUnitPanel.locator('option[value="specific-active-energy-at-recorded-production-rate.v1"]');
  await expect(unsupportedUnitRule).toHaveAttribute('disabled', '');
  await expect(unsupportedUnitRule).toContainText('A unidade da TAG está ausente ou não é aceita pela regra do catálogo');
  expect(unsupportedUnitFixture.performanceRequests).toEqual([]);
});

test('Performance V1B matches the pump flow unit version/code pair accepted by the server', async ({ page }) => {
  const validPage = await page.context().newPage();
  await installWorkspace(validPage, { pumpPerformanceReady: true });
  await openIntelligence(validPage);
  const validPanel = validPage.getByTestId(`intelligence-performance-${profileId}`);
  await validPanel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  const pumpRule = validPanel.locator('option[value="recorded-differential-pressure-power.v1"]');
  await expect(pumpRule).not.toHaveAttribute('disabled', '');

  const mismatchedPage = await page.context().newPage();
  const fixture = await installWorkspace(mismatchedPage, { pumpPerformanceReady: true, mismatchedPumpFlowUnitPair: true });
  await openIntelligence(mismatchedPage);
  const mismatchedPanel = mismatchedPage.getByTestId(`intelligence-performance-${profileId}`);
  await mismatchedPanel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  const mismatchedRule = mismatchedPanel.locator('option[value="recorded-differential-pressure-power.v1"]');
  await expect(mismatchedRule).toHaveAttribute('disabled', '');
  await expect(mismatchedRule).toContainText('A unidade da TAG está ausente ou não é aceita pela regra do catálogo');
  expect(fixture.performanceRequests).toEqual([]);
});

for (const scenario of [
  { name: 'without Intelligence entitlement', options: { licensed: false } },
  { name: 'without TrendUse', options: { trendUse: false } },
  { name: 'with an inactive Equipment scope', options: { active: false } },
  { name: 'without an armed execution permit', options: { armed: false } },
  { name: 'with unpublished Engineering changes', options: { workspaceDirty: true } }
]) {
  test(`Performance V1B remains blocked ${scenario.name}`, async ({ page }) => {
    const fixture = await installWorkspace(page, { ...scenario.options, performanceReady: true });
    await openIntelligence(page);
    const panel = page.getByTestId(`intelligence-performance-${profileId}`);
    await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
    await expect(panel.getByText('A análise exige licença válida', { exact: false })).toBeVisible();
    await expect(panel.getByLabel('Regra de desempenho')).toBeDisabled();
    await expect(panel.getByRole('button', { name: 'Analisar desempenho' })).toBeDisabled();
    expect(fixture.performanceRequests).toEqual([]);
  });
}

test('Performance V1B rejects a response tied to a different project revision', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true, mismatchedPerformanceRevision: true });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-performance-${profileId}`);
  await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByRole('button', { name: 'Analisar desempenho' }).click();
  await expect(panel.getByRole('alert')).toContainText('A resposta não corresponde à regra, associação, perfil, revisão do projeto ou janela selecionada');
  await expect(panel.getByTestId('intelligence-performance-result')).toHaveCount(0);
  expect(fixture.performanceRequests).toHaveLength(1);
});

test('Performance V1B enforces the catalog window maximum before dispatch', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-performance-${profileId}`);
  await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByLabel('De (horário local)').fill('2026-09-01T00:00');
  await panel.getByLabel('Até (horário local)').fill('2026-10-21T00:00');
  await expect(panel.getByRole('alert')).toContainText('não pode exceder 31 dias');
  await expect(panel.getByRole('button', { name: 'Analisar desempenho' })).toBeDisabled();
  expect(fixture.performanceRequests).toEqual([]);
});

test('Performance V1B clears prior output and reports a bounded server-query error', async ({ page }) => {
  const fixture = await installWorkspace(page, { performanceReady: true, performanceFailCode: 'INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE' });
  await openIntelligence(page);
  const panel = page.getByTestId(`intelligence-performance-${profileId}`);
  await panel.getByRole('button', { name: 'Analisar desempenho registrado' }).click();
  await panel.getByLabel('Regra de desempenho').selectOption('specific-active-energy-at-recorded-production-rate.v1');
  await panel.getByLabel('De (horário local)').fill('2026-10-20T10:00');
  await panel.getByLabel('Até (horário local)').fill('2026-10-21T11:00');
  await panel.getByRole('button', { name: 'Analisar desempenho' }).click();
  await expect(panel.getByRole('alert')).toContainText('Há registros demais nesse período para uma análise. Escolha um intervalo menor.');
  await expect(panel.getByTestId('intelligence-performance-result')).toHaveCount(0);
  expect(fixture.performanceRequests).toHaveLength(1);
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
  await expect(panel.getByText('Não informado', { exact: true }).first()).toBeVisible();
  await expect(panel).not.toContainText('desconhecido');
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
  await expect(panel.getByRole('alert')).toContainText('O período tem amostras demais para uma única análise. Escolha um intervalo menor.');
  expect(fixture.requests).toHaveLength(1);
  await expect(panel.getByTestId('intelligence-profile-coverage-result')).toHaveCount(0);
});

async function openIntelligence(page: Page, locale = 'pt-BR') {
  await page.addInitScript(value => window.localStorage.setItem('elitescada.engineering.locale', value), locale);
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
  performanceReady?: boolean;
  guidedDashboardReady?: boolean;
  stageReady?: boolean;
  pumpPerformanceReady?: boolean;
  duplicatePerformanceRole?: boolean;
  mismatchedPerformanceRevision?: boolean;
  semanticCatalogAvailable?: boolean;
  unsupportedPerformanceUnit?: boolean;
  mismatchedPumpFlowUnitPair?: boolean;
  performanceFailCode?: string;
  operatingContexts?: boolean;
  jointReady?: boolean;
  currentFrequencyCase?: string;
} = {}) {
  const requests: Array<Record<string, any>> = [];
  const currentFrequencyRequests: Array<Record<string, any>> = [];
  const performanceCatalogRequests: string[] = [];
  const performanceRequests: Array<Record<string, any>> = [];
  const dataReads: string[] = [];
  const otherWrites: string[] = [];
  const performanceSignals = options.pumpPerformanceReady ? [
    { id: '15000000-0000-0000-0000-000000000211', key: 'Vazão de descarga', signalRole: 'processo', tagId: currentTagId, required: true,
      semanticBinding: { catalogVersion: 1, roleCode: 'pump.discharge_volumetric_flow', flowDirection: 'pump_to_process' } },
    { id: '15000000-0000-0000-0000-000000000212', key: 'Pressão diferencial direta', signalRole: 'processo', tagId: speedTagId, required: true,
      semanticBinding: { catalogVersion: 2, roleCode: 'pump.direct_differential_pressure', signalKind: 'measured_output', pressureReference: 'differential_outlet_minus_inlet' } }
  ] : options.performanceReady ? [
    { id: '15000000-0000-0000-0000-000000000211', key: 'Potência ativa', signalRole: 'elétrico', tagId: currentTagId, required: true,
      semanticBinding: { catalogVersion: 2, roleCode: 'equipment.active_input_power', signalKind: 'measured_total_input' } },
    { id: '15000000-0000-0000-0000-000000000212', key: 'Produção boa', signalRole: 'processo', tagId: speedTagId, required: true,
      semanticBinding: { catalogVersion: 2, roleCode: 'process.net_good_production_volume_rate', signalKind: 'measured_output' } },
    ...(options.duplicatePerformanceRole ? [{ id: '15000000-0000-0000-0000-000000000215', key: 'Potência duplicada', signalRole: 'elétrico', tagId: '15000000-0000-0000-0000-000000000103', required: true,
      semanticBinding: { catalogVersion: 2, roleCode: 'equipment.active_input_power', signalKind: 'measured_total_input' } }] : []),
    ...(options.guidedDashboardReady ? [
      { id: firstContextSignalId, key: 'Temperatura contextual', signalRole: 'processo', tagId: firstContextTagId, required: true },
      { id: secondContextSignalId, key: 'Pressão contextual', signalRole: 'processo', tagId: secondContextTagId, required: true }
    ] : [])
  ] : [
    { id: '15000000-0000-0000-0000-000000000211', key: 'Corrente', signalRole: 'eléctrico', tagId: currentTagId, required: true },
    { id: '15000000-0000-0000-0000-000000000212', key: 'Velocidade', signalRole: 'mecânico', tagId: speedTagId, required: true }
  ];
  const profile = {
    id: profileId,
    key: 'pump-drive',
    name: 'Perfil da bomba',
    signals: [...performanceSignals, ...(options.jointReady ? [{ id: firstContextSignalId, key: 'Temperatura', signalRole: 'processo', tagId: firstContextTagId, required: true }] : [])]
  };
  const tags = [
    { id: currentTagId,
      name: options.pumpPerformanceReady ? 'Vazão de descarga' : options.performanceReady ? 'Potência ativa' : 'Corrente',
      path: options.pumpPerformanceReady ? 'Planta.Bomba01.VazaoDescarga' : options.performanceReady ? 'Planta.Bomba01.PotenciaAtiva' : 'Planta.Bomba01.Corrente',
      dataType: 'Double', engineeringUnit: options.pumpPerformanceReady ? 'm³/s' : options.performanceReady ? 'W' : 'A', readOnly: true,
      controlledEngineeringUnit: options.pumpPerformanceReady
        ? { catalogVersion: options.mismatchedPumpFlowUnitPair ? 1 : 2, code: 'unit.volumetric_flow.cubic_metre_per_second' }
        : { catalogVersion: 2, code: options.unsupportedPerformanceUnit ? 'unit.pressure.pascal' : 'unit.power.watt' } },
    { id: speedTagId,
      name: options.pumpPerformanceReady ? 'Pressão diferencial direta' : options.performanceReady ? 'Produção boa' : 'Velocidade',
      path: options.pumpPerformanceReady ? 'Planta.Bomba01.PressaoDiferencial' : options.performanceReady ? 'Planta.Bomba01.ProducaoBoa' : 'Planta.Bomba01.Velocidade',
      dataType: 'Double', engineeringUnit: options.pumpPerformanceReady ? 'Pa' : options.performanceReady ? 'm³/s' : 'rpm', readOnly: true,
      controlledEngineeringUnit: options.pumpPerformanceReady
        ? { catalogVersion: 2, code: 'unit.pressure.pascal' }
        : { catalogVersion: 2, code: 'unit.volumetric_flow.cubic_metre_per_second' } },
    ...(options.jointReady ? [{ id: firstContextTagId, name: 'Temperatura', path: 'Planta.Bomba01.Temperatura', dataType: 'Double', engineeringUnit: '°C', readOnly: true }] : []),
    ...(options.duplicatePerformanceRole ? [{ id: '15000000-0000-0000-0000-000000000103', name: 'Potência duplicada', path: 'Planta.Bomba01.Potencia2', dataType: 'Double', engineeringUnit: 'W', readOnly: true,
      controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.power.watt' } }] : []),
    ...(options.guidedDashboardReady ? [
      { id: firstContextTagId, name: 'Temperatura contextual', path: 'Planta.Bomba01.Temperatura', dataType: 'Double', engineeringUnit: '°C', readOnly: true,
        controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.temperature.degree_celsius' } },
      { id: secondContextTagId, name: 'Pressão contextual', path: 'Planta.Bomba01.Pressao', dataType: 'Double', engineeringUnit: 'kPa', readOnly: true,
        controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.pressure.kilopascal' } }
    ] : [])
  ];
  if (options.currentFrequencyCase) {
    const signals = profile.signals as any[], sourceTags = tags as any[];
    signals.splice(0, signals.length,
      { id: '15000000-0000-0000-0000-000000000211', key: 'Corrente do motor', signalRole: 'elétrico', tagId: currentTagId, required: true,
        semanticBinding: { catalogVersion: 1, roleCode: 'pump.electrical_current', currentBasis: 'line_rms' } },
      { id: '15000000-0000-0000-0000-000000000212', key: 'Frequência do inversor', signalRole: 'elétrico', tagId: speedTagId, required: true,
        semanticBinding: { catalogVersion: 1, roleCode: 'pump.drive_frequency', signalKind: 'measured_output', frequencyReference: options.currentFrequencyCase === 'shaft' ? 'shaft_rotational' : 'drive_electrical' } });
    sourceTags[0] = { ...sourceTags[0], name: 'Corrente do motor', engineeringUnit: 'A', controlledEngineeringUnit: { catalogVersion: 1, code: 'unit.electric_current.ampere' } };
    sourceTags[1] = { ...sourceTags[1], name: 'Frequência do inversor', engineeringUnit: 'Hz', controlledEngineeringUnit: { catalogVersion: 1, code: 'unit.frequency.hertz' } };
    if (options.currentFrequencyCase === 'setpoint') signals[1].semanticBinding.signalKind = 'command_setpoint';
    if (options.currentFrequencyCase === 'semantic-v2') signals[1].semanticBinding.catalogVersion = 2;
    if (options.currentFrequencyCase === 'unit-v2') sourceTags[1].controlledEngineeringUnit.catalogVersion = 2;
    if (options.currentFrequencyCase === 'free-text-unit') sourceTags[0].controlledEngineeringUnit = null;
    if (options.currentFrequencyCase === 'rpm') sourceTags[1].controlledEngineeringUnit.code = 'unit.angular_velocity.revolutions_per_minute';
    if (options.currentFrequencyCase === 'bad-qualifier') signals[0].semanticBinding.pressureReference = 'gauge';
    if (options.currentFrequencyCase === 'duplicate-tag') signals[1].tagId = currentTagId;
    if (options.currentFrequencyCase === 'duplicate-id') signals[1].id = signals[0].id;
    if (options.currentFrequencyCase === 'ambiguous-tag') sourceTags.push({ ...sourceTags[0] });
    if (options.currentFrequencyCase === 'boolean') sourceTags[0].dataType = 'Boolean';
  }
  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (path === '/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/query') currentFrequencyRequests.push(request.postDataJSON());
    if (path === '/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog') {
      performanceCatalogRequests.push(`${request.method()} ${path}`);
      return route.fulfill({ json: performanceCatalog() });
    }
    if (path === '/api/engineering/asset-process-intelligence/analytics/performance/v1/query') {
      const body = request.postDataJSON() as Record<string, any>;
      performanceRequests.push(body);
      if (options.performanceFailCode) return route.fulfill({ status: 413, json: { code: options.performanceFailCode } });
      return route.fulfill({ json: performanceResponse(body, options.mismatchedPerformanceRevision === true) });
    }
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
      semanticCatalogs: [{ catalogVersion: 1, roles: options.pumpPerformanceReady && options.semanticCatalogAvailable !== false ? [
        { code: 'pump.discharge_volumetric_flow', dimension: 'volumetric_flow', qualifiers: [{ name: 'flowDirection', allowedValues: ['pump_to_process', 'process_to_pump', 'bidirectional_signed', 'bidirectional_magnitude'] }] }
      ] : [] }, { catalogVersion: 2, roles: (options.performanceReady || options.pumpPerformanceReady) && options.semanticCatalogAvailable !== false ? [
        { code: 'equipment.active_input_power', dimension: 'power', qualifiers: [{ name: 'signalKind', allowedValues: ['measured_total_input'] }] },
        { code: 'process.net_good_production_volume_rate', dimension: 'volumetric_flow', qualifiers: [{ name: 'signalKind', allowedValues: ['measured_output'] }] },
        { code: 'pump.direct_differential_pressure', dimension: 'pressure', qualifiers: [
          { name: 'signalKind', allowedValues: ['measured_output'] },
          { name: 'pressureReference', allowedValues: ['differential_outlet_minus_inlet'] }
        ] }
      ] : [] }],
      controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: options.guidedDashboardReady ? [
        { code: 'unit.temperature.degree_celsius', dimension: 'temperature', canonicalUnit: '°C', scaleToCanonical: 1 },
        { code: 'unit.pressure.kilopascal', dimension: 'pressure', canonicalUnit: 'Pa', scaleToCanonical: 1000 }
      ] : [] }]
    } });
    if (path === '/api/engineering/asset-process-intelligence/scopes') return route.fulfill({ json: {
      configuration: {
        version: 2,
        scopes: [{ kind: 'Equipment', scopeId: equipmentId, active: options.active !== false, executionArmed: options.armed !== false }],
        profiles: [profile], assignments: [{ id: assignmentId, equipmentId, profileId }], processStages: options.stageReady ? [{ id: '15000000-0000-0000-0000-000000000204', key: 'stage-test', name: 'Etapa de teste', order: 0, links: [{ assignmentId, order: 0 }] }] : [],
        operatingContextProfiles: options.operatingContexts || options.jointReady ? [{ id: '15000000-0000-0000-0000-000000000401',
          key: 'running', displayLabel: 'Bomba em operação', assignmentId, version: 3,
          predicates: [{ tagId: options.jointReady ? firstContextTagId : speedTagId, kind: 'NumericInterval', minimum: 1000, maximum: 1800 }] }] : []
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
  return { requests, currentFrequencyRequests, performanceCatalogRequests, performanceRequests, dataReads, otherWrites };
}

function performanceCatalog() {
  return {
    contractVersion: 1, catalogVersion: 1, ruleVersion: '1.0.0', dataset: 'historian.samples',
    datasetContractVersion: 2, maximumWindowDays: 31, maximumRawRowsAcrossSelectedTags: 50000,
    rules: [{
      id: 'specific-active-energy-at-recorded-production-rate.v1', version: '1.0.0', kind: 'derived',
      formula: 'active_input_power_W / net_good_production_rate_m3_s / 3,600,000',
      inputRoles: ['equipment.active_input_power', 'process.net_good_production_volume_rate|process.net_good_production_mass_rate|process.net_good_production_item_rate'],
      inputUnits: [
        { role: 'equipment.active_input_power', canonicalUnit: 'W', unitCatalogVersions: [2], acceptedUnitCodes: ['unit.power.watt', 'unit.power.kilowatt'] },
        { role: 'process.net_good_production_volume_rate', canonicalUnit: 'm³/s', unitCatalogVersions: [2], acceptedUnitCodes: ['unit.volumetric_flow.cubic_metre_per_second'] }
      ],
      outputUnit: 'role-specific',
      outputUnitVariants: [{ productionRole: 'process.net_good_production_volume_rate', canonicalInputUnit: 'm³/s', outputUnit: 'kWh/m³' }],
      temporalRequirement: 'Exact equality of distinct synchronized source timestamps; no interpolation or assumed cadence.',
      minimumQualifiedTuples: 2, bounds: 'Power >= 0 W; production rate > 0.', unavailableStates: ['fewer_than_two_qualified_tuples']
    }, {
      id: 'recorded-differential-pressure-power.v1', version: '1.0.0', kind: 'derived',
      formula: 'flow_m3_per_second * direct_differential_pressure_Pa = pressure-only W',
      inputRoles: ['pump.discharge_volumetric_flow', 'pump.direct_differential_pressure'],
      inputUnits: [
        { role: 'pump.discharge_volumetric_flow', canonicalUnit: 'm³/s', unitCatalogVersions: [1, 2], acceptedUnitCodes: ['unit.volumetric_flow.cubic_metre_per_hour', 'unit.volumetric_flow.cubic_metre_per_second'] },
        { role: 'pump.direct_differential_pressure', canonicalUnit: 'Pa', unitCatalogVersions: [1, 2], acceptedUnitCodes: ['unit.pressure.bar', 'unit.pressure.pascal', 'unit.pressure.kilopascal'] }
      ],
      outputUnit: 'W', outputUnitVariants: [],
      temporalRequirement: 'Exact equality of distinct synchronized source timestamps; no interpolation or assumed cadence.',
      minimumQualifiedTuples: 2, bounds: 'Pump-to-process flow > 0 m³/s; differential pressure >= 0 Pa.',
      unavailableStates: ['fewer_than_two_qualified_tuples']
    }]
  };
}

function performanceResponse(body: Record<string, any>, mismatchedRevision: boolean) {
  return {
    contractVersion: 1, catalogVersion: 1, ruleId: body.ruleId, ruleVersion: '1.0.0',
    projectKey: 'intelligence-coverage-test', activeRevision: mismatchedRevision ? 5 : 4,
    scopeKind: 'Equipment', scopeId: body.scopeId, assignmentId: body.assignmentId, equipmentId: body.scopeId, profileId: body.profileId,
    window: body.window,
    signals: [
      { signalId: '15000000-0000-0000-0000-000000000211', signalKey: 'Potência ativa', roleCode: 'equipment.active_input_power', canonicalUnit: 'W' },
      { signalId: '15000000-0000-0000-0000-000000000212', signalKey: 'Produção boa', roleCode: 'process.net_good_production_volume_rate', canonicalUnit: 'm³/s' }
    ],
    inputs: [
      { signalId: '15000000-0000-0000-0000-000000000211', rawObservationCount: 10, eligibleSynchronizedCount: 8, pairedCount: 4, qualifiedCount: 3, qualityCounts: { Good: 8 }, rejectionCounts: { Uncertain: 2 } },
      { signalId: '15000000-0000-0000-0000-000000000212', rawObservationCount: 10, eligibleSynchronizedCount: 8, pairedCount: 4, qualifiedCount: 3, qualityCounts: { Good: 8 }, rejectionCounts: { Uncertain: 2 } }
    ],
    totalRawObservationCount: 20,
    summary: { status: 'available', unit: 'kWh/m³', reasonCodes: [], qualifiedTupleCount: 3, minimum: 1.2, maximum: 1.8, arithmeticMean: 1.5, meanSemantics: 'arithmetic mean over qualified tuples' },
    provenance: { dataset: 'historian.samples', datasetContractVersion: 2,
      timestampPairing: 'exact_synchronized_source_timestamp', acquisitionAlignment: 'unverified',
      operatingRegimeEquivalence: 'unverified', expectedCadence: 'unknown', completeness: 'unknown',
      measurementInterpretation: 'derived from recorded active input power and recorded net-good production rate' }
  };
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

test('central composer adds the reference report without analytical reads and authoring cannot query it', async ({ page }) => {
  const fixture = await installWorkspace(page);
  await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByRole('checkbox', { name: 'Conferir dados da referência', exact: true }).check();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(2);
  await drafts.last().click();
  const report = editor.getByTestId('runtime-intelligence-reference-evidence');
  await expect(report).toBeVisible();
  await expect(report.getByRole('button', { name: 'Consultar referência', exact: true, includeHidden: true })).toBeDisabled();
  expect(fixture.requests).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
});

test('stage composer creates reference-only member reports independently of performance and deviation', async ({ page }) => {
  const fixture = await installWorkspace(page, { stageReady: true });
  await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
  await expect(generator.getByRole('checkbox', { name: 'Análises por equipamento', exact: true })).toBeDisabled();
  await generator.getByRole('checkbox', { name: 'Dados da referência por equipamento', exact: true }).check();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(3);
  await drafts.last().click();
  const insights = editor.getByTestId('runtime-intelligence-process-stage-insights');
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-reference')).toBeVisible();
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeDisabled();
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-deviation')).toBeDisabled();
  expect(fixture.requests).toEqual([]);
  expect(fixture.otherWrites).toEqual([]);
});

test('composer offers saved context conditions independently and keeps authoring read-only', async ({ page }) => {
  const fixture = await installWorkspace(page, { operatingContexts: true });
  await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByRole('checkbox', { name: 'Condições de operação', exact: true }).check();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(2); await drafts.last().click();
  const report = editor.getByTestId('runtime-intelligence-context-eligibility');
  await expect(report).toBeVisible();
  await expect(report.getByRole('button', { name: 'Consultar condições', exact: true, includeHidden: true })).toBeDisabled();
  expect(fixture.requests).toEqual([]); expect(fixture.otherWrites).toEqual([]);
});

test('composer explains missing saved context without blocking the basic dashboard', async ({ page }) => {
  await installWorkspace(page); await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await expect(generator.getByRole('checkbox', { name: 'Condições de operação', exact: true })).toBeDisabled();
  await expect(generator.getByText(/Salve um contexto operacional/)).toBeVisible();
  await expect(generator.getByTestId('intelligence-dashboard-generate')).toBeEnabled();
});


test('stage composer generates saved conditions alone without requiring other member analyses', async ({ page }) => {
  const fixture = await installWorkspace(page, { stageReady: true, operatingContexts: true });
  await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
  await expect(generator.getByRole('checkbox', { name: 'Análises por equipamento', exact: true })).toBeDisabled();
  await generator.getByRole('checkbox', { name: 'Condições de operação por equipamento', exact: true }).check();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace');
  const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(3); await drafts.last().click();
  const insights = editor.getByTestId('runtime-intelligence-process-stage-insights');
  // Authoring keeps the canvas inert; inspect persisted choices without performing analytical reads.
  await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-context')).toBeEnabled();
  expect(fixture.requests).toEqual([]); expect(fixture.otherWrites).toEqual([]);
});

test('stage composer explains contexts absent from every member without blocking other views', async ({ page }) => {
  await installWorkspace(page, { stageReady: true }); await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
  await expect(generator.getByRole('checkbox', { name: 'Condições de operação por equipamento', exact: true })).toBeDisabled();
  await expect(generator.getByText(/Salve um contexto na associação de pelo menos um/)).toBeVisible();
  await expect(generator.getByTestId('intelligence-dashboard-generate')).toBeEnabled();
});


test('composer generates experimental joint deviation independently with named references and no authoring query', async ({ page }) => {
  const fixture = await installWorkspace(page, { jointReady: true }); await openIntelligence(page);
  const generator = page.getByTestId('intelligence-dashboard-generator');
  await generator.getByRole('checkbox', { name: 'Desvio conjunto experimental', exact: true }).check();
  await generator.getByTestId('intelligence-dashboard-generate').click();
  const editor = page.getByTestId('visual-editor-workspace'); const drafts = editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
  await expect(drafts).toHaveCount(2); await drafts.last().click();
  const report = editor.getByTestId('runtime-intelligence-joint-deviation'); await expect(report).toBeVisible();
  await expect(report.getByRole('button', { name: 'Calcular desvio conjunto', exact: true, includeHidden: true })).toBeDisabled();
  expect(fixture.requests).toEqual([]); expect(fixture.otherWrites).toEqual([]);
});


test('stage composer generates joint-only member analyses independently of context conditions',async({page})=>{
 const fixture=await installWorkspace(page,{stageReady:true,jointReady:true});await openIntelligence(page);
 const generator=page.getByTestId('intelligence-dashboard-generator');await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
 await generator.getByRole('checkbox',{name:'Desvio conjunto experimental por equipamento',exact:true}).check();
 await expect(generator.getByRole('checkbox',{name:'Condições de operação por equipamento',exact:true})).not.toBeChecked();
 await generator.getByTestId('intelligence-dashboard-generate').click();const editor=page.getByTestId('visual-editor-workspace');
 const drafts=editor.getByTestId('visual-editor-generated-drafts').getByRole('button');await expect(drafts).toHaveCount(3);await drafts.last().click();
 const insights=editor.getByTestId('runtime-intelligence-process-stage-insights');await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-joint')).toBeEnabled();
 await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-context')).toBeDisabled();
 await expect(insights.getByTestId('runtime-intelligence-process-stage-insights-performance')).toBeDisabled();
 expect(fixture.requests).toEqual([]);expect(fixture.otherWrites).toEqual([]);
});

test('stage composer explains missing joint requirements while keeping other views available',async({page})=>{
 await installWorkspace(page,{stageReady:true,operatingContexts:true});await openIntelligence(page);
 const generator=page.getByTestId('intelligence-dashboard-generator');await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
 await expect(generator.getByRole('checkbox',{name:'Desvio conjunto experimental por equipamento',exact:true})).toBeDisabled();
 await expect(generator.getByText(/Ao menos um equipamento precisa/)).toBeVisible();await expect(generator.getByTestId('intelligence-dashboard-generate')).toBeEnabled();
});


test('Intelligence readiness status wraps within a mobile viewport with enlarged text',async({page})=>{
 await installWorkspace(page,{operatingContexts:true});await openIntelligence(page);
 await page.addStyleTag({content:'.asset-process-intelligence .eng-section-meta span { font-size:14px; }'});
 await page.setViewportSize({width:390,height:844});
 const geometry=await page.evaluate(()=>({viewport:document.documentElement.clientWidth,document:document.documentElement.scrollWidth,
  status:document.querySelector('.asset-process-intelligence .eng-section-meta')!.getBoundingClientRect().toJSON()}));
 await test.info().attach('intelligence-mobile-status-geometry',{body:JSON.stringify(geometry,null,2),contentType:'application/json'});
 expect(geometry.document).toBeLessThanOrEqual(geometry.viewport);
 expect(geometry.status.right).toBeLessThanOrEqual(geometry.viewport);
 await expect(page.getByTestId('intelligence-dashboard-generator')).toBeVisible();
});

for (const reason of ['license','trend','inactive','permit','dirty'] as const) test(`current-frequency does not query when blocked by ${reason}`, async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready', licensed: reason !== 'license', trendUse: reason !== 'trend', active: reason !== 'inactive', armed: reason !== 'permit', workspaceDirty: reason === 'dirty' });
  let calls = 0;
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => { calls++; return route.fulfill({ json: {} }); });
  await openIntelligence(page); const panel = page.getByTestId(`intelligence-current-frequency-${assignmentId}`); await panel.locator('summary').click();
  await expect(panel.getByRole('button', { name: 'Comparar corrente', exact: true })).toBeDisabled();
  await expect(panel.getByRole('combobox', { name: 'Medição de corrente', exact: true })).toBeDisabled();
  expect(calls).toBe(0);
});

for (const [code,status,message] of [
  ['INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE',413,'50.000 leituras brutas'],
  ['INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE',404,'fonte ativa do Runtime'],
  ['INTELLIGENCE_CURRENT_FREQUENCY_UNITS_UNSUPPORTED',400,'unidades controladas'],
  ['AUTHORIZATION_DENIED',403,'permissões de tendência']
] as const) test(`current-frequency explains ${code} without displaying a result`, async ({ page }) => {
  await installWorkspace(page, { currentFrequencyCase: 'ready' });
  await page.route('**/analytics/current-frequency-comparison/v1/query', route => route.fulfill({ status, json: { code } }));
  await openIntelligence(page); const panel = await prepareCurrentFrequency(page); await panel.getByRole('button', { name: 'Comparar corrente', exact: true }).click();
  await expect(panel.getByRole('alert')).toContainText(message); await expect(panel.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);
});

for(const stage of [false,true]) test(`current-frequency composer creates ${stage?'stage member':'profile'} view without querying or persisting`,async({page})=>{
 const fixture=await installWorkspace(page,{currentFrequencyCase:'ready',stageReady:stage});await openIntelligence(page);
 const generator=page.getByTestId('intelligence-dashboard-generator');
 if(stage)await generator.getByTestId('intelligence-dashboard-source').selectOption('process-stage:15000000-0000-0000-0000-000000000204');
 await generator.getByRole('checkbox',{name:stage?'Corrente e frequência por equipamento':'Corrente em frequência semelhante',exact:true}).check();
 await generator.getByTestId('intelligence-dashboard-generate').click();const editor=page.getByTestId('visual-editor-workspace'),drafts=editor.getByTestId('visual-editor-generated-drafts').getByRole('button');
 await expect(drafts).toHaveCount(stage?3:2);if(stage)await drafts.last().click();else await drafts.filter({hasText:'Corrente em frequência semelhante'}).click();
 if(stage)await expect(editor.getByTestId('runtime-intelligence-process-stage-insights-current-frequency')).toBeEnabled();
 else {
 const visual=editor.getByTestId('runtime-intelligence-current-frequency');await expect(visual.getByRole('button',{name:/^(Comparar corrente|Compare current|Comparar corriente)$/,includeHidden:true})).toBeDisabled();
 await expect(visual.getByText(/No editor e na reprodução histórica|editor and historical playback|editor y reproducción histórica/)).toBeVisible();
 }
 expect(fixture.requests).toEqual([]);expect(fixture.currentFrequencyRequests).toEqual([]);expect(fixture.performanceRequests).toEqual([]);expect(fixture.otherWrites).toEqual([]);
});

test('profile coverage groups by an explicit supported interval and clears the old result when it changes',async({page})=>{
 const fixture=await installWorkspace(page);await openIntelligence(page);const panel=page.getByTestId(`intelligence-profile-coverage-${profileId}`);
 await panel.getByRole('button',{name:'Revisar cobertura histórica',exact:true}).click();const interval=panel.getByRole('combobox',{name:'Agrupar leituras a cada',exact:true});
 await interval.selectOption('3600');expect(fixture.requests).toHaveLength(0);
 await panel.getByRole('button',{name:'Analisar cobertura',exact:true}).click();await expect.poll(()=>fixture.requests.length).toBe(1);expect(fixture.requests[0].bucketSeconds).toBe(3600);
 await interval.selectOption('1800');expect(fixture.requests).toHaveLength(1);
});
