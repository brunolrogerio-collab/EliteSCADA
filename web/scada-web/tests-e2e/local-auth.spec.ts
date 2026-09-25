import { expect, test } from '@playwright/test';

const adminUsername = 'local-developer';
const adminPassword = 'E2Epass8';

test.setTimeout(90_000);

test('secure first-run creates the initial local Administrator, first project and durable local session', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: { Authorization: '' }
  });
  const page = await context.newPage();

  try {
    // Browser-side fetch resolves relative URLs against the current document, not
    // Playwright's baseURL. Enter the app origin before querying the auth contract.
    await page.goto('/engineering');

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

    // A genuinely fresh installation has no hidden Demo/preconfigured Engineering
    // content. This assertion runs before first-project creation so test fixtures cannot
    // be confused with normal product bootstrap.
    const freshEngineering = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/export/json');
      return { status: response.status, body: await response.json() };
    });
    expect(freshEngineering.status).toBe(200);
    expect(freshEngineering.body.tags).toHaveLength(0);
    expect(freshEngineering.body.alarms).toHaveLength(0);
    expect(freshEngineering.body.dataSources).toHaveLength(0);
    expect(freshEngineering.body.templates).toHaveLength(0);
    expect(freshEngineering.body.equipment).toHaveLength(0);
    expect(freshEngineering.body.screens).toHaveLength(0);
    expect(freshEngineering.body.popups).toHaveLength(0);
    expect(freshEngineering.body.commands).toHaveLength(0);
    expect(freshEngineering.body.gateways).toHaveLength(0);
    expect(freshEngineering.body.scripts).toHaveLength(0);
    expect(freshEngineering.body.visualAssets).toHaveLength(0);
    expect(freshEngineering.body.reports).toHaveLength(0);

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
    expect(securityRoles.body.map((role: { key: string }) => role.key)).toEqual(['developer']);

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
    expect(canonicalProject.body.securityRoles).toHaveLength(0);
    expect(canonicalProject.body.authorityPolicyReference).toBeTruthy();
    expect(canonicalProject.body.authorityPolicyReference.roleIds).toEqual([
      '46000000-0000-0000-0000-000000000002'
    ]);

    // This prerequisite intentionally leaves the first persisted project empty.
    // Any later E2E that needs TAG traffic must create its own test-owned fixture
    // through supported APIs instead of depending on product bootstrap seeding.

    // The clean-install contract is proven above. From this point on, provision an
    // explicit test-owned baseline for downstream Chromium specs. This is test setup,
    // not product bootstrap behavior.
    const authorityPolicy = await page.evaluate(async () => {
      const response = await fetch('/api/auth/authority-policy');
      return { status: response.status, body: await response.json() };
    });
    expect(authorityPolicy.status).toBe(200);
    expect(authorityPolicy.body.roles.map((role: { key: string }) => role.key)).toEqual(['developer']);

    const operatorRole = {
      id: '46000000-0000-0000-0000-000000000001',
      key: 'operator',
      name: 'Operator',
      description: 'E2E operator fixture',
      grants: [
        { capability: 'view' },
        { capability: 'tagRead' },
        { capability: 'commandExecute' },
        { capability: 'alarmAcknowledge' },
        { capability: 'trendUse' }
      ]
    };
    const authorityUpdate = await page.evaluate(async ({ policy, operator }) => {
      const response = await fetch('/api/auth/authority-policy', {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          schema: policy.schema,
          schemaVersion: policy.schemaVersion,
          expectedVersion: policy.version,
          roles: [...policy.roles, operator],
          scopes: policy.scopes
        })
      });
      return { status: response.status, body: await response.json() };
    }, { policy: authorityPolicy.body, operator: operatorRole });
    expect(authorityUpdate.status).toBe(200);
    expect(authorityUpdate.body.roles.map((role: { key: string }) => role.key).sort())
      .toEqual(['developer', 'operator']);

    const fixtureBase = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/export/json');
      return { status: response.status, body: await response.json() };
    });
    expect(fixtureBase.status).toBe(200);
    expect(fixtureBase.body.securityRoles).toHaveLength(0);
    expect(fixtureBase.body.authorityPolicyReference.roleIds).toHaveLength(2);

    const demoFixture = {
      ...fixtureBase.body,
      tags: [
        { id: '10000000-0000-0000-0000-000000000001', name: 'Tank Level', path: 'Demo.Tank01.Level', dataType: 'double', source: 'builtin.simulation', engineeringUnit: '%', readOnly: true },
        { id: '10000000-0000-0000-0000-000000000002', name: 'Pump Running', path: 'Demo.P01.Running', dataType: 'boolean', source: 'builtin.simulation', readOnly: false },
        { id: '10000000-0000-0000-0000-000000000003', name: 'Pump Fault', path: 'Demo.P01.Fault', dataType: 'boolean', source: 'builtin.simulation', readOnly: true },
        { id: '10000000-0000-0000-0000-000000000004', name: 'Pump Current', path: 'Demo.P01.Current', dataType: 'double', source: 'builtin.simulation', engineeringUnit: 'A', readOnly: true },
        { id: '10000000-0000-0000-0000-000000000005', name: 'Pump Frequency', path: 'Demo.P01.Frequency', dataType: 'double', source: 'builtin.simulation', engineeringUnit: 'Hz', readOnly: false },
        { id: '10000000-0000-0000-0000-000000000006', name: 'Discharge Pressure', path: 'Demo.Discharge.Pressure', dataType: 'double', source: 'builtin.simulation', engineeringUnit: 'bar', readOnly: true },
        { id: '10000000-0000-0000-0000-000000000007', name: 'Flow', path: 'Demo.Discharge.Flow', dataType: 'double', source: 'builtin.simulation', engineeringUnit: 'm³/h', readOnly: true }
      ],
      alarms: [
        { id: '20000000-0000-0000-0000-000000000001', name: 'High discharge pressure', tagId: '10000000-0000-0000-0000-000000000006', tagPath: 'Demo.Discharge.Pressure', type: 'high', priority: 'high', setpoint: 9.0, digitalActiveValue: true, area: 'Demo', message: 'Discharge pressure above 9.0 bar', requiresAcknowledgement: true, shelvingAllowed: true, enabled: true },
        { id: '20000000-0000-0000-0000-000000000002', name: 'Pump P01 fault', tagId: '10000000-0000-0000-0000-000000000003', tagPath: 'Demo.P01.Fault', type: 'digital', priority: 'critical', digitalActiveValue: true, area: 'Demo', message: 'Pump P01 fault active', requiresAcknowledgement: true, shelvingAllowed: true, enabled: true }
      ],
      dataSources: [{
        id: '40000000-0000-0000-0000-000000000001',
        key: 'builtin.simulation',
        name: 'Built-in Simulation',
        driver: 'builtin.simulation',
        enabled: true,
        settings: { scanIntervalMilliseconds: '500' },
        metadata: { system: 'true' }
      }],
      templates: [{
        id: '41000000-0000-0000-0000-000000000001',
        key: 'pump.standard',
        name: 'Standard Pump',
        bindings: [
          { key: 'running', kind: 'tag', target: '{equipmentPath}.Running', direction: 'read' },
          { key: 'fault', kind: 'tag', target: '{equipmentPath}.Fault', direction: 'read' },
          { key: 'current', kind: 'tag', target: '{equipmentPath}.Current', direction: 'read' },
          { key: 'frequency', kind: 'tag', target: '{equipmentPath}.Frequency', direction: 'readWrite' }
        ],
        properties: { category: 'pump', defaultFrequencyHz: '60' },
        context: { domain: 'pumping' }
      }],
      equipment: [{
        id: '42000000-0000-0000-0000-000000000001',
        path: 'Demo.P01',
        name: 'Pump P01',
        templateKey: 'pump.standard',
        bindings: [
          { key: 'running', kind: 'tag', target: 'Demo.P01.Running', direction: 'read' },
          { key: 'fault', kind: 'tag', target: 'Demo.P01.Fault', direction: 'read' },
          { key: 'current', kind: 'tag', target: 'Demo.P01.Current', direction: 'read' },
          { key: 'frequency', kind: 'tag', target: 'Demo.P01.Frequency', direction: 'readWrite' }
        ],
        properties: { displayLabel: 'P01' },
        context: { area: 'Demo', process: 'Discharge' }
      }],
      screens: [{
        id: '44000000-0000-0000-0000-000000000001',
        key: 'demo.overview',
        name: 'Demo Overview',
        route: '/demo',
        elements: [
          { key: 'tank01', type: 'tank', bindings: [{ key: 'level', kind: 'tag', target: 'Demo.Tank01.Level', direction: 'read' }], properties: { label: 'Reservatório TK01', x: 100, y: 100 } },
          { key: 'pump01', type: 'dynamo', dynamoKey: 'dynamo.pump.standard', equipmentPath: 'Demo.P01', properties: { x: 430, y: 160 } },
          { key: 'pressure', type: 'value', bindings: [{ key: 'value', kind: 'tag', target: 'Demo.Discharge.Pressure', direction: 'read' }], properties: { label: 'Pressão' } },
          { key: 'flow', type: 'value', bindings: [{ key: 'value', kind: 'tag', target: 'Demo.Discharge.Flow', direction: 'read' }], properties: { label: 'Vazão' } }
        ],
        properties: { canvasWidth: '1366', canvasHeight: '768' },
        context: { area: 'Demo', process: 'Pumping' }
      }],
      popups: [{
        id: '45000000-0000-0000-0000-000000000001',
        key: 'popup.pump.standard',
        name: 'Standard Pump Popup',
        templateKey: 'pump.standard',
        elements: [
          { key: 'current', type: 'value', bindings: [{ key: 'value', kind: 'tag', target: '{equipmentPath}.Current', direction: 'read' }], properties: { label: 'Corrente' } },
          { key: 'frequency', type: 'value', bindings: [{ key: 'value', kind: 'tag', target: '{equipmentPath}.Frequency', direction: 'readWrite' }], properties: { label: 'Frequência' } },
          { key: 'fault', type: 'status', bindings: [{ key: 'active', kind: 'tag', target: '{equipmentPath}.Fault', direction: 'read' }], properties: { label: 'Falha' } }
        ],
        properties: { width: '640', height: '420' },
        context: { role: 'equipment-details' }
      }],
      securityRoles: [],
      commands: [
        { id: '30000000-0000-0000-0000-000000000001', key: 'demo.p01.start', name: 'Start Pump P01', kind: 'writeTagValue', value: 'True', targetTagId: '10000000-0000-0000-0000-000000000002', targetTagPath: 'Demo.P01.Running', description: 'Starts the demo pump through the operational command domain.', area: 'Demo', equipmentPath: 'Demo.P01', enabled: true },
        { id: '30000000-0000-0000-0000-000000000002', key: 'demo.p01.stop', name: 'Stop Pump P01', kind: 'writeTagValue', value: 'False', targetTagId: '10000000-0000-0000-0000-000000000002', targetTagPath: 'Demo.P01.Running', description: 'Stops the demo pump through the operational command domain.', area: 'Demo', equipmentPath: 'Demo.P01', enabled: true }
      ],
      startupScreenId: '44000000-0000-0000-0000-000000000001'
    };

    const fixtureApply = await page.evaluate(async fixture => {
      const response = await fetch('/api/engineering/import/json/apply', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(fixture)
      });
      return { status: response.status, body: await response.json() };
    }, demoFixture);
    expect(fixtureApply.status).toBe(200);

    const fixtureSave = await page.evaluate(async currentProjectKey => {
      const response = await fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/save`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ projectName: 'E2E Explicit Demo Fixture', savedBy: 'local-auth-e2e' })
      });
      return { status: response.status, body: await response.json() };
    }, projectKey);
    expect(fixtureSave.status).toBe(200);

    const populatedWorkspace = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/workspace');
      return { status: response.status, body: await response.json() };
    });
    expect(populatedWorkspace.status).toBe(200);
    expect(populatedWorkspace.body.tagCount).toBe(7);
    expect(populatedWorkspace.body.securityRoleCount).toBe(1);
    expect(populatedWorkspace.body.isDirty).toBe(false);

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
