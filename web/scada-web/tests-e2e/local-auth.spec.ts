import { expect, test } from '@playwright/test';
import { admitInteractiveRuntimeSession, terminateRuntimeSession } from './runtimeSessionLease';

const adminUsername = 'local-developer';
const adminPassword = 'E2Epass8';

test.setTimeout(90_000);

test('secure first-run creates the initial local Administrator, first project and durable local session', async ({ browser, baseURL, request }) => {
  const apiBaseUrl = process.env.ELITESCADA_E2E_API_BASE_URL ?? 'http://127.0.0.1:5080';
  const context = await browser.newContext({
    baseURL: baseURL ?? 'http://127.0.0.1:5173',
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

    const cookies = await context.cookies(baseURL ?? 'http://127.0.0.1:5173');
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

    const initialRuntimeProjection = await page.evaluate(async () => {
      const response = await fetch('/api/runtime/application');
      return { status: response.status, body: await response.json() };
    });
    expect(initialRuntimeProjection.status).toBe(200);
    expect(initialRuntimeProjection.body.mode).toBe('neutral');
    expect(initialRuntimeProjection.body.projectKey).toBeNull();
    expect(initialRuntimeProjection.body.revision).toBeNull();
    expect(initialRuntimeProjection.body.package).toBeNull();

    // A genuinely fresh installation is still empty before first-project creation.
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
    expect(freshEngineering.body.dynamos).toHaveLength(0);
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
    expect(workspace.body.visualAssetCount).toBe(26);
    expect(workspace.body.dynamoCount).toBe(26);
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
    const canonicalProjectResponse = await page.request.get(`${apiBaseUrl}/api/engineering/export/json`);
    expect(canonicalProjectResponse.status()).toBe(200);
    const canonicalProject = await canonicalProjectResponse.json();
    expect(canonicalProject.tags).toHaveLength(0);
    expect(canonicalProject.alarms).toHaveLength(0);
    expect(canonicalProject.dataSources).toHaveLength(0);
    expect(canonicalProject.templates).toHaveLength(0);
    expect(canonicalProject.equipment).toHaveLength(0);
    expect(canonicalProject.screens).toHaveLength(0);
    expect(canonicalProject.popups).toHaveLength(0);
    expect(canonicalProject.commands).toHaveLength(0);
    expect(canonicalProject.gateways).toHaveLength(0);
    expect(canonicalProject.scripts).toHaveLength(0);
    expect(canonicalProject.scriptVisualEventReferences).toHaveLength(0);
    expect(canonicalProject.visualAssets).toHaveLength(26);
    expect(canonicalProject.reports).toHaveLength(0);
    expect(canonicalProject.dynamos).toHaveLength(26);
    expect(canonicalProject.dynamos.every((dynamo: { metadata?: Record<string, string> }) =>
      dynamo.metadata?.catalogGeneration === '1' && dynamo.metadata.catalogStatus === 'active')).toBeTruthy();
    expect(canonicalProject.securityRoles).toHaveLength(0);
    expect(canonicalProject.authorityPolicyReference).toBeTruthy();
    expect(canonicalProject.authorityPolicyReference.roleIds).toEqual([
      '46000000-0000-0000-0000-000000000002'
    ]);

    // A saved Working revision is not Runtime Active. Before the explicit E2E fixture is
    // published/activated, the server must remain neutral and expose no Runtime TAGs.
    const firstProjectRuntime = await page.evaluate(async currentProjectKey => {
      const response = await fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/runtime`);
      return { status: response.status, body: await response.json() };
    }, projectKey);
    expect(firstProjectRuntime.status).toBe(200);
    expect(firstProjectRuntime.body.durable.activeRevision).toBeNull();
    expect(firstProjectRuntime.body.live.mode).toBe('neutral');
    expect(firstProjectRuntime.body.live.revision).toBeNull();

    const firstProjectProjection = await page.evaluate(async () => {
      const response = await fetch('/api/runtime/application');
      return { status: response.status, body: await response.json() };
    });
    expect(firstProjectProjection.status).toBe(200);
    expect(firstProjectProjection.body.mode).toBe('neutral');
    expect(firstProjectProjection.body.projectKey).toBeNull();
    expect(firstProjectProjection.body.revision).toBeNull();
    expect(firstProjectProjection.body.package).toBeNull();

    const firstProjectRuntimeTags = await page.evaluate(async () => {
      const response = await fetch('/api/tags');
      return { status: response.status, body: await response.json() };
    });
    expect(firstProjectRuntimeTags.status).toBe(200);
    expect(firstProjectRuntimeTags.body).toHaveLength(0);

    await page.goto('/');
    await expect(page.getByTestId('runtime-neutral')).toBeVisible();
    await expect(page.getByTestId('runtime-simulation-fallback')).toHaveCount(0);
    await expect(page.getByText('Demo · Estação Elevatória')).toHaveCount(0);
    await page.reload();
    await expect(page.getByTestId('runtime-neutral')).toBeVisible();
    const resumedProjection = await page.evaluate(async () => {
      const response = await fetch('/api/runtime/application');
      return { status: response.status, body: await response.json() };
    });
    expect(resumedProjection.body.mode).toBe('neutral');
    expect(resumedProjection.body.projectKey).toBeNull();
    expect(resumedProjection.body.revision).toBeNull();

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
          roles: [
            ...policy.roles.map((role: { key: string; grants: Array<{ capability: string }> }) => role.key === 'developer'
              ? {
                ...role,
                grants: [
                  ...role.grants.filter(grant => !['engineeringModify', 'userRoleAdmin'].includes(grant.capability)),
                  { capability: 'engineeringModify' },
                  { capability: 'userRoleAdmin' }
                ]
              }
              : role),
            operator
          ],
          scopes: policy.scopes
        })
      });
      return { status: response.status, body: await response.json() };
    }, { policy: authorityPolicy.body, operator: operatorRole });
    expect(authorityUpdate.status).toBe(200);
    expect(authorityUpdate.body.roles.map((role: { key: string }) => role.key).sort())
      .toEqual(['developer', 'operator']);

    const fixtureBaseResponse = await page.request.get(`${apiBaseUrl}/api/engineering/export/json`);
    expect(fixtureBaseResponse.status()).toBe(200);
    const fixtureBase = await fixtureBaseResponse.json();
    expect(fixtureBase.securityRoles).toHaveLength(0);
    expect(fixtureBase.authorityPolicyReference.roleIds).toHaveLength(2);

    const demoFixture = {
      ...fixtureBase,
      tags: [
        { id: '10000000-0000-0000-0000-000000000001', name: 'Tank Level', path: 'Demo.Tank01.Level', dataType: 'double', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', engineeringUnit: '%', readOnly: true, initialValue: { dataType: 'double', value: 62.5 } },
        { id: '10000000-0000-0000-0000-000000000002', name: 'Pump Running', path: 'Demo.P01.Running', dataType: 'boolean', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', readOnly: false, initialValue: { dataType: 'boolean', value: true } },
        { id: '10000000-0000-0000-0000-000000000003', name: 'Pump Fault', path: 'Demo.P01.Fault', dataType: 'boolean', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', readOnly: true, initialValue: { dataType: 'boolean', value: false } },
        { id: '10000000-0000-0000-0000-000000000004', name: 'Pump Current', path: 'Demo.P01.Current', dataType: 'double', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', engineeringUnit: 'A', readOnly: true, initialValue: { dataType: 'double', value: 12.5 }, historian: { enabled: true, strategy: 'onChange', deadband: 0.01, periodMilliseconds: null, maximumPeriodMilliseconds: 5000 } },
        { id: '10000000-0000-0000-0000-000000000005', name: 'Pump Frequency', path: 'Demo.P01.Frequency', dataType: 'double', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', engineeringUnit: 'Hz', readOnly: false, initialValue: { dataType: 'double', value: 60 }, historian: { enabled: true, strategy: 'onChange', deadband: 0.01, periodMilliseconds: null, maximumPeriodMilliseconds: 5000 } },
        { id: '10000000-0000-0000-0000-000000000006', name: 'Discharge Pressure', path: 'Demo.Discharge.Pressure', dataType: 'double', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', engineeringUnit: 'bar', readOnly: true, initialValue: { dataType: 'double', value: 10.5 } },
        { id: '10000000-0000-0000-0000-000000000007', name: 'Flow', path: 'Demo.Discharge.Flow', dataType: 'double', source: 'memory.server.e2e', dataSourceId: '40000000-0000-0000-0000-000000000001', engineeringUnit: 'm³/h', readOnly: true, initialValue: { dataType: 'double', value: 75 } }
      ],
      alarms: [
        { id: '20000000-0000-0000-0000-000000000001', name: 'High discharge pressure', tagId: '10000000-0000-0000-0000-000000000006', tagPath: 'Demo.Discharge.Pressure', type: 'high', priority: 'high', setpoint: 9.0, digitalActiveValue: true, area: 'Demo', message: 'Discharge pressure above 9.0 bar', requiresAcknowledgement: true, shelvingAllowed: true, enabled: true },
        { id: '20000000-0000-0000-0000-000000000002', name: 'Pump P01 fault', tagId: '10000000-0000-0000-0000-000000000003', tagPath: 'Demo.P01.Fault', type: 'digital', priority: 'critical', digitalActiveValue: true, area: 'Demo', message: 'Pump P01 fault active', requiresAcknowledgement: true, shelvingAllowed: true, enabled: true }
      ],
      dataSources: [{
        id: '40000000-0000-0000-0000-000000000001',
        key: 'memory.server.e2e',
        name: 'E2E Server Memory',
        driver: 'builtin.memory.server',
        enabled: true,
        settings: {},
        metadata: { fixture: 'runtime-e2e' }
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
      commands: [
        { id: '30000000-0000-0000-0000-000000000001', key: 'demo.p01.start', name: 'Start Pump P01', kind: 'writeTagValue', value: 'True', targetTagId: '10000000-0000-0000-0000-000000000002', targetTagPath: 'Demo.P01.Running', description: 'Starts the demo pump through the operational command domain.', area: 'Demo', equipmentPath: 'Demo.P01', enabled: true },
        { id: '30000000-0000-0000-0000-000000000002', key: 'demo.p01.stop', name: 'Stop Pump P01', kind: 'writeTagValue', value: 'False', targetTagId: '10000000-0000-0000-0000-000000000002', targetTagPath: 'Demo.P01.Running', description: 'Stops the demo pump through the operational command domain.', area: 'Demo', equipmentPath: 'Demo.P01', enabled: true }
      ],
      startupScreenId: '44000000-0000-0000-0000-000000000001'
    };

    const fixtureApply = await page.request.post(`${apiBaseUrl}/api/engineering/import/json/apply`, {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: demoFixture
    });
    expect(fixtureApply.status()).toBe(200);

    const fixtureSave = await page.evaluate(async currentProjectKey => {
      const response = await fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/save`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ projectName: 'E2E Explicit Demo Fixture', savedBy: 'local-auth-e2e' })
      });
      return { status: response.status, body: await response.json() };
    }, projectKey);
    expect(fixtureSave.status).toBe(200);
    expect(Number.isInteger(fixtureSave.body.revision)).toBeTruthy();
    const fixtureRevision = fixtureSave.body.revision as number;

    // Downstream Runtime specs must exercise this explicit test-owned Engineering revision,
    // never the legacy DemoRuntimeServices fallback. Preserve Working != Published != Active.
    const fixturePublish = await page.evaluate(async ({ currentProjectKey, revision }) => {
      const response = await fetch(
        `/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/revisions/${revision}/publish`,
        {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ publishedBy: 'local-auth-e2e' })
        });
      return { status: response.status, body: await response.json() };
    }, { currentProjectKey: projectKey, revision: fixtureRevision });
    expect(fixturePublish.status).toBe(200);
    expect(fixturePublish.body.lifecycle.publishedRevision).toBe(fixtureRevision);

    const fixtureActivate = await page.evaluate(async currentProjectKey => {
      const response = await fetch(
        `/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/published/activate`,
        {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ activatedBy: 'local-auth-e2e' })
        });
      const text = await response.text();
      let body: any = null;
      if (text) {
        try { body = JSON.parse(text); }
        catch { body = { raw: text }; }
      }
      return { status: response.status, body };
    }, projectKey);
    expect(fixtureActivate.status).toBe(200);
    expect(fixtureActivate.body?.activated).toBe(true);

    const activeFixtureRuntime = await page.evaluate(async currentProjectKey => {
      const response = await fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/runtime`);
      return { status: response.status, body: await response.json() };
    }, projectKey);
    expect(activeFixtureRuntime.status).toBe(200);
    expect(activeFixtureRuntime.body.consistent).toBe(true);
    expect(activeFixtureRuntime.body.durable.activeRevision).toBe(fixtureRevision);
    expect(activeFixtureRuntime.body.live.mode).toBe('engineering');
    expect(activeFixtureRuntime.body.live.projectKey).toBe(projectKey);
    expect(activeFixtureRuntime.body.live.revision).toBe(fixtureRevision);

    // Candidate initial Server Memory values are composed before the activation
    // EventGate commits and therefore are intentionally not Historian evidence.
    // Seed one deterministic sample only after activation through the normal
    // authenticated Runtime Session + TAG write path used by real clients.
    const historianTagId = '10000000-0000-0000-0000-000000000005';
    const leaseHeaders = await admitInteractiveRuntimeSession(request);
    try {
      const writeResponse = await request.post(`/api/tags/${historianTagId}/write`, {
        data: { value: 59.5 },
        headers: leaseHeaders
      });
      expect(writeResponse.status()).toBe(202);

      await expect.poll(async () => {
        const response = await request.get(`/api/history/${historianTagId}?limit=5`);
        if (!response.ok()) return 0;
        const history = await response.json() as unknown[];
        return history.length;
      }, { timeout: 12_000 }).toBeGreaterThan(0);
    } finally {
      await terminateRuntimeSession(request, leaseHeaders);
    }

    const populatedWorkspace = await page.evaluate(async () => {
      const response = await fetch('/api/engineering/workspace');
      return { status: response.status, body: await response.json() };
    });
    expect(populatedWorkspace.status).toBe(200);
    expect(populatedWorkspace.body.tagCount).toBe(7);
    expect(populatedWorkspace.body.securityRoleCount).toBe(1);
    expect(populatedWorkspace.body.isDirty).toBe(false);

    // W15-INSTALLATION-UX mounted journey: preserve A, detach to true neutral,
    // attach B through normal bootstrap/lifecycle, detach B, then restore A.
    const authorityBackupPassword = 'Authority-A-backup-2026';
    const applicationABackup = await page.evaluate(async ({ currentProjectKey, currentProjectName }) => {
      const query = new URLSearchParams({ projectKey: currentProjectKey, projectName: currentProjectName });
      const response = await fetch(`/api/project-package/export?${query.toString()}`);
      return { status: response.status, bytes: Array.from(new Uint8Array(await response.arrayBuffer())) };
    }, { currentProjectKey: projectKey, currentProjectName: 'E2E Explicit Demo Fixture' });
    expect(applicationABackup.status).toBe(200);
    expect(applicationABackup.bytes.length).toBeGreaterThan(100);

    const authorityABackup = await page.evaluate(async password => {
      const response = await fetch('/api/auth/authority-backup/export', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ password })
      });
      return { status: response.status, body: await response.json() };
    }, authorityBackupPassword);
    expect(authorityABackup.status).toBe(200);
    expect(authorityABackup.body.backup).toContain('elitescada.authority-backup');

    const licenseBeforeSwitch = await page.evaluate(async () => {
      const response = await fetch('/api/licensing/status');
      return { status: response.status, body: await response.json() };
    });
    expect(licenseBeforeSwitch.status).toBe(200);

    const detachCurrentApplication = async () => {
      await page.goto('/engineering/installation');
      await expect(page.getByTestId('installation-switching')).toBeVisible({ timeout: 15_000 });
      await expect(page.getByTestId('installation-current-state')).toBeVisible();
      await page.getByRole('button', { name: 'Detach application from this installation' }).click();

      await page.getByLabel(/Continue without exporting the Application now/).check();
      await page.getByLabel(/Continue without exporting the Authority now/).check();
      await page.getByLabel(/I understand this Application will stop being the one attached/).check();
      await page.getByLabel(/I understand the current Authority will be detached/).check();
      await page.getByLabel(/I understand Historian\/database data is preserved/).check();

      const confirm = page.getByTestId('installation-detach-confirm');
      await expect(confirm).toBeEnabled();
      await Promise.all([
        page.waitForURL(/\/$/),
        confirm.click()
      ]);
    };

    await detachCurrentApplication();

    expect(await page.evaluate(async () => (await fetch('/api/auth/me')).status)).toBe(401);

    // Reload proves that neutral is server/store-owned rather than React/session state.
    await page.reload();
    await expect(page.locator('input[name="bootstrap-username"]')).toBeVisible();

    const adminBUsername = 'local-b-admin';
    const adminBPassword = 'E2EBpass8';
    await page.locator('input[name="bootstrap-username"]').fill(adminBUsername);
    await page.locator('input[name="bootstrap-display-name"]').fill('Local B Administrator');
    await page.locator('input[name="bootstrap-password"]').fill(adminBPassword);
    await page.locator('input[name="bootstrap-password-confirmation"]').fill(adminBPassword);
    await page.locator('button[type="submit"]').click();

    await expect(page.getByRole('heading', { name: 'Create New Project' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('button', { name: 'Import application' })).toBeVisible();

    const neutralAfterA = await page.evaluate(async () => {
      const response = await fetch('/api/runtime/application');
      return { status: response.status, body: await response.json() };
    });
    expect(neutralAfterA.status).toBe(200);
    expect(neutralAfterA.body.mode).toBe('neutral');
    expect(neutralAfterA.body.projectKey).toBeNull();

    const licenseAfterA = await page.evaluate(async () => {
      const response = await fetch('/api/licensing/status');
      return { status: response.status, body: await response.json() };
    });
    expect(licenseAfterA.status).toBe(200);
    expect(licenseAfterA.body.license.state).toBe(licenseBeforeSwitch.body.license.state);

    const projectBKey = 'e2e-plant-b';
    await page.locator('input[name="project-key"]').fill(projectBKey);
    await page.locator('input[name="project-name"]').fill('E2E Plant B');
    await page.locator('button[type="submit"]').click();
    await expect(page.locator('.eng-shell')).toBeVisible({ timeout: 15_000 });

    const bWorkspaceResponse = await page.request.get(`${apiBaseUrl}/api/engineering/workspace`);
    expect(bWorkspaceResponse.status()).toBe(200);
    const bWorkspace = await bWorkspaceResponse.json();
    const engineeringResponse = await page.request.get(`${apiBaseUrl}/api/engineering/export/json`);
    expect(engineeringResponse.status()).toBe(200);
    const engineering = await engineeringResponse.json();

    // A fresh First Project is intentionally neutral Engineering content. Give B
    // one canonical self-contained Server Memory source/TAG through the same
    // Preview/Apply contract used by product CI so Publish -> Activate exercises
    // a real Runtime instead of relying on any hidden Demo fallback. Keep this
    // large package transfer off the development-server proxy on Windows.
    const dataSourceId = '96000000-0000-0000-0000-000000000001';
    const tagId = '96000000-0000-0000-0000-000000000002';
    const activatableEngineering = {
      ...engineering,
      exportedAt: new Date().toISOString(),
      dataSources: [{
        id: dataSourceId,
        key: 'installation.b.memory.server',
        name: 'Installation B Server Memory',
        driver: 'builtin.memory.server',
        enabled: true,
        metadata: { owner: 'w15-installation-e2e' }
      }],
      tags: [{
        id: tagId,
        name: 'Runtime Value',
        path: 'Installation.B.RuntimeValue',
        dataType: 'double',
        source: 'installation.b.memory.server',
        address: null,
        engineeringUnit: '%',
        description: 'Self-contained Runtime value for Installation B switching acceptance',
        readOnly: false,
        scaleMinimum: 0,
        scaleMaximum: 100,
        historian: {
          enabled: false,
          strategy: 'change',
          deadband: null,
          periodMilliseconds: null,
          maximumPeriodMilliseconds: null
        },
        metadata: { owner: 'w15-installation-e2e' },
        initialValue: { dataType: 'double', value: 42.5 },
        dataSourceId
      }],
      alarms: [],
      commands: [],
      gateways: []
    };

    const preview = await page.request.post(`${apiBaseUrl}/api/engineering/import/json/preview`, {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: activatableEngineering
    });
    const previewBody = await preview.json();
    const apply = await page.request.post(`${apiBaseUrl}/api/engineering/import/json/apply`, {
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'x-elitescada-workspace-version': String(bWorkspace.changeVersion)
      },
      data: activatableEngineering
    });
    const applyBody = await apply.json();
    const save = await page.request.post(`${apiBaseUrl}/api/engineering/persistence/${encodeURIComponent(projectBKey)}/save`, {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: { projectName: 'E2E Plant B' }
    });
    const saveBody = await save.json();
    const revision = saveBody.revision as number;
    const publish = await page.request.post(`${apiBaseUrl}/api/engineering/persistence/${encodeURIComponent(projectBKey)}/revisions/${revision}/publish`, {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: { publishedBy: 'installation-e2e-b' }
    });
    const publishBody = await publish.json();
    const activate = await page.request.post(`${apiBaseUrl}/api/engineering/persistence/${encodeURIComponent(projectBKey)}/published/activate`, {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: { activatedBy: 'installation-e2e-b' }
    });
    const activateBody = await activate.json();
    const bLifecycle = {
      previewStatus: preview.status(),
      previewBody,
      applyStatus: apply.status(),
      applyBody,
      saveStatus: save.status(),
      saveBody,
      revision,
      publishStatus: publish.status(),
      publishBody,
      activateStatus: activate.status(),
      activateBody
    };
    expect(bLifecycle.previewStatus).toBe(200);
    expect(bLifecycle.previewBody.canApply).toBe(true);
    expect(bLifecycle.previewBody.errorCount ?? 0).toBe(0);
    expect(bLifecycle.applyStatus).toBe(200);
    expect((bLifecycle.applyBody.issues ?? []).some((issue: { isError?: boolean }) => issue.isError)).toBe(false);
    expect(bLifecycle.saveStatus).toBe(200);
    expect(bLifecycle.revision).toBeGreaterThan(0);
    expect(bLifecycle.publishStatus).toBe(200);
    expect(bLifecycle.activateStatus).toBe(200);
    expect(bLifecycle.activateBody.activated).toBe(true);

    const runtimeB = await page.evaluate(async () => {
      const response = await fetch('/api/runtime/application');
      return { status: response.status, body: await response.json() };
    });
    expect(runtimeB.status).toBe(200);
    expect(runtimeB.body.mode).toBe('engineering');
    expect(runtimeB.body.projectKey).toBe(projectBKey);
    expect(runtimeB.body.revision).toBe(bLifecycle.revision);

    const usersInB = await page.evaluate(async () => {
      const response = await fetch('/api/auth/users');
      return { status: response.status, body: await response.json() };
    });
    expect(usersInB.status).toBe(200);
    expect(usersInB.body.map((user: { username: string }) => user.username)).toContain(adminBUsername);
    expect(usersInB.body.map((user: { username: string }) => user.username)).not.toContain(adminUsername);

    await detachCurrentApplication();
    expect(await page.evaluate(async () => (await fetch('/api/auth/me')).status)).toBe(401);
    await expect(page.locator('input[name="bootstrap-username"]')).toBeVisible({ timeout: 15_000 });

    // Restore A through the full neutral Restore path: Authority and Application remain
    // two separate artifacts, and the restored Authority must authenticate before Apply.
    await page.getByRole('button', { name: 'Restore backup' }).click();
    await page.getByTestId('recovery-application-file').setInputFiles({
      name: 'application-a.escadapkg',
      mimeType: 'application/vnd.elitescada.project-package',
      buffer: Buffer.from(applicationABackup.bytes)
    });
    await page.getByTestId('recovery-authority-file').setInputFiles({
      name: 'authority-a.json',
      mimeType: 'application/json',
      buffer: Buffer.from(authorityABackup.body.backup)
    });
    await page.getByTestId('recovery-authority-password').fill(authorityBackupPassword);
    await page.getByRole('button', { name: 'Validate backups' }).click();
    await page.getByRole('button', { name: 'Restore Authority' }).click();

    await expect(page.locator('input[name="username"]')).toBeVisible({ timeout: 15_000 });
    const staleBLogin = await page.evaluate(async ({ username, password }) => {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ username, password })
      });
      return response.status;
    }, { username: adminBUsername, password: adminBPassword });
    expect(staleBLogin).toBe(401);

    await page.locator('input[name="username"]').fill(adminUsername);
    await page.locator('input[name="password"]').fill(adminPassword);
    await page.locator('button[type="submit"]').click();

    await expect(page.getByTestId('restore-first-application')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: 'Continue recovery' })).toBeVisible();
    await expect(page.getByTestId('recovery-continuation-summary')).toContainText('application-a.escadapkg');
    await expect(page.getByTestId('recovery-application-file')).toHaveCount(0);
    await expect(page.getByTestId('recovery-license-file')).toHaveCount(0);
    await expect(page.getByTestId('recovery-authority-password')).toHaveCount(0);
    await page.getByRole('button', { name: 'Continue recovery' }).click();
    await page.getByRole('button', { name: 'Import selected application' }).click();
    await expect(page.getByTestId('runtime-engineering-application')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId('runtime-engineering-canvas')).toBeVisible();

    const restoredA = await page.evaluate(async currentProjectKey => {
      const [workspaceResponse, runtimeResponse, usersResponse] = await Promise.all([
        fetch('/api/engineering/workspace'),
        fetch(`/api/engineering/persistence/${encodeURIComponent(currentProjectKey)}/runtime`),
        fetch('/api/auth/users')
      ]);
      return {
        workspace: await workspaceResponse.json(),
        runtime: await runtimeResponse.json(),
        users: await usersResponse.json()
      };
    }, projectKey);
    expect(restoredA.workspace.projectKey).toBe(projectKey);
    expect(restoredA.runtime.consistent).toBe(true);
    expect(restoredA.runtime.live.projectKey).toBe(projectKey);
    expect(restoredA.users.map((user: { username: string }) => user.username)).toContain(adminUsername);
    expect(restoredA.users.map((user: { username: string }) => user.username)).not.toContain(adminBUsername);

    const historianAfterRestore = await page.evaluate(async tagId => {
      const response = await fetch(`/api/history/${tagId}?limit=5`);
      return { status: response.status, body: response.ok ? await response.json() : [] };
    }, historianTagId);
    expect(historianAfterRestore.status).toBe(200);
    expect(historianAfterRestore.body.length).toBeGreaterThan(0);

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

    await expect(page.getByTestId('session-menu-toggle').getByText('@local-developer')).toBeVisible({ timeout: 15_000 });
    await page.reload();
    await expect(page.getByTestId('session-menu-toggle').getByText('@local-developer')).toBeVisible({ timeout: 15_000 });

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


test('auth system chrome follows semantic Light and Dark themes including password and file inputs', async ({ page }) => {
  const config = {
    authenticationEnabled: true,
    localLoginEnabled: true,
    initialAdministratorRequired: true,
    initialAdministratorSetupAvailable: true,
    initialAdministratorBlockedReason: null,
    passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
  };
  await page.route('**/api/auth/config', route => route.fulfill({ json: config }));
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401 }));

  await page.addInitScript(() => {
    if (!localStorage.getItem('elitescada.app.theme'))
      localStorage.setItem('elitescada.app.theme', 'light');
  });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');

  const light = await page.locator('.auth-card').evaluate(element => ({
    card: getComputedStyle(element).backgroundColor,
    password: getComputedStyle(document.querySelector('input[name="bootstrap-password"]')!).backgroundColor
  }));
  await page.getByRole('button', { name: /Restore backup|Restaurar backup|Restaurar backup/i }).click();
  const lightFile = await page.getByTestId('recovery-application-file')
    .evaluate(element => getComputedStyle(element).backgroundColor);

  await page.evaluate(() => localStorage.setItem('elitescada.app.theme', 'dark'));
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'dark');

  const dark = await page.locator('.auth-card').evaluate(element => ({
    card: getComputedStyle(element).backgroundColor,
    password: getComputedStyle(document.querySelector('input[name="bootstrap-password"]')!).backgroundColor
  }));
  await page.getByRole('button', { name: /Restore backup|Restaurar backup|Restaurar backup/i }).click();
  const darkFile = await page.getByTestId('recovery-application-file')
    .evaluate(element => getComputedStyle(element).backgroundColor);

  expect(light.card).not.toBe(dark.card);
  expect(light.password).not.toBe(dark.password);
  expect(lightFile).not.toBe(darkFile);
});
