import { expect, test, type APIRequestContext } from '@playwright/test';

const projectKey = 'e2e-wave11';
const screenStartId = '16000000-0000-0000-0000-000000000001';
const popupOpenId = '16000000-0000-0000-0000-000000000002';
const popupId = '16000000-0000-0000-0000-000000000003';
const popupStopId = '16000000-0000-0000-0000-000000000004';
const dynamoId = '16000000-0000-0000-0000-000000000005';
const dynamoChildId = '16000000-0000-0000-0000-000000000006';
const dynamoInstanceId = '16000000-0000-0000-0000-000000000007';
const popupKey = 'c16.popup.command';
const dynamoKey = 'c16.dynamo.command';

async function runningValue(request: APIRequestContext): Promise<boolean | null> {
  const response = await request.get('/api/tags/by-path/Demo.P01.Running');
  if (!response.ok()) return null;
  const payload = await response.json() as { current?: { value?: unknown } | null };
  return typeof payload.current?.value === 'boolean' ? payload.current.value : null;
}

async function executeCommand(request: APIRequestContext, commandId: string) {
  const response = await request.post(`/api/commands/${encodeURIComponent(commandId)}/execute`);
  expect(response.ok(), `Command ${commandId} failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
}

test('C16 Screen Dynamo and Popup execute canonical Commands in the Active HMI Runtime', async ({ page, request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;

  const overview = working.screens?.find((screen: any) => screen.key === 'demo.overview');
  expect(overview?.id).toBeTruthy();
  const start = working.commands?.find((command: any) => command.key === 'demo.p01.start');
  const stop = working.commands?.find((command: any) => command.key === 'demo.p01.stop');
  expect(start?.id).toBeTruthy();
  expect(stop?.id).toBeTruthy();

  working.startupScreenId = overview.id;
  overview.elements = (overview.elements ?? []).filter((element: any) =>
    ![screenStartId, popupOpenId, dynamoInstanceId].includes(element.id));
  overview.elements.push(
    {
      id: screenStartId,
      key: 'c16-screen-start',
      type: 'core.button',
      properties: { x: 1020, y: 40, width: 210, height: 52, text: 'C16 SCREEN START', zIndex: 300 },
      actions: [{
        eventKey: 'click',
        kind: 'executeCommand',
        targetKey: null,
        commandId: start.id,
        parameters: null,
        version: 1
      }]
    },
    {
      id: popupOpenId,
      key: 'c16-open-popup',
      type: 'core.button',
      properties: { x: 1020, y: 112, width: 210, height: 52, text: 'C16 OPEN POPUP', zIndex: 300 },
      actions: [{ eventKey: 'click', kind: 'openPopup', targetKey: popupKey, version: 1 }]
    },
    {
      id: dynamoInstanceId,
      key: 'c16-dynamo-instance',
      type: 'dynamo',
      dynamoKey,
      properties: { x: 1020, y: 184, width: 230, height: 72, zIndex: 300 }
    }
  );

  working.dynamos = (working.dynamos ?? []).filter((dynamo: any) => dynamo.key !== dynamoKey);
  working.dynamos.push({
    id: dynamoId,
    key: dynamoKey,
    name: 'C16 Command Dynamo',
    parameters: [],
    elements: [{
      id: dynamoChildId,
      key: 'c16-dynamo-start',
      type: 'core.button',
      properties: { x: 0, y: 0, width: 210, height: 52, text: 'C16 DYNAMO START' },
      actions: [{
        eventKey: 'click',
        kind: 'executeCommand',
        targetKey: null,
        commandId: start.id,
        parameters: null,
        version: 1
      }]
    }]
  });

  working.popups = (working.popups ?? []).filter((popup: any) => popup.key !== popupKey);
  working.popups.push({
    id: popupId,
    key: popupKey,
    name: 'C16 Command Popup',
    templateKey: null,
    x: 360,
    y: 220,
    elements: [{
      id: popupStopId,
      key: 'c16-popup-stop',
      type: 'core.button',
      properties: { x: 0, y: 0, width: 210, height: 52, text: 'C16 POPUP STOP' },
      actions: [{
        eventKey: 'click',
        kind: 'executeCommand',
        targetKey: null,
        commandId: stop.id,
        parameters: null,
        version: 1
      }]
    }]
  });

  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: working });
  expect(previewResponse.ok()).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items: any[] };
  expect(preview.canApply, JSON.stringify(preview.items.flatMap(item => item.issues ?? []), null, 2)).toBeTruthy();
  expect(preview.errorCount).toBe(0);

  const applyResponse = await request.post('/api/engineering/import/json/apply', { data: working });
  expect(applyResponse.ok(), `C16 apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

  const saveResponse = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 11 E2E' }
  });
  expect(saveResponse.ok()).toBeTruthy();
  const saved = await saveResponse.json() as { revision: number };

  const publishResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`,
    { data: {} }
  );
  expect(publishResponse.ok()).toBeTruthy();

  const activateResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/published/activate`,
    { data: {} }
  );
  expect(activateResponse.ok(), `C16 activate failed: HTTP ${activateResponse.status()} ${await activateResponse.text()}`).toBeTruthy();

  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok()).toBeTruthy();
  const active = await activeResponse.json() as any;
  expect(active.revision).toBe(saved.revision);
  expect(active.package.startupScreenId).toBe(overview.id);
  expect(active.package.popups.find((popup: any) => popup.key === popupKey)).toMatchObject({ x: 360, y: 220 });

  await page.setViewportSize({ width: 1280, height: 720 });
  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toHaveAttribute('data-runtime-revision', String(saved.revision));
  await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', 'demo.overview');
  await expect.poll(async () => Number(await page.getByTestId('runtime-logical-viewport').getAttribute('data-runtime-scale')))
    .toBeCloseTo(2 / 3, 3);

  await executeCommand(request, stop.id);
  await expect.poll(() => runningValue(request)).toBe(false);
  await page.getByRole('button', { name: 'C16 SCREEN START' }).click();
  await expect.poll(() => runningValue(request)).toBe(true);

  await executeCommand(request, stop.id);
  await expect.poll(() => runningValue(request)).toBe(false);
  await page.getByRole('button', { name: 'C16 DYNAMO START' }).click();
  await expect.poll(() => runningValue(request)).toBe(true);

  await page.getByRole('button', { name: 'C16 OPEN POPUP' }).click();
  const popupLayer = page.locator('.runtime-visual-popup-layer');
  await expect(popupLayer).toHaveAttribute('data-popup-count', '1');
  const firstPopup = page.locator('.runtime-visual-popup').first();
  await expect(firstPopup).toHaveAttribute('data-popup-key', popupKey);
  await expect(firstPopup).toHaveAttribute('data-popup-logical-x', '360');
  await expect(firstPopup).toHaveAttribute('data-popup-logical-y', '220');
  await expect(firstPopup).toHaveAttribute('data-popup-stack-index', '0');

  await page.getByRole('button', { name: 'C16 POPUP STOP' }).click();
  await expect.poll(() => runningValue(request)).toBe(false);

  await page.getByRole('button', { name: 'C16 OPEN POPUP' }).click();
  await expect(popupLayer).toHaveAttribute('data-popup-count', '2');
  const popups = page.locator('.runtime-visual-popup');
  await expect(popups.nth(0)).toHaveAttribute('data-popup-stack-index', '0');
  await expect(popups.nth(1)).toHaveAttribute('data-popup-stack-index', '1');
  await expect(popups.nth(1)).toHaveAttribute('data-popup-logical-x', '360');
  await expect(popups.nth(1)).toHaveAttribute('data-popup-logical-y', '220');
});