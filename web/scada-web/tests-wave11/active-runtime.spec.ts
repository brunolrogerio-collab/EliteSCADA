import { expect, test } from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const operatorToken = createE2eJwt('wave11-operator', ['operator'], 'Wave 11 Operator');
const runtimeSourceKey = 'memory.server.wave11';
const runtimeSourceId = '00000000-0000-0000-0000-00000000b511';
const tinyPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl2sAAAAASUVORK5CYII=',
  'base64'
);

test('Active persisted Engineering revision is the mounted HMI Runtime truth', async ({ page, request }) => {
  await page.goto('/');
  await expect(page.getByTestId('runtime-simulation-fallback')).toBeVisible();

  const initialProjectionResponse = await request.get('/api/runtime/application');
  expect(initialProjectionResponse.ok()).toBeTruthy();
  const initialProjection = await initialProjectionResponse.json() as {
    mode: string;
    revision?: number | null;
    package?: unknown;
  };
  expect(initialProjection.mode).toBe('simulation');
  expect(initialProjection.revision ?? null).toBeNull();
  expect(initialProjection.package ?? null).toBeNull();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as { changeVersion: number };
  const assetImportResponse = await request.post(
    '/api/engineering/visual-assets/import?key=wave11.active.asset&name=Wave%2011%20Active%20Asset&fileName=wave11-active.png',
    {
      headers: {
        accept: 'application/json',
        'content-type': 'application/octet-stream',
        'x-elitescada-workspace-version': String(workspace.changeVersion)
      },
      data: tinyPng
    }
  );
  expect(assetImportResponse.ok(), `Asset import failed: HTTP ${assetImportResponse.status()} ${await assetImportResponse.text()}`).toBeTruthy();
  const importedAsset = await assetImportResponse.json() as {
    asset: { id: string; mediaType: string; sha256: string };
    assetRef: { assetId: string };
    workspaceVersion: number;
  };
  expect(importedAsset.asset.mediaType).toBe('image/png');
  expect(importedAsset.assetRef.assetId).toBe(`asset:${importedAsset.asset.id}`);

  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const workingA = await workingResponse.json() as any;
  const baselineScreen = workingA.screens.find((screen: any) => screen.key === 'demo.overview');
  expect(baselineScreen).toBeTruthy();
  const baselinePressure = baselineScreen.elements.find((element: any) => element.key === 'pressure');
  expect(baselinePressure?.properties?.label).toBe('Pressão');
  expect(workingA.visualAssets.some((asset: any) => asset.id === importedAsset.asset.id)).toBeTruthy();

  baselineScreen.elements.push({
    id: '00000000-0000-0000-0000-00000000a511',
    key: 'active-asset',
    type: 'core.image',
    properties: {
      x: 720,
      y: 18,
      width: 32,
      height: 32,
      assetRef: importedAsset.assetRef,
      imageFit: 'contain'
    }
  });

  // The built-in simulation source is intentionally a host fallback and is not
  // an activatable Engineering source. Convert this deterministic fixture to
  // Server Memory so the lifecycle test exercises a real Active Runtime without
  // depending on external PLCs, brokers or network timing.
  workingA.dataSources = [{
    id: runtimeSourceId,
    key: runtimeSourceKey,
    name: 'Wave 11 Server Memory',
    driver: 'builtin.memory.server',
    enabled: true
  }];
  workingA.tags = workingA.tags.map((tag: any) => ({
    ...tag,
    source: runtimeSourceKey,
    address: null,
    dataSourceId: runtimeSourceId
  }));

  const activatableWorkingResponse = await request.post('/api/engineering/import/json/apply', {
    data: workingA
  });
  expect(activatableWorkingResponse.ok()).toBeTruthy();

  const saveAResponse = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 11 E2E' }
  });
  expect(saveAResponse.ok()).toBeTruthy();
  const savedA = await saveAResponse.json() as { revision: number };
  expect(savedA.revision).toBeGreaterThan(0);

  const publishAResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/revisions/${savedA.revision}/publish`,
    { data: {} }
  );
  expect(publishAResponse.ok()).toBeTruthy();

  const activateAResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/published/activate`,
    { data: {} }
  );
  expect(activateAResponse.ok(), `Activate A failed: HTTP ${activateAResponse.status()} ${await activateAResponse.text()}`).toBeTruthy();

  const activeApplication = page.getByTestId('runtime-engineering-application');
  await expect(activeApplication).toBeVisible();
  await expect(activeApplication).toHaveAttribute('data-runtime-project-key', projectKey);
  await expect(activeApplication).toHaveAttribute('data-runtime-revision', String(savedA.revision));
  await expect(page.getByTestId('runtime-simulation-fallback')).toHaveCount(0);
  await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key', 'demo.overview');
  const activeCanvas = page.getByTestId('runtime-engineering-canvas');
  await expect(activeCanvas.getByText('Pressão', { exact: true })).toBeVisible();

  const activeAssetImage = activeCanvas.locator('img[alt="active-asset"]');
  await expect(activeAssetImage).toBeVisible();
  await expect(activeAssetImage).toHaveAttribute(
    'src',
    new RegExp(`/api/runtime/visual-assets/${importedAsset.asset.id}/content$`, 'i')
  );
  const activeAssetPayloadResponse = await request.get(
    `/api/runtime/visual-assets/${importedAsset.asset.id}/content`
  );
  expect(activeAssetPayloadResponse.ok()).toBeTruthy();
  expect(activeAssetPayloadResponse.headers()['content-type']).toContain('image/png');

  const operatorProjectionResponse = await request.get('/api/runtime/application', {
    headers: { Authorization: `Bearer ${operatorToken}` }
  });
  expect(operatorProjectionResponse.ok()).toBeTruthy();
  const operatorWorkingResponse = await request.get('/api/engineering/export/json', {
    headers: { Authorization: `Bearer ${operatorToken}` }
  });
  expect(operatorWorkingResponse.status()).toBe(403);

  const activeAResponse = await request.get('/api/runtime/application');
  expect(activeAResponse.ok()).toBeTruthy();
  const activeA = await activeAResponse.json() as any;
  expect(activeA.mode).toBe('engineering');
  expect(activeA.projectKey).toBe(projectKey);
  expect(activeA.revision).toBe(savedA.revision);
  expect(activeA.package.visualAssets.some((asset: any) => asset.id === importedAsset.asset.id)).toBeTruthy();
  expect(activeA.package.screens.find((screen: any) => screen.key === 'demo.overview')
    .elements.find((element: any) => element.key === 'pressure').properties.label).toBe('Pressão');

  const workingB = structuredClone(workingA);
  const screenB = workingB.screens.find((screen: any) => screen.key === 'demo.overview');
  const pressureB = screenB.elements.find((element: any) => element.key === 'pressure');
  pressureB.properties = { ...pressureB.properties, label: 'REVISION B ACTIVE' };

  const applyWorkingBResponse = await request.post('/api/engineering/import/json/apply', {
    data: workingB
  });
  expect(applyWorkingBResponse.ok()).toBeTruthy();

  await page.waitForTimeout(3500);
  await expect(activeApplication).toHaveAttribute('data-runtime-revision', String(savedA.revision));
  await expect(activeCanvas.getByText('REVISION B ACTIVE', { exact: true })).toHaveCount(0);
  await expect(activeCanvas.getByText('Pressão', { exact: true })).toBeVisible();
  await expect(activeAssetImage).toHaveAttribute(
    'src',
    new RegExp(`/api/runtime/visual-assets/${importedAsset.asset.id}/content$`, 'i')
  );

  const projectionDuringWorkingResponse = await request.get('/api/runtime/application');
  expect(projectionDuringWorkingResponse.ok()).toBeTruthy();
  const projectionDuringWorking = await projectionDuringWorkingResponse.json() as any;
  expect(projectionDuringWorking.revision).toBe(savedA.revision);
  expect(projectionDuringWorking.package.screens.find((screen: any) => screen.key === 'demo.overview')
    .elements.find((element: any) => element.key === 'pressure').properties.label).toBe('Pressão');

  const saveBResponse = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 11 E2E' }
  });
  expect(saveBResponse.ok()).toBeTruthy();
  const savedB = await saveBResponse.json() as { revision: number; basedOnRevision?: number | null };
  expect(savedB.revision).toBeGreaterThan(savedA.revision);
  expect(savedB.basedOnRevision).toBe(savedA.revision);

  const publishBResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/revisions/${savedB.revision}/publish`,
    { data: {} }
  );
  expect(publishBResponse.ok()).toBeTruthy();

  const activateBResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/published/activate`,
    { data: {} }
  );
  expect(activateBResponse.ok(), `Activate B failed: HTTP ${activateBResponse.status()} ${await activateBResponse.text()}`).toBeTruthy();

  await expect(activeApplication).toHaveAttribute('data-runtime-revision', String(savedB.revision));
  await expect(page.getByTestId('runtime-engineering-canvas').getByText('REVISION B ACTIVE', { exact: true })).toBeVisible();

  const activeBResponse = await request.get('/api/runtime/application');
  expect(activeBResponse.ok()).toBeTruthy();
  const activeB = await activeBResponse.json() as any;
  expect(activeB.mode).toBe('engineering');
  expect(activeB.revision).toBe(savedB.revision);
  expect(activeB.package.screens.find((screen: any) => screen.key === 'demo.overview')
    .elements.find((element: any) => element.key === 'pressure').properties.label).toBe('REVISION B ACTIVE');
});

test('C05 canonical visual properties survive Save Publish Activate and drive Active HMI rendering', async ({ page, request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;
  const screen = working.screens.find((candidate: any) => candidate.key === 'demo.overview');
  expect(screen).toBeTruthy();

  const objectId = '00000000-0000-0000-0000-00000000c505';
  const objectKey = 'c05-property-lifecycle';
  const textObjectId = '00000000-0000-0000-0000-00000000c506';
  const textObjectKey = 'c05-text-lifecycle';
  screen.elements = screen.elements.filter((element: any) =>
    element.key !== objectKey && element.key !== textObjectKey);
  screen.elements.push({
    id: objectId,
    key: objectKey,
    type: 'core.rectangle',
    properties: {
      x: 84,
      y: 96,
      width: 180,
      height: 90,
      rotation: 15,
      scaleX: 0.9,
      scaleY: 1.1,
      horizontalFlip: true,
      verticalFlip: false,
      zIndex: 99,
      visible: true,
      opacity: 0.6,
      tooltip: 'C05 rectangle tooltip',
      enabled: false,
      fillStyle: 'gradient',
      fillColor: '#12345680',
      fillSecondaryColor: '#ABCDEF',
      gradientDirection: 'diagonal-up',
      strokeColor: '#445566',
      strokeWidth: 8,
      strokeStyle: 'none',
      cornerRadius: 12,
      shadowEnabled: true,
      shadowColor: '#01020380',
      shadowOffsetX: 4,
      shadowOffsetY: 6,
      shadowBlur: 10
    }
  });
  screen.elements.push({
    id: textObjectId,
    key: textObjectKey,
    type: 'core.text',
    properties: {
      x: 84,
      y: 200,
      width: 120,
      height: 32,
      zIndex: 100,
      visible: true,
      opacity: 1,
      tooltip: 'C05 text tooltip',
      text: 'C05 text overflow presentation',
      textColor: '#112233',
      fontFamily: 'Arial',
      fontSize: 16,
      fontWeight: 700,
      fontStyle: 'italic',
      underline: true,
      textWrap: false,
      lineHeight: 1.6,
      textOverflow: 'ellipsis',
      horizontalAlignment: 'left',
      verticalAlignment: 'middle'
    }
  });

  const applyResponse = await request.post('/api/engineering/import/json/apply', { data: working });
  expect(applyResponse.ok(), `C05 apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

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
  expect(activateResponse.ok(), `C05 activate failed: HTTP ${activateResponse.status()} ${await activateResponse.text()}`).toBeTruthy();

  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok()).toBeTruthy();
  const active = await activeResponse.json() as any;
  expect(active.revision).toBe(saved.revision);
  const activeScreen = active.package.screens
    .find((candidate: any) => candidate.key === 'demo.overview');
  const activeElement = activeScreen.elements.find((element: any) => element.key === objectKey);
  const activeTextElement = activeScreen.elements.find((element: any) => element.key === textObjectKey);
  expect(activeElement?.properties).toMatchObject({
    rotation: 15,
    scaleX: 0.9,
    scaleY: 1.1,
    horizontalFlip: true,
    verticalFlip: false,
    zIndex: 99,
    visible: true,
    opacity: 0.6,
    tooltip: 'C05 rectangle tooltip',
    enabled: false,
    fillStyle: 'gradient',
    fillColor: '#12345680',
    fillSecondaryColor: '#ABCDEF',
    gradientDirection: 'diagonal-up',
    strokeColor: '#445566',
    strokeWidth: 8,
    strokeStyle: 'none',
    cornerRadius: 12,
    shadowEnabled: true,
    shadowColor: '#01020380',
    shadowOffsetX: 4,
    shadowOffsetY: 6,
    shadowBlur: 10
  });
  expect(activeTextElement?.properties).toMatchObject({
    tooltip: 'C05 text tooltip',
    text: 'C05 text overflow presentation',
    underline: true,
    textWrap: false,
    lineHeight: 1.6,
    textOverflow: 'ellipsis'
  });

  await page.goto('/');
  const activeApplication = page.getByTestId('runtime-engineering-application');
  await expect(activeApplication).toHaveAttribute('data-runtime-revision', String(saved.revision));
  const activeCanvas = page.getByTestId('runtime-engineering-canvas');
  const rendered = activeCanvas.locator(`[data-object-id="${objectId}"]`);
  await expect(rendered).toBeVisible();
  await expect(rendered).toHaveAttribute('title', 'C05 rectangle tooltip');
  await expect(rendered).toHaveAttribute('data-enabled', 'false');
  await expect(rendered).toHaveCSS('pointer-events', 'none');
  const renderedStyle = await rendered.getAttribute('style');
  expect(renderedStyle).toContain('opacity: 0.6');
  expect(renderedStyle).toContain('border-width: 0px');
  expect(renderedStyle).toContain('border-style: none');
  expect(renderedStyle).toContain('rotate(15deg) scale(-0.9, 1.1)');
  const renderedBackground = await rendered.evaluate(element => getComputedStyle(element).backgroundImage);
  expect(renderedBackground).toContain('linear-gradient');
  const renderedFilter = await rendered.evaluate(element => getComputedStyle(element).filter);
  expect(renderedFilter).toContain('drop-shadow');

  const renderedText = activeCanvas.locator(`[data-object-id="${textObjectId}"]`);
  await expect(renderedText).toBeVisible();
  await expect(renderedText).toHaveAttribute('title', 'C05 text tooltip');
  const renderedTextStyle = await renderedText.getAttribute('style');
  expect(renderedTextStyle).toContain('text-decoration-line: underline');
  expect(renderedTextStyle).toContain('line-height: 1.6');
  expect(renderedTextStyle).toContain('white-space: pre');
  expect(renderedTextStyle).toContain('text-overflow: ellipsis');
});

test('an unavailable Active projection fails closed without reading mutable Working', async ({ page }) => {
  let workingReads = 0;
  await page.route('**/api/runtime/application', async route => {
    await route.fulfill({
      status: 409,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Active Engineering Runtime is inconsistent with persisted activation.' })
    });
  });
  await page.route('**/api/engineering/export/json', async route => {
    workingReads++;
    await route.continue();
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-application-error')).toBeVisible();
  await expect(page.getByTestId('runtime-simulation-fallback')).toHaveCount(0);
  await expect(page.getByTestId('runtime-engineering-application')).toHaveCount(0);
  expect(workingReads).toBe(0);
});
