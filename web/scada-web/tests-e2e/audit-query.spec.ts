import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';

test('Audit uses bounded keyset pagination while preserving the array response contract', async ({ request }) => {
  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as Array<{ id: string; path: string }>;
  const frequency = tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(frequency).toBeTruthy();

  expect((await request.post(`/api/tags/${frequency!.id}/write`, { data: { value: 55 } })).status()).toBe(202);
  expect((await request.post(`/api/tags/${frequency!.id}/write`, { data: { value: 56 } })).status()).toBe(202);

  const queryPath = '/api/audit?limit=1&action=tag.write&targetKind=tag&targetId=Demo.P01.Frequency';

  await expect.poll(async () => {
    const response = await request.get(queryPath);
    if (!response.ok()) return 0;
    const events = await response.json() as Array<unknown>;
    return events.length;
  }).toBe(1);

  const firstResponse = await request.get(queryPath);
  expect(firstResponse.ok()).toBeTruthy();
  const firstPage = await firstResponse.json() as Array<{ id: string; action: string; targetId: string }>;
  expect(firstPage).toHaveLength(1);
  expect(firstPage[0].action).toBe('tag.write');
  expect(firstPage[0].targetId).toBe('Demo.P01.Frequency');

  const cursor = firstResponse.headers()['x-elitescada-audit-next-cursor'];
  expect(cursor).toBeTruthy();

  const secondResponse = await request.get(`${queryPath}&cursor=${encodeURIComponent(cursor!)}`);
  expect(secondResponse.ok()).toBeTruthy();
  const secondPage = await secondResponse.json() as Array<{ id: string; action: string; targetId: string }>;
  expect(secondPage).toHaveLength(1);
  expect(secondPage[0].id).not.toBe(firstPage[0].id);

  const invalidCursor = await request.get('/api/audit?cursor=definitely-not-a-valid-cursor');
  expect(invalidCursor.status()).toBe(400);

  const diagnostics = await request.get('/api/audit/diagnostics');
  expect(diagnostics.ok()).toBeTruthy();
  const health = await diagnostics.json() as {
    store: { persistedCount: number };
    buffer: { successfullyForwardedCount: number; queueDepth: number };
    retention: { enabled: boolean; finiteRetentionActive: boolean };
  };
  expect(health.store.persistedCount).toBeGreaterThanOrEqual(2);
  expect(health.buffer.successfullyForwardedCount).toBeGreaterThanOrEqual(2);
  expect(health.buffer.queueDepth).toBeGreaterThanOrEqual(0);
  expect(health.retention.enabled).toBe(false);
  expect(health.retention.finiteRetentionActive).toBe(false);

  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${createE2eJwt('audit-query-operator', ['operator'], 'Audit Query Operator')}` }
  });
  const anonymous = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: '' }
  });

  try {
    expect((await operator.get('/api/audit')).status()).toBe(403);
    expect((await operator.get('/api/audit/diagnostics')).status()).toBe(403);
    expect((await anonymous.get('/api/audit')).status()).toBe(401);
    expect((await anonymous.get('/api/audit/diagnostics')).status()).toBe(401);
  } finally {
    await operator.dispose();
    await anonymous.dispose();
  }
});
