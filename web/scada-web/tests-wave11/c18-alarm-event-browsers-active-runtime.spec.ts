import {
  expect,
  request as playwrightRequest,
  test,
  type APIRequestContext,
  type Locator,
  type Page
} from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const overviewKey = 'demo.overview';
const popupKey = 'c16.popup.command';

const screenAlarmArea = 'Screen-Alarm-Area';
const screenEventArea = 'Screen-Event-Area';
const popupAlarmArea = 'Popup-Alarm-Area';
const popupEventArea = 'Popup-Event-Area';

type BrowserType = 'core.alarmBrowser' | 'core.eventBrowser';

test('C18 authors Alarm and Event Browser in Screen and Popup, then Save Publish Activate reaches Active Runtime', async ({ page, request }) => {
  await page.addInitScript(() => {
    if (!localStorage.getItem('elitescada.engineering.locale')) {
      localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
    }
  });

  const initialWorking = await loadWorking(request);
  const overview = initialWorking.screens?.find((screen: any) => screen.key === overviewKey);
  const popup = initialWorking.popups?.find((candidate: any) => candidate.key === popupKey);
  expect(overview?.id, 'C16 dependency must leave demo.overview available for C18 authoring').toBeTruthy();
  expect(popup?.id, 'C16 dependency must leave its canonical command Popup available for C18 authoring').toBeTruthy();
  expect(initialWorking.startupScreenId, 'Wave11 must expose a canonical startup screen before C18').toBe(overview.id);
  expect(flatten(overview.elements ?? []).some((element: any) =>
    (element.actions ?? []).some((action: any) => action.kind === 'openPopup' && action.targetKey === popupKey)
  ), 'C18 must reuse the C16 canonical Popup route instead of injecting private test wiring').toBeTruthy();

  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
  await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: overviewKey }).click();

  await authorBrowser(page, 'core.alarmBrowser', {
    area: screenAlarmArea,
    pageSize: 10,
    mode: 'current',
    lifecycle: 'active',
    x: 20,
    y: 300
  });
  await authorBrowser(page, 'core.eventBrowser', {
    area: screenEventArea,
    pageSize: 20,
    source: 'runtime.hmi',
    x: 780,
    y: 300
  });
  await previewAndApplyScreen(page);

  await expect.poll(async () => {
    const working = await loadWorking(request);
    const screen = working.screens?.find((candidate: any) => candidate.key === overviewKey);
    return Boolean(
      findBrowserByArea(screen, 'core.alarmBrowser', screenAlarmArea) &&
      findBrowserByArea(screen, 'core.eventBrowser', screenEventArea)
    );
  }).toBe(true);

  await page.locator('.eng-nav').getByRole('button', { name: /Popups/ }).click();
  await expect(page.getByTestId('popup-visual-editor-workspace')).toBeVisible();
  await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: popupKey }).click();

  await authorBrowser(page, 'core.alarmBrowser', {
    area: popupAlarmArea,
    pageSize: 30,
    mode: 'history',
    lifecycle: 'active',
    x: 10,
    y: 80
  });
  await authorBrowser(page, 'core.eventBrowser', {
    area: popupEventArea,
    pageSize: 40,
    source: 'runtime.popup',
    x: 10,
    y: 420
  });
  await previewAndApplyPopup(page);

  await expect.poll(async () => {
    const working = await loadWorking(request);
    const candidate = working.popups?.find((item: any) => item.key === popupKey);
    return Boolean(
      findBrowserByArea(candidate, 'core.alarmBrowser', popupAlarmArea) &&
      findBrowserByArea(candidate, 'core.eventBrowser', popupEventArea)
    );
  }).toBe(true);

  const authored = await loadWorking(request);
  const authoredScreen = authored.screens.find((candidate: any) => candidate.key === overviewKey);
  const authoredPopup = authored.popups.find((candidate: any) => candidate.key === popupKey);
  const screenAlarm = findBrowserByArea(authoredScreen, 'core.alarmBrowser', screenAlarmArea)!;
  const screenEvent = findBrowserByArea(authoredScreen, 'core.eventBrowser', screenEventArea)!;
  const popupAlarm = findBrowserByArea(authoredPopup, 'core.alarmBrowser', popupAlarmArea)!;
  const popupEvent = findBrowserByArea(authoredPopup, 'core.eventBrowser', popupEventArea)!;

  expect(screenAlarm.id).toBeTruthy();
  expect(screenEvent.id).toBeTruthy();
  expect(popupAlarm.id).toBeTruthy();
  expect(popupEvent.id).toBeTruthy();
  expect(new Set([screenAlarm.id, screenEvent.id, popupAlarm.id, popupEvent.id]).size).toBe(4);
  expect(screenAlarm.properties).toMatchObject({ x: 20, y: 300, width: 720, height: 320, browserConfig: { mode: 'current', lifecycle: 'active', area: screenAlarmArea, pageSize: 10 } });
  expect(screenEvent.properties).toMatchObject({ x: 780, y: 300, width: 720, height: 320, browserConfig: { source: 'runtime.hmi', area: screenEventArea, pageSize: 20 } });
  expect(popupAlarm.properties).toMatchObject({ x: 10, y: 80, width: 720, height: 320, browserConfig: { mode: 'history', lifecycle: 'active', area: popupAlarmArea, pageSize: 30 } });
  expect(popupEvent.properties).toMatchObject({ x: 10, y: 420, width: 720, height: 320, browserConfig: { source: 'runtime.popup', area: popupEventArea, pageSize: 40 } });
  expect(screenAlarm.properties.browserConfig).not.toEqual(popupAlarm.properties.browserConfig);
  expect(screenEvent.properties.browserConfig).not.toEqual(popupEvent.properties.browserConfig);

  const beforeSaveResponse = await request.get('/api/engineering/workspace');
  expect(beforeSaveResponse.ok()).toBeTruthy();
  const beforeSave = await beforeSaveResponse.json() as { baseRevision: number | null; isDirty: boolean };
  expect(beforeSave.isDirty).toBe(true);

  await page.locator('.eng-nav button').first().click();
  const lifecycle = page.locator('.eng-lifecycle-workspace');
  await expect(lifecycle).toBeVisible();
  const lifecycleActions = lifecycle.locator('.eng-lifecycle-workspace__action-buttons');
  const saveButton = lifecycleActions.getByRole('button').first();
  await expect(saveButton).toBeEnabled();
  await saveButton.click();

  await expect.poll(async () => {
    const response = await request.get('/api/engineering/workspace');
    if (!response.ok()) return true;
    const workspace = await response.json() as { isDirty: boolean };
    return workspace.isDirty;
  }).toBe(false);
  const savedWorkspaceResponse = await request.get('/api/engineering/workspace');
  const savedWorkspace = await savedWorkspaceResponse.json() as { baseRevision: number | null };
  expect(savedWorkspace.baseRevision).toBeTruthy();
  expect(savedWorkspace.baseRevision).not.toBe(beforeSave.baseRevision);
  const savedRevision = savedWorkspace.baseRevision!;

  const revisionRow = lifecycle.locator('.eng-lifecycle-workspace__revision-row').filter({ hasText: `r${savedRevision}` }).first();
  await expect(revisionRow).toBeVisible();
  const publishButton = revisionRow.locator('.eng-lifecycle-workspace__row-actions').getByRole('button').nth(1);
  await expect(publishButton).toBeEnabled();
  await publishButton.click();
  await confirmLifecycleAction(lifecycle);

  await expect.poll(async () => {
    const response = await request.get(`/api/engineering/persistence/${projectKey}/lifecycle`);
    if (!response.ok()) return null;
    return (await response.json() as { publishedRevision: number | null }).publishedRevision;
  }).toBe(savedRevision);

  const activateButton = lifecycleActions.getByRole('button').nth(1);
  await expect(activateButton).toBeEnabled();
  await activateButton.click();
  await confirmLifecycleAction(lifecycle);

  await expect.poll(async () => {
    const response = await request.get('/api/runtime/application');
    if (!response.ok()) return null;
    return (await response.json() as { revision: number }).revision;
  }).toBe(savedRevision);

  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok()).toBeTruthy();
  const active = await activeResponse.json() as any;
  expect(active.projectKey).toBe(projectKey);
  expect(active.revision).toBe(savedRevision);
  const activeScreen = active.package.screens.find((candidate: any) => candidate.key === overviewKey);
  const activePopup = active.package.popups.find((candidate: any) => candidate.key === popupKey);
  expect(findBrowserByArea(activeScreen, 'core.alarmBrowser', screenAlarmArea)?.id).toBe(screenAlarm.id);
  expect(findBrowserByArea(activeScreen, 'core.eventBrowser', screenEventArea)?.id).toBe(screenEvent.id);
  expect(findBrowserByArea(activePopup, 'core.alarmBrowser', popupAlarmArea)?.id).toBe(popupAlarm.id);
  expect(findBrowserByArea(activePopup, 'core.eventBrowser', popupEventArea)?.id).toBe(popupEvent.id);

  const screenOperationalQuery = page.waitForRequest(req => {
    if (!req.url().includes('/api/historical/query') || req.method() !== 'POST') return false;
    try {
      const body = req.postDataJSON();
      return body?.datasetKey === 'operational.events' && body?.filters?.some((filter: any) =>
        filter.field === 'area' && filter.values?.[0]?.value === screenEventArea);
    } catch {
      return false;
    }
  });

  await page.goto('/');
  const eventQuery = await screenOperationalQuery;
  const eventQueryBody = eventQuery.postDataJSON() as any;
  expect(eventQueryBody.datasetKey).toBe('operational.events');
  expect(eventQueryBody.page.limit).toBe(20);
  expect(eventQueryBody.filters).toEqual(expect.arrayContaining([
    expect.objectContaining({ field: 'source', operator: 'contains', values: [{ kind: 'string', value: 'runtime.hmi' }] }),
    expect.objectContaining({ field: 'area', operator: 'contains', values: [{ kind: 'string', value: screenEventArea }] })
  ]));

  await expect(page.getByTestId('runtime-engineering-application')).toHaveAttribute('data-runtime-revision', String(savedRevision));
  const mountedScreenAlarm = page.locator(`[data-runtime-object-id="${screenAlarm.id}"]`);
  const mountedScreenEvent = page.locator(`[data-runtime-object-id="${screenEvent.id}"]`);
  await expect(mountedScreenAlarm).toBeVisible();
  await expect(mountedScreenEvent).toBeVisible();
  await expect(mountedScreenAlarm.locator('.hmi-browser__header strong')).toHaveText('Alarmes');
  await expect(mountedScreenEvent.locator('.hmi-browser__header strong')).toHaveText('Eventos operacionais');
  await expect(mountedScreenAlarm).toHaveAttribute('data-browser-state', /^(ready|empty)$/);
  await expect(mountedScreenEvent).toHaveAttribute('data-browser-state', /^(ready|empty)$/);

  await page.getByRole('button', { name: 'C16 OPEN POPUP' }).click();
  const popupLayer = page.locator('.runtime-visual-popup-layer');
  await expect(popupLayer).toHaveAttribute('data-popup-count', '1');
  const mountedPopupAlarm = page.locator(`.runtime-visual-popup [data-runtime-object-id="${popupAlarm.id}"]`);
  const mountedPopupEvent = page.locator(`.runtime-visual-popup [data-runtime-object-id="${popupEvent.id}"]`);
  await expect(mountedPopupAlarm).toBeAttached();
  await expect(mountedPopupEvent).toBeAttached();
  await expect(mountedPopupAlarm.locator('.hmi-browser__header strong')).toHaveText('Alarmes');
  await expect(mountedPopupEvent.locator('.hmi-browser__header strong')).toHaveText('Eventos operacionais');

  await page.evaluate(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.reload();
  await expect(page.locator(`[data-runtime-object-id="${screenAlarm.id}"] .hmi-browser__header strong`)).toHaveText('Alarms');
  await expect(page.locator(`[data-runtime-object-id="${screenEvent.id}"] .hmi-browser__header strong`)).toHaveText('Operational events');

  await page.evaluate(() => localStorage.setItem('elitescada.engineering.locale', 'es'));
  await page.reload();
  await expect(page.locator(`[data-runtime-object-id="${screenAlarm.id}"] .hmi-browser__header strong`)).toHaveText('Alarmas');
  await expect(page.locator(`[data-runtime-object-id="${screenEvent.id}"] .hmi-browser__header strong`)).toHaveText('Eventos operacionales');

  const definitionsResponse = await request.get('/api/alarms/definitions');
  expect(definitionsResponse.ok()).toBeTruthy();
  const definitions = await definitionsResponse.json() as Array<{ id: string }>;
  expect(definitions.length).toBeGreaterThan(0);
  const definitionId = definitions[0].id;

  const developerAck = await request.post(`/api/alarms/${encodeURIComponent(definitionId)}/ack`, { data: {} });
  expect([401, 403]).not.toContain(developerAck.status());

  const noRoleToken = createE2eJwt('wave11-c18-no-role', [], 'Wave 11 C18 No Role');
  const deniedContext = await playwrightRequest.newContext({
    baseURL: 'http://127.0.0.1:5174',
    extraHTTPHeaders: { Authorization: `Bearer ${noRoleToken}` }
  });
  try {
    const deniedAck = await deniedContext.post(`/api/alarms/${encodeURIComponent(definitionId)}/ack`, { data: {} });
    expect(deniedAck.status()).toBe(403);
  } finally {
    await deniedContext.dispose();
  }
});

async function authorBrowser(
  page: Page,
  type: BrowserType,
  options: Readonly<{
    area: string;
    pageSize: number;
    source?: string;
    mode?: 'current' | 'history';
    lifecycle?: 'all' | 'active' | 'returned';
    x: number;
    y: number;
  }>
) {
  await page.locator(`[data-object-type="${type}"]`).click();
  const object = page.locator(`[data-canvas-object-type="${type}"]`).last();
  await expect(object).toBeVisible();
  await object.click();

  const editor = page.getByTestId('browser-configuration-editor');
  await expect(editor).toBeVisible();
  if (type === 'core.alarmBrowser') {
    await editor.getByLabel('Fonte', { exact: true }).selectOption(options.mode ?? 'current');
    await editor.getByLabel('Estado', { exact: true }).selectOption(options.lifecycle ?? 'all');
  } else if (options.source !== undefined) {
    await editor.getByLabel('Origem', { exact: true }).fill(options.source);
  }
  await editor.getByLabel('Área', { exact: true }).fill(options.area);
  await editor.getByLabel('Linhas por página', { exact: true }).fill(String(options.pageSize));

  const inspector = page.getByTestId('visual-property-inspector');
  await setInspectorNumber(inspector, 'X', options.x);
  await setInspectorNumber(inspector, 'Y', options.y);
}

async function setInspectorNumber(inspector: Locator, name: string, value: number) {
  const input = inspector.getByRole('spinbutton', { name, exact: true });
  await expect(input).toBeVisible();
  await input.fill(String(value));
  await input.press('Enter');
}

async function previewAndApplyScreen(page: Page) {
  const preview = page.getByTestId('visual-editor-preview');
  const apply = page.getByTestId('visual-editor-apply');
  await preview.click();
  await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
  await expect(apply).toBeEnabled();
  page.once('dialog', dialog => dialog.accept());
  await apply.click();
}

async function previewAndApplyPopup(page: Page) {
  await page.getByRole('button', { name: 'Preview da alteração', exact: true }).click();
  await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
  const apply = page.getByRole('button', { name: 'Aplicar ao Workspace', exact: true });
  await expect(apply).toBeEnabled();
  page.once('dialog', dialog => dialog.accept());
  await apply.click();
}

async function confirmLifecycleAction(lifecycle: Locator) {
  const confirmation = lifecycle.locator('.eng-lifecycle-workspace__confirmation');
  await expect(confirmation).toBeVisible();
  await confirmation.locator('.eng-lifecycle-workspace__critical').click();
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

function findBrowserByArea(container: any, type: BrowserType, area: string): any | null {
  return flatten(container?.elements ?? []).find((element: any) =>
    element.type === type && element.properties?.browserConfig?.area === area
  ) ?? null;
}

function flatten(elements: readonly any[]): any[] {
  const result: any[] = [];
  for (const element of elements) {
    result.push(element);
    result.push(...flatten(element.children ?? []));
  }
  return result;
}
