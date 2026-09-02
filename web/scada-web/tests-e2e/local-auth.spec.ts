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
    expect(authConfig.body.initialAdministratorSetupAvailable).toBe(false);
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
    const loginResponsePromise = page.waitForResponse(response =>
      response.url().endsWith('/api/auth/login') &&
      response.request().method() === 'POST' &&
      response.status() === 200);
    await page.locator('button[type="submit"]').click();
    const loginResponse = await loginResponsePromise;
    const loginProfile = await loginResponse.json();
    expect(loginProfile.identityProvider).toBe('local');

    const localSession = await page.evaluate(async () => {
      const response = await fetch('/api/auth/local-session');
      return { status: response.status, body: await response.json() };
    });
    expect(localSession.status).toBe(200);
    expect(localSession.body.authenticated).toBe(true);
    expect(localSession.body.username).toBe('local-developer');

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

    // Validate that the signed HttpOnly cookie is also accepted by the existing
    // realtime authentication path before a truly empty first project removes the
    // seeded Demo TAGs that would otherwise generate test messages.
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

    const firstProjectKey = page.locator('input[name="project-key"]');
    if (await firstProjectKey.isVisible().catch(() => false)) {
      // The first-project requirement is server/store-side. A reload with only the
      // HttpOnly local cookie must keep the authenticated user in this setup flow.
      await page.reload();
      await expect(page.locator('input[name="project-key"]')).toBeVisible();
      await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);

      const projectKey = `e2e-c01-${Date.now()}`;
      await page.locator('input[name="project-key"]').fill(projectKey);
      await page.locator('input[name="project-name"]').fill('E2E C01 First Project');
      await page.locator('button[type="submit"]').click();
      await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });

      const workspace = await page.evaluate(async () => {
        const response = await fetch('/api/engineering/workspace');
        return { status: response.status, body: await response.json() };
      });
      expect(workspace.status).toBe(200);
      expect(workspace.body.projectKey).toBe(projectKey);
      expect(workspace.body.baseRevision).toBeGreaterThanOrEqual(1);
      expect(workspace.body.isDirty).toBe(false);

      // A genuinely new project must not persist the process Demo seeded into the
      // in-memory host workspace. Canonical built-in Dynamos remain available as
      // product library content, and the developer role remains so the new local
      // Administrator is not locked out of the project it just created.
      expect(workspace.body.tagCount).toBe(0);
      expect(workspace.body.alarmCount).toBe(0);
      expect(workspace.body.dataSourceCount).toBe(0);
      expect(workspace.body.templateCount).toBe(0);
      expect(workspace.body.equipmentCount).toBe(0);
      expect(workspace.body.screenCount).toBe(0);
      expect(workspace.body.popupCount).toBe(0);
      expect(workspace.body.commandCount).toBe(0);
      expect(workspace.body.visualAssetCount).toBe(0);
      expect(workspace.body.dynamoCount).toBeGreaterThan(0);
      expect(workspace.body.securityRoleCount).toBe(1);

      const securityRoles = await page.evaluate(async () => {
        const response = await fetch('/api/engineering/security-roles');
        return { status: response.status, body: await response.json() };
      });
      expect(securityRoles.status).toBe(200);
      expect(securityRoles.body).toHaveLength(1);
      expect(securityRoles.body[0].key).toBe('developer');

      // The workspace descriptor does not expose every canonical collection.
      // Verify the actual exported Engineering package as the persistence source of
      // truth so stale Gateways, Reports or script references cannot hide in a
      // supposedly empty first project.
      const canonicalProject = await page.evaluate(async () => {
        const response = await fetch('/api/engineering/export/json');
        return { status: response.status, body: await response.json() };
      });
      expect(canonicalProject.status).toBe(200);
      expect(canonicalProject.body.tags).toHaveLength(0);
      expect(canonicalProject.body.alarms).toHaveLength(0);
      expect(canonicalProject.body.dataSources).toHaveLength(0);
      expect(canonicalProject.body.templates).toHaveLength(0);
      expect(canonicalProject.body.equipment).toHaveLength(0);
      expect(canonicalProject.body.screens).toHaveLength(0);
      expect(canonicalProject.body.popups).toHaveLength(0);
      expect(canonicalProject.body.commands).toHaveLength(0);
      expect(canonicalProject.body.gateways).toHaveLength(0);
      expect(canonicalProject.body.scripts).toHaveLength(0);
      expect(canonicalProject.body.scriptVisualEventReferences).toHaveLength(0);
      expect(canonicalProject.body.visualAssets).toHaveLength(0);
      expect(canonicalProject.body.reports).toHaveLength(0);
      expect(canonicalProject.body.dynamos.length).toBeGreaterThan(0);
      expect(canonicalProject.body.securityRoles).toHaveLength(1);
      expect(canonicalProject.body.securityRoles[0].key).toBe('developer');
    } else {
      // Other parallel E2E scenarios may already have persisted a project in the
      // shared test database. In that case local login must proceed normally.
      await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });
    }

    await expect(page.locator('.eng-sidebar')).toBeVisible();

    const logoutStatus = await page.evaluate(async () =>
      (await fetch('/api/auth/logout', { method: 'POST' })).status);
    expect(logoutStatus).toBe(204);

    const meAfterLogout = await page.evaluate(async () =>
      (await fetch('/api/auth/me')).status);
    expect(meAfterLogout).toBe(401);

    const localSessionAfterLogout = await page.evaluate(async () => {
      const response = await fetch('/api/auth/local-session');
      return await response.json();
    });
    expect(localSessionAfterLogout.authenticated).toBe(false);

    // The browser no longer has an authenticated cookie, and client-side state is
    // cleared as well. Neither can reopen the server/store-owned bootstrap.
    await page.evaluate(() => window.localStorage.clear());
    const bootstrapRetry = await page.evaluate(async () => {
      const response = await fetch('/api/auth/bootstrap', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          username: 'browser-reset-admin',
          displayName: 'Browser Reset Admin',
          password: '12345678'
        })
      });
      return { status: response.status, body: await response.json() };
    });
    expect(bootstrapRetry.status).toBe(409);
    expect(bootstrapRetry.body.error).toContain('already closed');

    await page.reload();
    await expect(page.locator('.auth-card')).toBeVisible();
    await expect(page.locator('input[name="username"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);
  } finally {
    await context.close();
  }
});
