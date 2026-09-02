import { expect, test } from '@playwright/test';

const adminUsername = 'local-developer';
const adminPassword = 'E2Epass8';

test('secure first-run creates the initial local Administrator, first project and durable local session', async ({ browser }) => {
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
    expect(authConfig.body.initialAdministratorRequired).toBe(true);
    expect(authConfig.body.initialAdministratorSetupAvailable).toBe(true);
    expect(authConfig.body.initialAdministratorBlockedReason).toBeNull();
    expect(authConfig.body.passwordPolicy.minimumLength).toBe(8);
    expect(authConfig.body.passwordPolicy.maximumLength).toBe(1024);

    await page.goto('/engineering');
    await expect(page.locator('.auth-card')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toBeVisible();
    await expect(page.locator('input[name="username"]')).toHaveCount(0);

    await page.locator('input[name="bootstrap-username"]').fill(adminUsername);
    await page.locator('input[name="bootstrap-display-name"]').fill('Local Developer');
    await page.locator('input[name="bootstrap-password"]').fill('1234567');
    await page.locator('input[name="bootstrap-password-confirmation"]').fill('1234567');
    await expect(page.locator('button[type="submit"]')).toBeDisabled();

    await page.locator('input[name="bootstrap-password"]').fill(adminPassword);
    await page.locator('input[name="bootstrap-password-confirmation"]').fill(adminPassword);
    await expect(page.locator('button[type="submit"]')).toBeEnabled();

    const bootstrapResponsePromise = page.waitForResponse(response =>
      response.url().endsWith('/api/auth/bootstrap') &&
      response.request().method() === 'POST' &&
      response.status() === 200);
    await page.locator('button[type="submit"]').click();
    const bootstrapResponse = await bootstrapResponsePromise;
    const bootstrapProfile = await bootstrapResponse.json();
    expect(bootstrapProfile.username).toBe(adminUsername);
    expect(bootstrapProfile.roles).toContain('developer');
    expect(bootstrapProfile.identityProvider).toBe('local');

    const localSession = await page.evaluate(async () => {
      const response = await fetch('/api/auth/local-session');
      return { status: response.status, body: await response.json() };
    });
    expect(localSession.status).toBe(200);
    expect(localSession.body.authenticated).toBe(true);
    expect(localSession.body.username).toBe(adminUsername);

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

    // The fresh server still has the process Demo in memory, but no persisted project.
    // Capture it so this prerequisite test can restore the shared E2E baseline after
    // proving that the first persisted project is genuinely empty.
    const seededEngineering = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/export/json');
      return { status: response.status, body: await response.json() };
    });
    expect(seededEngineering.status).toBe(200);
    expect(seededEngineering.body.tags.length).toBeGreaterThan(0);
    expect(seededEngineering.body.securityRoles.length).toBeGreaterThan(1);

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

    await expect(page.locator('input[name="project-key"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);

    // First-project setup is server/store-owned. Reloading with only the signed
    // HttpOnly local cookie must return to the same setup instead of reopening bootstrap.
    await page.reload();
    await expect(page.locator('input[name="project-key"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);

    const projectKey = 'e2e-wave03';
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

    // The descriptor does not expose every canonical collection, so assert the
    // actual package that persistence/import/export use as the source of truth.
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

    // Restore the original Demo through the canonical API before the dependent
    // Chromium project starts, then save it so the common E2E baseline is clean.
    const restoredImport = await page.evaluate(async seededPackage => {
      const response = await fetch('/api/engineering/import/json/apply', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(seededPackage)
      });
      return { status: response.status, body: await response.json() };
    }, seededEngineering.body);
    expect(restoredImport.status).toBe(200);

    const restoredSave = await page.evaluate(async currentProjectKey => {
      const response = await fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/save`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ projectName: 'E2E Wave 03 Demo Restored', savedBy: 'local-auth-e2e' })
      });
      return { status: response.status, body: await response.json() };
    }, projectKey);
    expect(restoredSave.status).toBe(200);

    const restoredWorkspace = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/workspace');
      return { status: response.status, body: await response.json() };
    });
    expect(restoredWorkspace.status).toBe(200);
    expect(restoredWorkspace.body.projectKey).toBe(projectKey);
    expect(restoredWorkspace.body.tagCount).toBeGreaterThan(0);
    expect(restoredWorkspace.body.securityRoleCount).toBeGreaterThan(1);
    expect(restoredWorkspace.body.isDirty).toBe(false);

    const logoutStatus = await page.evaluate(async () =>
      (await fetch('/api/auth/logout', { method: 'POST' })).status);
    expect(logoutStatus).toBe(204);
    expect(await page.evaluate(async () => (await fetch('/api/auth/me')).status)).toBe(401);

    const localSessionAfterLogout = await page.evaluate(async () => {
      const response = await fetch('/api/auth/local-session');
      return await response.json();
    });
    expect(localSessionAfterLogout.authenticated).toBe(false);

    // Neither browser state nor cookies are authoritative for bootstrap availability.
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

    // After bootstrap is permanently closed, the same Administrator uses the normal
    // login path. Wrong credentials fail; the exact 8-character accepted password
    // succeeds and the resulting signed local session survives a browser reload.
    await page.reload();
    await expect(page.locator('input[name="username"]')).toBeVisible();
    await expect(page.locator('input[name="bootstrap-username"]')).toHaveCount(0);

    await page.locator('input[name="username"]').fill(adminUsername);
    await page.locator('input[name="password"]').fill('definitely-wrong-password');
    await page.locator('button[type="submit"]').click();
    await expect(page.locator('.auth-error')).toBeVisible();

    await page.locator('input[name="password"]').fill(adminPassword);
    const loginResponsePromise = page.waitForResponse(response =>
      response.url().endsWith('/api/auth/login') &&
      response.request().method() === 'POST' &&
      response.status() === 200);
    await page.locator('button[type="submit"]').click();
    const loginResponse = await loginResponsePromise;
    const loginProfile = await loginResponse.json();
    expect(loginProfile.username).toBe(adminUsername);
    expect(loginProfile.identityProvider).toBe('local');

    await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });
    await page.reload();
    await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });

    const reloadedLocalSession = await page.evaluate(async () => {
      const response = await fetch('/api/auth/local-session');
      return { status: response.status, body: await response.json() };
    });
    expect(reloadedLocalSession.status).toBe(200);
    expect(reloadedLocalSession.body.authenticated).toBe(true);
    expect(reloadedLocalSession.body.username).toBe(adminUsername);
  } finally {
    await context.close();
  }
});
