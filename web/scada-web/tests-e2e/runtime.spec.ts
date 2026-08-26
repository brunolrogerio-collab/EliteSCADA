import { expect, test } from '@playwright/test';

test('SCADA runtime operates end-to-end in Chromium', async ({ page, request }) => {
  await page.goto('/');

  await expect(page.getByText('SCADA Platform')).toBeVisible();
  await expect(page.getByText(/ONLINE · 7 TAGs/)).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText('Reservatório TK01')).toBeVisible();
  await expect(page.getByTitle('Abrir detalhes da bomba')).toBeVisible();

  const tagResponse = await request.get('/api/tags');
  expect(tagResponse.ok()).toBeTruthy();
  const tags = await tagResponse.json() as Array<{ id: string; path: string; readOnly: boolean }>;
  expect(tags).toHaveLength(7);

  const frequencyTag = tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(frequencyTag).toBeTruthy();
  expect(frequencyTag!.readOnly).toBeFalsy();

  const writeResponse = await request.post(`/api/tags/${frequencyTag!.id}/write`, {
    data: { value: 50 }
  });
  expect(writeResponse.status()).toBe(202);
  await expect(page.getByText('50.0 Hz')).toBeVisible({ timeout: 10_000 });

  await page.getByTitle('Abrir detalhes da bomba').click();
  await expect(page.getByText('Bomba P01')).toBeVisible();
  await expect(page.getByText('Histórico recente · Corrente')).toBeVisible();
  await expect.poll(async () => page.locator('.spark-values span').count(), { timeout: 10_000 }).toBeGreaterThan(0);

  const exportResponse = await request.get('/api/engineering/export/json');
  expect(exportResponse.ok()).toBeTruthy();
  const engineeringText = await exportResponse.text();
  const engineering = JSON.parse(engineeringText) as {
    schemaVersion: number;
    tags: Array<{ path: string; source?: string }>;
    dataSources: Array<{ key: string; driver: string }>;
    templates: Array<{ key: string; bindings: Array<{ key: string; target: string }> }>;
    equipment: Array<{ path: string; templateKey?: string; bindings: Array<{ key: string; target: string }> }>;
    dynamos: Array<{ key: string; templateKey?: string }>;
    screens: Array<{
      key: string;
      route?: string;
      elements: Array<{ key: string; type: string; dynamoKey?: string; equipmentPath?: string; bindings?: Array<{ key: string; target: string }> }>;
    }>;
    popups: Array<{
      key: string;
      templateKey?: string;
      elements: Array<{ key: string; type: string; bindings?: Array<{ key: string; target: string }> }>;
    }>;
  };
  expect(engineering.schemaVersion).toBe(5);
  expect(engineering.tags.some(tag => tag.path === 'Demo.Tank01.Level')).toBeTruthy();
  expect(engineering.tags.some(tag => tag.path === 'Demo.P01.Frequency')).toBeTruthy();
  expect(engineering.tags.every(tag => tag.source === 'builtin.simulation')).toBeTruthy();
  expect(engineering.dataSources).toHaveLength(1);
  expect(engineering.dataSources[0].key).toBe('builtin.simulation');
  expect(engineering.templates).toHaveLength(1);
  expect(engineering.templates[0].key).toBe('pump.standard');
  expect(engineering.templates[0].bindings.some(binding => binding.target === '{equipmentPath}.Running')).toBeTruthy();
  expect(engineering.equipment).toHaveLength(1);
  expect(engineering.equipment[0].path).toBe('Demo.P01');
  expect(engineering.equipment[0].templateKey).toBe('pump.standard');
  expect(engineering.equipment[0].bindings.some(binding => binding.target === 'Demo.P01.Frequency')).toBeTruthy();
  expect(engineering.dynamos).toHaveLength(1);
  expect(engineering.dynamos[0].key).toBe('dynamo.pump.standard');
  expect(engineering.dynamos[0].templateKey).toBe('pump.standard');

  expect(engineering.screens).toHaveLength(1);
  expect(engineering.screens[0].key).toBe('demo.overview');
  expect(engineering.screens[0].route).toBe('/demo');
  const pumpElement = engineering.screens[0].elements.find(element => element.key === 'pump01');
  expect(pumpElement).toBeTruthy();
  expect(pumpElement!.dynamoKey).toBe('dynamo.pump.standard');
  expect(pumpElement!.equipmentPath).toBe('Demo.P01');
  const pressureElement = engineering.screens[0].elements.find(element => element.key === 'pressure');
  expect(pressureElement?.bindings?.some(binding => binding.target === 'Demo.Discharge.Pressure')).toBeTruthy();

  expect(engineering.popups).toHaveLength(1);
  expect(engineering.popups[0].key).toBe('popup.pump.standard');
  expect(engineering.popups[0].templateKey).toBe('pump.standard');
  const popupFrequency = engineering.popups[0].elements.find(element => element.key === 'frequency');
  expect(popupFrequency?.bindings?.some(binding => binding.target === '{equipmentPath}.Frequency')).toBeTruthy();

  const screensResponse = await request.get('/api/engineering/screens');
  expect(screensResponse.ok()).toBeTruthy();
  const screens = await screensResponse.json() as Array<{ key: string }>;
  expect(screens.map(screen => screen.key)).toContain('demo.overview');

  const popupsResponse = await request.get('/api/engineering/popups');
  expect(popupsResponse.ok()).toBeTruthy();
  const popups = await popupsResponse.json() as Array<{ key: string }>;
  expect(popups.map(popup => popup.key)).toContain('popup.pump.standard');

  const packagePreviewResponse = await request.post('/api/engineering/import/json/preview', {
    data: engineeringText,
    headers: { 'content-type': 'application/json; charset=utf-8' }
  });
  expect(packagePreviewResponse.ok()).toBeTruthy();
  const packagePreview = await packagePreviewResponse.json() as { errorCount: number; canApply: boolean };
  expect(packagePreview.errorCount).toBe(0);
  expect(packagePreview.canApply).toBeTruthy();

  const dataSourceCsvResponse = await request.get('/api/engineering/export/datasources.csv');
  expect(dataSourceCsvResponse.ok()).toBeTruthy();
  const dataSourceCsv = await dataSourceCsvResponse.text();
  expect(dataSourceCsv).toContain('builtin.simulation');
  expect(dataSourceCsv).toContain('scanIntervalMilliseconds');

  const tagsCsvResponse = await request.get('/api/engineering/export/tags.csv');
  expect(tagsCsvResponse.ok()).toBeTruthy();
  const tagsCsv = await tagsCsvResponse.text();
  expect(tagsCsv).toContain('MaximumPeriodMilliseconds');
  expect(tagsCsv).toContain('MetadataJson');
  expect(tagsCsv).toContain('ReadRolesJson');
  expect(tagsCsv).toContain('WriteRolesJson');
  expect(tagsCsv).toContain('ConfigureRolesJson');
  const tagsPreviewResponse = await request.post('/api/engineering/import/tags.csv/preview', {
    data: tagsCsv,
    headers: { 'content-type': 'text/csv; charset=utf-8' }
  });
  expect(tagsPreviewResponse.ok()).toBeTruthy();
  const tagsPreview = await tagsPreviewResponse.json() as { errorCount: number; canApply: boolean };
  expect(tagsPreview.errorCount).toBe(0);
  expect(tagsPreview.canApply).toBeTruthy();

  const projectPackageResponse = await request.get('/api/project-package/export?projectKey=demo&projectName=Demo%20Project');
  expect(projectPackageResponse.ok()).toBeTruthy();
  expect(projectPackageResponse.headers()['content-type']).toContain('application/vnd.elitescada.project-package');
  const projectPackage = await projectPackageResponse.body();
  expect(projectPackage.length).toBeGreaterThan(0);

  const projectInspectResponse = await request.post('/api/project-package/inspect', {
    data: projectPackage,
    headers: { 'content-type': 'application/vnd.elitescada.project-package' }
  });
  expect(projectInspectResponse.ok()).toBeTruthy();
  const projectInspect = await projectInspectResponse.json() as {
    manifest: {
      format: string;
      formatVersion: number;
      projectKey: string;
      projectName: string;
      engineeringSchemaVersion: number;
      files: Array<{ path: string; sha256: string }>;
    };
    engineering: { tags: number; dataSources: number; screens: number; popups: number };
  };
  expect(projectInspect.manifest.format).toBe('elitescada.project-package');
  expect(projectInspect.manifest.formatVersion).toBe(1);
  expect(projectInspect.manifest.projectKey).toBe('demo');
  expect(projectInspect.manifest.projectName).toBe('Demo Project');
  expect(projectInspect.manifest.engineeringSchemaVersion).toBe(5);
  expect(projectInspect.manifest.files).toHaveLength(1);
  expect(projectInspect.manifest.files[0].path).toBe('engineering.json');
  expect(projectInspect.manifest.files[0].sha256).toHaveLength(64);
  expect(projectInspect.engineering.tags).toBe(7);
  expect(projectInspect.engineering.dataSources).toBe(1);
  expect(projectInspect.engineering.screens).toBe(1);
  expect(projectInspect.engineering.popups).toBe(1);

  const projectPreviewResponse = await request.post('/api/project-package/import/preview', {
    data: projectPackage,
    headers: { 'content-type': 'application/vnd.elitescada.project-package' }
  });
  expect(projectPreviewResponse.ok()).toBeTruthy();
  const projectPreview = await projectPreviewResponse.json() as { errorCount: number; canApply: boolean };
  expect(projectPreview.errorCount).toBe(0);
  expect(projectPreview.canApply).toBeTruthy();

  const alarmResponse = await request.get('/api/alarms?activeOnly=true');
  expect(alarmResponse.ok()).toBeTruthy();
  const alarms = await alarmResponse.json() as Array<{ definitionId: string }>;
  expect(alarms.length).toBeGreaterThan(0);

  const ackResponse = await request.post(`/api/alarms/${alarms[0].definitionId}/ack`, {
    data: { user: 'e2e-operator' }
  });
  expect(ackResponse.ok()).toBeTruthy();
});
