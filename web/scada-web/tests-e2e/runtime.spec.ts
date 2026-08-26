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
  const engineering = await exportResponse.json() as {
    schemaVersion: number;
    tags: Array<{ path: string; source?: string }>;
    dataSources: Array<{ key: string; driver: string }>;
  };
  expect(engineering.schemaVersion).toBe(2);
  expect(engineering.tags.some(tag => tag.path === 'Demo.Tank01.Level')).toBeTruthy();
  expect(engineering.tags.some(tag => tag.path === 'Demo.P01.Frequency')).toBeTruthy();
  expect(engineering.tags.every(tag => tag.source === 'builtin.simulation')).toBeTruthy();
  expect(engineering.dataSources).toHaveLength(1);
  expect(engineering.dataSources[0].key).toBe('builtin.simulation');

  const dataSourceCsvResponse = await request.get('/api/engineering/export/datasources.csv');
  expect(dataSourceCsvResponse.ok()).toBeTruthy();
  const dataSourceCsv = await dataSourceCsvResponse.text();
  expect(dataSourceCsv).toContain('builtin.simulation');
  expect(dataSourceCsv).toContain('scanIntervalMilliseconds');

  const tagsCsvResponse = await request.get('/api/engineering/export/tags.csv');
  expect(tagsCsvResponse.ok()).toBeTruthy();
  const tagsCsv = await tagsCsvResponse.text();
  const tagsPreviewResponse = await request.post('/api/engineering/import/tags.csv/preview', {
    data: tagsCsv,
    headers: { 'content-type': 'text/csv; charset=utf-8' }
  });
  expect(tagsPreviewResponse.ok()).toBeTruthy();
  const tagsPreview = await tagsPreviewResponse.json() as { errorCount: number; canApply: boolean };
  expect(tagsPreview.errorCount).toBe(0);
  expect(tagsPreview.canApply).toBeTruthy();

  const alarmResponse = await request.get('/api/alarms?activeOnly=true');
  expect(alarmResponse.ok()).toBeTruthy();
  const alarms = await alarmResponse.json() as Array<{ definitionId: string }>;
  expect(alarms.length).toBeGreaterThan(0);

  const ackResponse = await request.post(`/api/alarms/${alarms[0].definitionId}/ack`, {
    data: { user: 'e2e-operator' }
  });
  expect(ackResponse.ok()).toBeTruthy();
});
