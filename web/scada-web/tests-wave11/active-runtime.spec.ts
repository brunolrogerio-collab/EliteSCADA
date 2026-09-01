import { expect, test } from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const operatorToken = createE2eJwt('wave11-operator', ['operator'], 'Wave 11 Operator');
const runtimeSourceKey = 'memory.server.wave11';
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
    key: runtimeSourceKey,
    name: 'Wave 11 Server Memory',
    driver: 'builtin.memory.server',
    enabled: true
  }];
  workingA.tags = workingA.tags.map((tag: any) => ({
    ...tag,
    source: runtimeSourceKey,
    address: null
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
