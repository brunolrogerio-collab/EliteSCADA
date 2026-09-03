import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';

const projectKey = 'e2e-wave11';
const packageFileName = 'EliteSCADA-Wave11-Demo.escadapkg';
const packageMediaType = 'application/vnd.elitescada.project-package';

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
    const logicalStage = page.getByTestId('runtime-logical-stage');
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
