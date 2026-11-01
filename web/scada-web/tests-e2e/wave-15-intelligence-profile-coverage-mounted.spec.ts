import { expect, test, type Page } from '@playwright/test';

const equipmentId = '15000000-0000-0000-0000-000000000301';
const profileId = '15000000-0000-0000-0000-000000000201';
const assignmentId = '15000000-0000-0000-0000-000000000203';
const currentTagId = '15000000-0000-0000-0000-000000000101';
const speedTagId = '15000000-0000-0000-0000-000000000102';
const firstContextSignalId = '15000000-0000-0000-0000-000000000213';
const secondContextSignalId = '15000000-0000-0000-0000-000000000214';
const firstContextTagId = '15000000-0000-0000-0000-000000000105';
const secondContextTagId = '15000000-0000-0000-0000-000000000106';

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
  await ranges.nth(0).fill('0');
  await ranges.nth(1).fill('100');
  await ranges.nth(2).fill('0');
  await ranges.nth(3).fill('100');

  const guidedGenerate = page.getByTestId(`intelligence-guided-dashboard-assignment-${assignmentId}`);
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
  performanceReady?: boolean;
  guidedDashboardReady?: boolean;
  pumpPerformanceReady?: boolean;
  duplicatePerformanceRole?: boolean;
  mismatchedPerformanceRevision?: boolean;
  semanticCatalogAvailable?: boolean;
  unsupportedPerformanceUnit?: boolean;
  mismatchedPumpFlowUnitPair?: boolean;
  performanceFailCode?: string;
} = {}) {
  const requests: Array<Record<string, any>> = [];
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
    signals: performanceSignals
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
    ...(options.duplicatePerformanceRole ? [{ id: '15000000-0000-0000-0000-000000000103', name: 'Potência duplicada', path: 'Planta.Bomba01.Potencia2', dataType: 'Double', engineeringUnit: 'W', readOnly: true,
      controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.power.watt' } }] : []),
    ...(options.guidedDashboardReady ? [
      { id: firstContextTagId, name: 'Temperatura contextual', path: 'Planta.Bomba01.Temperatura', dataType: 'Double', engineeringUnit: '°C', readOnly: true,
        controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.temperature.degree_celsius' } },
      { id: secondContextTagId, name: 'Pressão contextual', path: 'Planta.Bomba01.Pressao', dataType: 'Double', engineeringUnit: 'kPa', readOnly: true,
        controlledEngineeringUnit: { catalogVersion: 2, code: 'unit.pressure.kilopascal' } }
    ] : [])
  ];
  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
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
  return { requests, performanceCatalogRequests, performanceRequests, dataReads, otherWrites };
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
