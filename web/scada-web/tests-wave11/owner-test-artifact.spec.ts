import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const packageFileName = 'EliteSCADA-Wave11-Demo.escadapkg';
const packageMediaType = 'application/vnd.elitescada.project-package';
const runtimeOnlyToken = createE2eJwt('wave11-runtime-only-c09', ['operator'], 'Wave 11 Runtime Operator');

test('exports the owner-test package from the verified Active Engineering application', async ({ request }) => {
  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok(), `Active Runtime lookup failed: HTTP ${activeResponse.status()} ${await activeResponse.text()}`).toBeTruthy();
  const active = await activeResponse.json() as any;

  expect(active.mode).toBe('engineering');
  expect(active.projectKey).toBe(projectKey);
  expect(active.revision).toBeGreaterThan(0);
  const activeScreen = active.package.screens.find((screen: any) => screen.key === 'demo.overview');
  expect(activeScreen).toBeTruthy();
  expect(activeScreen.elements.find((element: any) => element.key === 'pressure')?.properties?.label)
    .toBe('REVISION B ACTIVE');
  expect(active.package.visualAssets.length).toBeGreaterThan(0);

  const packageResponse = await request.get(
    `/api/project-package/export?projectKey=${encodeURIComponent(projectKey)}&projectName=${encodeURIComponent('EliteSCADA Wave 11 Demo')}`
  );
  expect(packageResponse.ok(), `Package export failed: HTTP ${packageResponse.status()} ${await packageResponse.text()}`).toBeTruthy();
  expect(packageResponse.headers()['content-type']).toContain(packageMediaType);
  const packageBytes = await packageResponse.body();
  expect(packageBytes.length).toBeGreaterThan(0);

  const inspectionResponse = await request.post('/api/project-package/inspect', {
    headers: {
      accept: 'application/json',
      'content-type': packageMediaType
    },
    data: packageBytes
  });
  expect(
    inspectionResponse.ok(),
    `Package inspection failed: HTTP ${inspectionResponse.status()} ${await inspectionResponse.text()}`
  ).toBeTruthy();
  const inspection = await inspectionResponse.json() as any;
  expect(inspection.manifest.projectKey).toBe(projectKey);
  expect(inspection.manifest.product).toBe('EliteSCADA');
  expect(inspection.manifest.engineeringSchema).toBe('scada.engineering');
  expect(inspection.engineering.screens).toBeGreaterThan(0);
  expect(inspection.engineering.visualAssets).toBeGreaterThan(0);

  const previewResponse = await request.post('/api/project-package/import/preview?mode=CreateAndUpdate', {
    headers: {
      accept: 'application/json',
      'content-type': packageMediaType
    },
    data: packageBytes
  });
  expect(
    previewResponse.ok(),
    `Package preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`
  ).toBeTruthy();
  const preview = await previewResponse.json() as { errorCount: number };
  expect(preview.errorCount).toBe(0);

  const outputDirectory = path.resolve(process.cwd(), 'owner-test-artifacts');
  await mkdir(outputDirectory, { recursive: true });
  const packagePath = path.join(outputDirectory, packageFileName);
  await writeFile(packagePath, packageBytes);

  const sha256 = createHash('sha256').update(packageBytes).digest('hex');
  await writeFile(
    path.join(outputDirectory, `${packageFileName}.sha256`),
    `${sha256}  ${packageFileName}\n`,
    'utf8'
  );
  await writeFile(
    path.join(outputDirectory, 'owner-test-metadata.json'),
    `${JSON.stringify({
      projectKey,
      projectName: 'EliteSCADA Wave 11 Demo',
      activeRevision: active.revision,
      packageFileName,
      sha256,
      validatedAgainstActiveRuntime: true,
      exportBoundary: '/api/project-package/export',
      validationBoundaries: [
        '/api/runtime/application',
        '/api/project-package/inspect',
        '/api/project-package/import/preview'
      ]
    }, null, 2)}\n`,
    'utf8'
  );
});

test('Active HMI logical viewport scales and centers uniformly at representative browser resolutions', async ({ page }) => {
  for (const browserViewport of [
    { width: 1280, height: 720 },
    { width: 1920, height: 1080 },
    { width: 2560, height: 1440 },
    { width: 3840, height: 2160 }
  ]) {
    await page.setViewportSize(browserViewport);
    await page.goto('/');

    const application = page.getByTestId('runtime-engineering-application');
    const logicalViewport = page.getByTestId('runtime-logical-viewport');
    await expect(application).toHaveAttribute('data-runtime-project-key', projectKey);
    await expect(logicalViewport).toBeVisible();
    await expect(logicalViewport).toHaveAttribute('data-design-width', '1920');
    await expect(logicalViewport).toHaveAttribute('data-design-height', '1080');
    await expect.poll(async () => Number(await logicalViewport.getAttribute('data-runtime-scale'))).toBeGreaterThan(0);

    const geometry = await logicalViewport.evaluate((viewport, stageTestId) => {
      const stage = viewport.querySelector<HTMLElement>(`[data-testid="${stageTestId}"]`);
      if (!stage) throw new Error('Runtime logical stage was not mounted.');
      const viewportElement = viewport as HTMLElement;
      const viewportRect = viewportElement.getBoundingClientRect();
      const stageRect = stage.getBoundingClientRect();
      const matrix = new DOMMatrixReadOnly(getComputedStyle(stage).transform);
      return {
        viewportWidth: viewportElement.clientWidth,
        viewportHeight: viewportElement.clientHeight,
        viewportLeft: viewportRect.left,
        viewportTop: viewportRect.top,
        stageLeft: stageRect.left,
        stageTop: stageRect.top,
        stageWidth: stageRect.width,
        stageHeight: stageRect.height,
        matrixScaleX: matrix.a,
        matrixScaleY: matrix.d,
        reportedScale: Number(viewportElement.dataset.runtimeScale)
      };
    }, 'runtime-logical-stage');

    const expectedScale = Math.min(
      geometry.viewportWidth / 1920,
      geometry.viewportHeight / 1080
    );
    const expectedWidth = 1920 * expectedScale;
    const expectedHeight = 1080 * expectedScale;
    const expectedOffsetX = (geometry.viewportWidth - expectedWidth) / 2;
    const expectedOffsetY = (geometry.viewportHeight - expectedHeight) / 2;

    expect(geometry.reportedScale).toBeCloseTo(expectedScale, 5);
    expect(geometry.matrixScaleX).toBeCloseTo(expectedScale, 5);
    expect(geometry.matrixScaleY).toBeCloseTo(expectedScale, 5);
    expect(geometry.stageWidth).toBeCloseTo(expectedWidth, 1);
    expect(geometry.stageHeight).toBeCloseTo(expectedHeight, 1);
    expect(geometry.stageLeft - geometry.viewportLeft).toBeCloseTo(expectedOffsetX, 1);
    expect(geometry.stageTop - geometry.viewportTop).toBeCloseTo(expectedOffsetY, 1);
  }
});

test('runtime-only operator sees only permitted application surfaces and backend remains authoritative', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: 'http://127.0.0.1:5174',
    extraHTTPHeaders: { Authorization: `Bearer ${runtimeOnlyToken}` }
  });
  const page = await context.newPage();

  try {
    await page.goto('/');
    const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
    await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
    await expect(navigation.getByRole('link', { name: /Runtime/ })).toBeVisible();
    await expect(navigation.getByRole('link', { name: /Engineering/ })).toHaveCount(0);
    await expect(navigation.getByRole('link', { name: /Audit|Auditoria|Auditoría/ })).toHaveCount(0);
    await expect(navigation.getByRole('link', { name: /Licensing|Licenciamento|Licenciamiento/ })).toHaveCount(0);

    const capabilityResponse = await context.request.get('/api/auth/effective-capabilities');
    expect(capabilityResponse.ok()).toBeTruthy();
    const capabilities = await capabilityResponse.json() as { runtime: string[]; workspace: string[] };
    expect(capabilities.runtime).toContain('View');
    expect(capabilities.workspace).not.toContain('EngineeringModify');

    await page.goto('/engineering');
    await expect(page.locator('.eng-shell')).toHaveCount(0);
    await expect(page.getByRole('alert')).toBeVisible();

    const engineeringResponse = await context.request.get('/api/engineering/workspace');
    const auditResponse = await context.request.get('/api/audit/diagnostics');
    const licensingResponse = await context.request.get('/api/licensing/status');
    expect(engineeringResponse.status()).toBe(403);
    expect(auditResponse.status()).toBe(403);
    expect(licensingResponse.status()).toBe(403);
  } finally {
    await context.close();
  }
});

test('operator controls keep alarms as an overlay and support native fullscreen on the Active Runtime', async ({ page }) => {
  await page.goto('/');
  const application = page.getByTestId('runtime-engineering-application');
  const canvas = page.getByTestId('runtime-engineering-canvas');
  await expect(application).toBeVisible();
  await expect(canvas).toBeVisible();

  const before = await canvas.boundingBox();
  expect(before).not.toBeNull();

  const alarmsButton = page.getByRole('button', { name: /Alarms|Alarmes|Alarmas/ }).first();
  await alarmsButton.click();
  const alarmOverlay = page.locator('.runtime-operator-overlay');
  await expect(alarmOverlay).toBeVisible();
  const after = await canvas.boundingBox();
  expect(after).not.toBeNull();
  expect(after!.width).toBeCloseTo(before!.width, 1);
  expect(after!.height).toBeCloseTo(before!.height, 1);
  await page.getByRole('button', { name: /Close alarms|Fechar alarmes|Cerrar alarmas/ }).click();
  await expect(alarmOverlay).toHaveCount(0);

  const enterFullscreen = page.getByRole('button', { name: /Fullscreen|Tela cheia|Pantalla completa/ }).first();
  await enterFullscreen.click();
  await expect(application).toHaveAttribute('data-runtime-fullscreen', 'true');
  await expect.poll(async () => page.evaluate(() => document.fullscreenElement?.getAttribute('data-testid') ?? null))
    .toBe('runtime-engineering-application');

  const exitFullscreen = page.getByRole('button', { name: /Exit fullscreen|Sair da tela cheia|Salir de pantalla completa/ }).first();
  await exitFullscreen.click();
  await expect.poll(async () => page.evaluate(() => document.fullscreenElement === null)).toBe(true);
});
