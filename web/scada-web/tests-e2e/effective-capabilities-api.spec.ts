import { expect, test } from '@playwright/test';

test('effective capabilities endpoint projects runtime and workspace grants independently', async ({ request }) => {
  const response = await request.get('/api/auth/effective-capabilities');
  expect(response.ok()).toBe(true);

  const payload = await response.json() as {
    authenticationEnabled: boolean;
    runtime: string[];
    workspace: string[];
  };

  expect(payload.authenticationEnabled).toBe(true);
  expect(Array.isArray(payload.runtime)).toBe(true);
  expect(Array.isArray(payload.workspace)).toBe(true);
  expect(payload.workspace).toContain('EngineeringModify');

  for (const capabilities of [payload.runtime, payload.workspace]) {
    expect(new Set(capabilities).size).toBe(capabilities.length);
  }
});

test('effective capabilities endpoint requires authentication when authentication is enabled', async ({ playwright }) => {
  const anonymous = await playwright.request.newContext({ baseURL: 'http://127.0.0.1:5173' });
  try {
    const response = await anonymous.get('/api/auth/effective-capabilities');
    expect(response.status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }
});
