import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { EEE_IDS, EEE_PATHS } from './c11-eee-demo-foundation-canonical';
import { buildEeeDemoPackage, EEE_HMI } from './c11-eee-demo-hmi';

const projectKey = 'e2e-wave11';
const TAG_QUALITY = { Good: 0, Unavailable: 8 } as const;

test('C11 canonical EEE HMI survives lifecycle and exercises operator-facing generic product surfaces', async ({ page, request }) => {
  const original = await loadWorking(request);

  try {
    const candidate = buildEeeDemoPackage(original);
    const overview = candidate.screens.find((screen: any) => screen.key === EEE_HMI.screens.overview.key);
    expect(overview?.id).toBe(EEE_HMI.screens.overview.id);
    expect(candidate.startupScreenId).toBe(EEE_HMI.screens.overview.id);
    expect(candidate.screens).toHaveLength(6);
    expect(candidate.popups).toHaveLength(2);
    expect(candidate.dynamos).toHaveLength(1);
    expect(candidate.dynamos[0]).toMatchObject({ key: EEE_HMI.dynamoKey });

    const authoredWetWell = overview.elements.find((element: any) => element.id === EEE_HMI.elements.wetWell);
    expect(authoredWetWell?.analogFill).toMatchObject({
      direction: 'BottomToTop',
      source: { kind: 'Tag', valueType: 'Number', target: EEE_PATHS.levelPct }
    });

    await previewAndApply(request, candidate, 'C11 EEE HMI');
    const saved = await savePublishActivate(request, 'EliteSCADA — EEE Demo — Wave11 HMI harness');

    const activeResponse = await request.get('/api/runtime/application');
    expect(activeResponse.ok(), `Active application failed: HTTP ${activeResponse.status()} ${await activeResponse.text()}`).toBeTruthy();
    const active = await activeResponse.json() as any;
    expect(active.projectKey).toBe(projectKey);
    expect(active.revision).toBe(saved.revision);
    expect(active.package.startupScreenId).toBe(EEE_HMI.screens.overview.id);

    const activeOverview = active.package.screens.find((screen: any) => screen.key === EEE_HMI.screens.overview.key);
    expect(activeOverview?.id).toBe(EEE_HMI.screens.overview.id);
    const persistedWetWell = activeOverview.elements.find((element: any) => element.id === EEE_HMI.elements.wetWell);
    expect(persistedWetWell?.analogFill).toMatchObject({
      direction: 'bottomToTop',
      source: { kind: 'tag', valueType: 'number', target: EEE_PATHS.levelPct }
    });

    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.levelPct)).value), { timeout: 15_000 })
      .toBeGreaterThan(0);

    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto('/');
    await expect(page.getByTestId('runtime-engineering-application')).toHaveAttribute('data-runtime-revision', String(saved.revision));
    await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', EEE_HMI.screens.overview.key);
    await assertLogicalViewport(page);
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    const wetWell = page.locator(`[data-object-id="${EEE_HMI.elements.wetWell}"]`);
    await expect(wetWell).toBeVisible();
    await expect(wetWell).toHaveAttribute('data-dynamic-state', 'available');
    const fill = wetWell.getByTestId('visual-analog-fill');
    await expect(fill).toBeVisible();
    const initialFill = Number(await fill.getAttribute('data-fill-percent'));
    expect(Number.isFinite(initialFill)).toBe(true);
    await expect.poll(async () => Number(await fill.getAttribute('data-fill-percent')), { timeout: 10_000 })
      .toBeGreaterThan(initialFill);

    const pumpDynamos = page.locator(`[data-dynamo-key="${EEE_HMI.dynamoKey}"]`);
    await expect(pumpDynamos).toHaveCount(2);
    const p01 = page.locator(`[data-object-id="${EEE_HMI.elements.p01}"]`);
    const p02 = page.locator(`[data-object-id="${EEE_HMI.elements.p02}"]`);
    await expect(p01).toHaveAttribute('data-dynamo-instance-id', EEE_HMI.elements.p01);
    await expect(p02).toHaveAttribute('data-dynamo-instance-id', EEE_HMI.elements.p02);
    await expect(p01).toHaveAttribute('data-dynamic-state', 'available');
    await expect(p02).toHaveAttribute('data-dynamic-state', 'available');

    await executeCommand(request, EEE_IDS.commands.autoDisable);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.autoMode)).value)).toBe(false);
    await executeCommand(request, EEE_IDS.commands.resetFaults);
    await executeCommand(request, EEE_IDS.commands.p01Stop);
    await executeCommand(request, EEE_IDS.commands.p02Stop);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value)).toBe(false);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value)).toBe(false);

    // One reusable Dynamo definition must remain independently bound per
    // instance. Prove P01-only operation first and confirm P02 stays stopped.
    await executeCommand(request, EEE_IDS.commands.p01Start);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value)).toBe(true);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value)).toBe(false);
    await expect(p01.getByText('OPERANDO')).toBeVisible();
    await expect(p02.getByText('OPERANDO')).toBeHidden();

    await page.getByRole('button', { name: 'DETALHES P01' }).click();
    const popupLayer = page.locator('.runtime-visual-popup-layer');
    await expect(popupLayer).toHaveAttribute('data-popup-count', '1');
    const popup = page.locator('.runtime-visual-popup').first();
    await expect(popup).toHaveAttribute('data-popup-key', EEE_HMI.popups.p01.key);
    await expect(popup).toHaveAttribute('data-popup-logical-x', '520');
    await expect(popup).toHaveAttribute('data-popup-logical-y', '210');
    await popup.getByRole('button', { name: 'PARAR' }).click();
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value)).toBe(false);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value)).toBe(false);
    await popup.getByRole('button', { name: 'FECHAR' }).click();

    // Then prove the second instance is not merely a duplicated visual: P02 can
    // run alone while P01 remains stopped, using the same Dynamo definition.
    await executeCommand(request, EEE_IDS.commands.p02Start);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value)).toBe(true);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value)).toBe(false);
    await expect(p02.getByText('OPERANDO')).toBeVisible();
    await expect(p01.getByText('OPERANDO')).toBeHidden();
    await executeCommand(request, EEE_IDS.commands.p02Stop);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value)).toBe(false);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value)).toBe(false);

    await executeCommand(request, EEE_IDS.commands.injectP01Fault);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Fault)).value)).toBe(true);
    await expect(p01.getByText('FALHA')).toBeVisible();
    await executeCommand(request, EEE_IDS.commands.resetFaults);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Fault)).value)).toBe(false);

    await executeCommand(request, EEE_IDS.commands.badQualityEnable);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.p01PressureBar)).quality)).toBe(TAG_QUALITY.Unavailable);
    await expect.poll(async () => p01.locator('.visual-editor-dynamic-unavailable').count(), { timeout: 10_000 }).toBeGreaterThan(0);
    await executeCommand(request, EEE_IDS.commands.badQualityDisable);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.p01PressureBar)).quality)).toBe(TAG_QUALITY.Good);

    await page.getByRole('button', { name: 'TENDÊNCIAS' }).click();
    await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', EEE_HMI.screens.trends.key);
    const trend = page.getByTestId('visual-trend');
    await expect(trend).toBeVisible();
    await expect(trend).toHaveAttribute('data-trend-mode', 'live');
    await expect(trend).toHaveAttribute('data-trend-pen-count', '5');
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    await page.getByRole('button', { name: 'ALARMES / EVENTOS' }).click();
    await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', EEE_HMI.screens.alarmsEvents.key);
    const browsers = page.locator('.hmi-browser');
    await expect(browsers).toHaveCount(2);
    await expect.poll(async () => await page.locator('.hmi-browser[data-browser-state="error"]').count(), { timeout: 15_000 }).toBe(0);
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    await page.getByRole('button', { name: 'EEE PRINCIPAL' }).click();
    await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', EEE_HMI.screens.overview.key);
    await assertNoDocumentOverflow(page);
  } finally {
    await previewAndApply(request, original, 'C11 HMI cleanup');
    await savePublishActivate(request, 'Wave 11 E2E — restored after C11 HMI');
  }
});

async function assertLogicalViewport(page: Page) {
  const viewport = page.getByTestId('runtime-logical-viewport');
  await expect(viewport).toBeVisible();
  const geometry = await viewport.evaluate(element => {
    const node = element as HTMLElement;
    return {
      width: node.clientWidth,
      height: node.clientHeight,
      designWidth: Number(node.dataset.designWidth),
      designHeight: Number(node.dataset.designHeight),
      scale: Number(node.dataset.runtimeScale)
    };
  });
  expect(geometry.designWidth).toBe(1920);
  expect(geometry.designHeight).toBe(1080);
  expect(geometry.scale).toBeCloseTo(Math.min(
    geometry.width / geometry.designWidth,
    geometry.height / geometry.designHeight
  ), 5);
}

async function assertNoDocumentOverflow(page: Page) {
  const overflow = await page.evaluate(() => ({
    width: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    height: document.documentElement.scrollHeight - document.documentElement.clientHeight
  }));
  expect(overflow.width).toBeLessThanOrEqual(1);
  expect(overflow.height).toBeLessThanOrEqual(1);
}

async function previewAndApply(request: APIRequestContext, candidate: any, label: string) {
  const before = await loadWorkspace(request);
  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
  expect(previewResponse.ok(), `${label} preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
  expect(preview.canApply, `${label} preview issues: ${JSON.stringify(preview.items ?? [], null, 2)}`).toBe(true);
  expect(preview.errorCount).toBe(0);

  const afterPreview = await loadWorkspace(request);
  expect(afterPreview.changeVersion).toBe(before.changeVersion);
  const applyResponse = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
    data: candidate
  });
  expect(applyResponse.ok(), `${label} Apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();
}

async function savePublishActivate(request: APIRequestContext, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${projectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save failed: HTTP ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };
  const publish = await request.post(`/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish failed: HTTP ${publish.status()} ${await publish.text()}`).toBeTruthy();
  const activate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate failed: HTTP ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function readCurrent(request: APIRequestContext, path: string): Promise<{ value?: unknown; quality?: unknown }> {
  const response = await request.get(`/api/tags/by-path/${path}`);
  expect(response.ok(), `TAG ${path} read failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  const body = await response.json() as { current?: { value?: unknown; quality?: unknown } | null };
  return body.current ?? {};
}

async function executeCommand(request: APIRequestContext, commandId: string) {
  const response = await request.post(`/api/commands/${commandId}/execute`);
  expect(response.ok(), `Command ${commandId} failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
}
