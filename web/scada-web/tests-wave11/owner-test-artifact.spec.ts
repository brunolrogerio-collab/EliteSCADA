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
