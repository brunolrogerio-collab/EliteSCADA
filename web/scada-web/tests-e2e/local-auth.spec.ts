import { expect, test } from '@playwright/test';

test('local login authenticates Runtime and Engineering with an HttpOnly JWT cookie', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: { Authorization: '' }
  });
  const page = await context.newPage();

  try {
    const authConfig = await page.evaluate(async () => {
      const response = await fetch('/api/auth/config');
      return { status: response.status, body: await response.json() };
    });
    expect(authConfig.status).toBe(200);
    expect(authConfig.body.localLoginEnabled).toBe(true);
    expect(authConfig.body.initialAdministratorRequired).toBe(false);
    expect(authConfig.body.passwordPolicy.minimumLength).toBe(8);
    expect(authConfig.body.passwordPolicy.maximumLength).toBe(1024);

    await page.goto('/engineering');
    await expect(page.locator('.auth-card')).toBeVisible();
    await expect(page.locator('input[name="username"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);
    await expect(page.locator('input[name="password"]')).toBeVisible();

    await page.locator('input[name="username"]').fill('local-developer');
    await page.locator('input[name="password"]').fill('definitely-wrong-password');
    await page.locator('button[type="submit"]').click();
    await expect(page.locator('.auth-error')).toBeVisible();

    await page.locator('input[name="password"]').fill('E2E-local-password-123!');
    await page.locator('button[type="submit"]').click();

    await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.eng-sidebar')).toBeVisible();

    const cookies = await context.cookies('http://127.0.0.1:5173');
    const accessCookie = cookies.find(cookie => cookie.name === 'elitescada_access');
    expect(accessCookie).toBeTruthy();
    expect(accessCookie!.httpOnly).toBeTruthy();
    expect(accessCookie!.secure).toBeFalsy();
    expect(accessCookie!.sameSite).toBe('Strict');
    expect(accessCookie!.value.length).toBeGreaterThan(40);

    const profile = await page.evaluate(async () => {
      const response = await fetch('/api/auth/me');
      return { status: response.status, body: await response.json() };
    });
    expect(profile.status).toBe(200);
    expect(profile.body.displayName).toBe('Local Developer');
    expect(profile.body.roles).toContain('developer');

    const realtime = await page.evaluate(async () => {
      return await new Promise<string>(resolve => {
        const socket = new WebSocket('ws://127.0.0.1:5173/ws/tags');
        const timeout = window.setTimeout(() => {
          socket.close();
          resolve('timeout');
        }, 4000);
        socket.onmessage = event => {
          window.clearTimeout(timeout);
          socket.close();
          resolve(event.data);
        };
        socket.onerror = () => {
          window.clearTimeout(timeout);
          resolve('rejected');
        };
      });
    });
    expect(realtime).not.toBe('timeout');
    expect(realtime).not.toBe('rejected');
    expect(JSON.parse(realtime).type).toBe('tagValueChanged');

    const logoutStatus = await page.evaluate(async () =>
      (await fetch('/api/auth/logout', { method: 'POST' })).status);
    expect(logoutStatus).toBe(204);

    const meAfterLogout = await page.evaluate(async () =>
      (await fetch('/api/auth/me')).status);
    expect(meAfterLogout).toBe(401);

    await page.reload();
    await expect(page.locator('.auth-card')).toBeVisible();
    await expect(page.locator('input[name="username"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);
  } finally {
    await context.close();
  }
});
