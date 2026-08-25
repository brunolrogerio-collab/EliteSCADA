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
  const engineeringJson = await exportResponse.text();
  expect(engineeringJson).toContain('Demo.Tank01.Level');
  expect(engineeringJson).toContain('Demo.P01.Frequency');

  const alarmResponse = await request.get('/api/alarms?activeOnly=true');
  expect(alarmResponse.ok()).toBeTruthy();
  const alarms = await alarmResponse.json() as Array<{ definitionId: string }>;
  expect(alarms.length).toBeGreaterThan(0);

  const ackResponse = await request.post(`/api/alarms/${alarms[0].definitionId}/ack`, {
    data: { user: 'e2e-operator' }
  });
  expect(ackResponse.ok()).toBeTruthy();
});
