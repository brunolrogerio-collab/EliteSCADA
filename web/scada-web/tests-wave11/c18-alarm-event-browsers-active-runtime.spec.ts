import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const popupKey = 'c18.popup.browsers';
const popupId = '18000000-0000-0000-0000-000000000001';
const popupOpenId = '18000000-0000-0000-0000-000000000002';
const screenAlarmId = '18000000-0000-0000-0000-000000000003';
const screenEventId = '18000000-0000-0000-0000-000000000004';
const popupAlarmId = '18000000-0000-0000-0000-000000000005';
const popupEventId = '18000000-0000-0000-0000-000000000006';

function alarmConfig(area: string, pageSize: number, mode: 'current' | 'history' = 'current') {
  return {
    version: 1,
    mode,
    lifecycle: 'active',
    acknowledgement: 'all',
    minimumPriority: null,
    area,
    tagPath: '',
    text: '',
    lookbackSeconds: 3600,
    columns: ['timestamp', 'state', 'priority', 'name', 'area', 'tag.path', 'message'],
    sortField: 'timestamp',
    sortDirection: 'descending',
    pageSize,
    acknowledgeEnabled: true
  };
}

function eventConfig(area: string, pageSize: number, source = '') {
  return {
    version: 1,
    type: '',
    category: '',
    source,
    area,
    equipmentPath: '',
    tagPath: '',
    operator: '',
    operation: '',
    commandKey: '',
    text: '',
    lookbackSeconds: 3600,
    columns: ['timestamp', 'type', 'category', 'source', 'area', 'equipment.path', 'tag.path', 'operator', 'operation', 'message'],
    sortField: 'timestamp',
    sortDirection: 'descending',
    pageSize
  };
}

function browserElement(id: string, key: string, type: 'core.alarmBrowser' | 'core.eventBrowser', x: number, y: number, config: object) {
  return {
    id,
    key,
    type,
    properties: {
      x,
      y,
      width: 700,
      height: 300,
      zIndex: 318,
      visible: true,
      opacity: 1,
      browserConfig: config
    }
  };
}

test('C18 Alarm and Event Browser survive Engineering Save Publish Activate in Screen and Popup with independent configs', async ({ page, request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;

  const overview = working.screens?.find((screen: any) => screen.key === 'demo.overview');
  expect(overview?.id).toBeTruthy();
  working.startupScreenId = overview.id;

  const screenAlarm = alarmConfig('Screen-Alarm-Area', 10, 'current');
  const screenEvent = eventConfig('Screen-Event-Area', 20, 'runtime.hmi');
  const popupAlarm = alarmConfig('Popup-Alarm-Area', 30, 'history');
  const popupEvent = eventConfig('Popup-Event-Area', 40, 'runtime.popup');

  overview.elements = (overview.elements ?? []).filter((element: any) =>
    ![popupOpenId, screenAlarmId, screenEventId].includes(element.id));
  overview.elements.push(
    {
      id: popupOpenId,
      key: 'c18-open-browser-popup',
      type: 'core.button',
      properties: { x: 20, y: 20, width: 220, height: 48, text: 'C18 OPEN BROWSERS', zIndex: 320 },
      actions: [{ eventKey: 'click', kind: 'openPopup', targetKey: popupKey, version: 1 }]
    },
    browserElement(screenAlarmId, 'c18-screen-alarm-browser', 'core.alarmBrowser', 20, 100, screenAlarm),
    browserElement(screenEventId, 'c18-screen-event-browser', 'core.eventBrowser', 760, 100, screenEvent)
  );

  working.popups = (working.popups ?? []).filter((popup: any) => popup.key !== popupKey);
  working.popups.push({
    id: popupId,
    key: popupKey,
    name: 'C18 Browser Popup',
    templateKey: null,
    x: 260,
    y: 180,
    elements: [
      browserElement(popupAlarmId, 'c18-popup-alarm-browser', 'core.alarmBrowser', 10, 10, popupAlarm),
      browserElement(popupEventId, 'c18-popup-event-browser', 'core.eventBrowser', 10, 330, popupEvent)
    ]
  });

  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: working });
  expect(previewResponse.ok()).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items: any[] };
  expect(preview.canApply, JSON.stringify(preview.items.flatMap(item => item.issues ?? []), null, 2)).toBeTruthy();
  expect(preview.errorCount).toBe(0);

  const applyResponse = await request.post('/api/engineering/import/json/apply', { data: working });
  expect(applyResponse.ok(), `C18 apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

  const saveResponse = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 11 E2E' }
  });
  expect(saveResponse.ok(), `C18 save failed: HTTP ${saveResponse.status()} ${await saveResponse.text()}`).toBeTruthy();
  const saved = await saveResponse.json() as { revision: number };

  const publishResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`,
    { data: {} }
  );
  expect(publishResponse.ok(), `C18 publish failed: HTTP ${publishResponse.status()} ${await publishResponse.text()}`).toBeTruthy();

  const activateResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/published/activate`,
    { data: {} }
  );
  expect(activateResponse.ok(), `C18 activate failed: HTTP ${activateResponse.status()} ${await activateResponse.text()}`).toBeTruthy();

  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok()).toBeTruthy();
  const active = await activeResponse.json() as any;
  expect(active.projectKey).toBe(projectKey);
  expect(active.revision).toBe(saved.revision);

  const activeScreen = active.package.screens.find((candidate: any) => candidate.id === overview.id);
  const activePopup = active.package.popups.find((candidate: any) => candidate.key === popupKey);
  expect(activePopup).toMatchObject({ x: 260, y: 180 });

  expect(activeScreen.elements.find((element: any) => element.id === screenAlarmId)).toMatchObject({
    type: 'core.alarmBrowser',
    properties: { x: 20, y: 100, width: 700, height: 300, browserConfig: screenAlarm }
  });
  expect(activeScreen.elements.find((element: any) => element.id === screenEventId)).toMatchObject({
    type: 'core.eventBrowser',
    properties: { x: 760, y: 100, width: 700, height: 300, browserConfig: screenEvent }
  });
  expect(activePopup.elements.find((element: any) => element.id === popupAlarmId)).toMatchObject({
    type: 'core.alarmBrowser',
    properties: { browserConfig: popupAlarm }
  });
  expect(activePopup.elements.find((element: any) => element.id === popupEventId)).toMatchObject({
    type: 'core.eventBrowser',
    properties: { browserConfig: popupEvent }
  });

  expect(screenAlarm).not.toEqual(popupAlarm);
  expect(screenEvent).not.toEqual(popupEvent);

  const screenOperationalQuery = page.waitForRequest(req => {
    if (!req.url().includes('/api/historical/query') || req.method() !== 'POST') return false;
    try { return req.postDataJSON()?.datasetKey === 'operational.events'; } catch { return false; }
  });

  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.goto('/');
  const eventQuery = await screenOperationalQuery;
  const eventQueryBody = eventQuery.postDataJSON() as any;
  expect(eventQueryBody.datasetKey).toBe('operational.events');
  expect(eventQueryBody.page.limit).toBe(20);
  expect(eventQueryBody.filters).toEqual(expect.arrayContaining([
    expect.objectContaining({ field: 'source', operator: 'contains', values: [{ kind: 'string', value: 'runtime.hmi' }] }),
    expect.objectContaining({ field: 'area', operator: 'contains', values: [{ kind: 'string', value: 'Screen-Event-Area' }] })
  ]));

  await expect(page.getByTestId('runtime-engineering-application')).toHaveAttribute('data-runtime-revision', String(saved.revision));
  const mountedScreenAlarm = page.locator(`[data-runtime-object-id="${screenAlarmId}"]`);
  const mountedScreenEvent = page.locator(`[data-runtime-object-id="${screenEventId}"]`);
  await expect(mountedScreenAlarm).toBeVisible();
  await expect(mountedScreenEvent).toBeVisible();
  await expect(mountedScreenAlarm.locator('.hmi-browser__header strong')).toHaveText('Alarmes');
  await expect(mountedScreenEvent.locator('.hmi-browser__header strong')).toHaveText('Eventos operacionais');
  await expect(mountedScreenAlarm).toHaveAttribute('data-browser-state', /^(ready|empty)$/);
  await expect(mountedScreenEvent).toHaveAttribute('data-browser-state', /^(ready|empty)$/);

  await page.getByRole('button', { name: 'C18 OPEN BROWSERS' }).click();
  const popupLayer = page.locator('.runtime-visual-popup-layer');
  await expect(popupLayer).toHaveAttribute('data-popup-count', '1');
  const mountedPopupAlarm = page.locator(`.runtime-visual-popup [data-runtime-object-id="${popupAlarmId}"]`);
  const mountedPopupEvent = page.locator(`.runtime-visual-popup [data-runtime-object-id="${popupEventId}"]`);
  await expect(mountedPopupAlarm).toBeAttached();
  await expect(mountedPopupEvent).toBeAttached();
  await expect(mountedPopupAlarm.locator('.hmi-browser__header strong')).toHaveText('Alarmes');
  await expect(mountedPopupEvent.locator('.hmi-browser__header strong')).toHaveText('Eventos operacionais');

  await page.evaluate(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.reload();
  await expect(page.locator(`[data-runtime-object-id="${screenAlarmId}"] .hmi-browser__header strong`)).toHaveText('Alarms');
  await expect(page.locator(`[data-runtime-object-id="${screenEventId}"] .hmi-browser__header strong`)).toHaveText('Operational events');

  await page.evaluate(() => localStorage.setItem('elitescada.engineering.locale', 'es'));
  await page.reload();
  await expect(page.locator(`[data-runtime-object-id="${screenAlarmId}"] .hmi-browser__header strong`)).toHaveText('Alarmas');
  await expect(page.locator(`[data-runtime-object-id="${screenEventId}"] .hmi-browser__header strong`)).toHaveText('Eventos operacionales');

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
