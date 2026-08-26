import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';

test('API distinguishes unauthenticated, forbidden and developer access', async ({ request }) => {
  const meResponse = await request.get('/api/auth/me');
  expect(meResponse.ok()).toBeTruthy();
  const me = await meResponse.json() as { subjectId: string; displayName: string; roles: string[] };
  expect(me.subjectId).toBe('e2e-developer');
  expect(me.displayName).toBe('E2E Developer');
  expect(me.roles).toContain('developer');

  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as Array<{ id: string; path: string; readOnly: boolean }>;
  const frequency = tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(frequency).toBeTruthy();
  expect(frequency!.readOnly).toBeFalsy();

  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineeringJson = await engineeringResponse.text();

  const anonymous = await playwrightRequest.newContext({ baseURL });
  try {
    expect((await anonymous.get('/api/auth/me')).status()).toBe(401);
    expect((await anonymous.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 51 }
    })).status()).toBe(401);
    expect((await anonymous.post('/api/engineering/import/json/apply', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }

  const operatorToken = createE2eJwt('e2e-operator', ['operator'], 'E2E Operator');
  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });
  try {
    const operatorMe = await operator.get('/api/auth/me');
    expect(operatorMe.ok()).toBeTruthy();

    // The demo operator can execute operational commands, but ProcessValueWrite is deliberately absent.
    expect((await operator.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 52 }
    })).status()).toBe(403);

    // Engineering mutations require EngineeringModify, which the operator also does not have.
    expect((await operator.post('/api/engineering/import/json/apply', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(403);
  } finally {
    await operator.dispose();
  }
});
