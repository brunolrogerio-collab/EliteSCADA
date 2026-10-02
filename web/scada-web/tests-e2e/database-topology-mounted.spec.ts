import { expect, test, type Page, type Route, type TestInfo } from '@playwright/test';

const harnessPath = '/tests-e2e/database-topology-harness.html';
const operationId = '11111111-2222-3333-4444-555555555555';
const secret = 'DB-B-super-secret';

function endpointStatus(overrides: Record<string, unknown> = {}) {
  return {
    host: 'db-primary.example.internal',
    port: 5432,
    database: 'elitescada',
    username: 'elitescada_admin',
    tlsMode: 'VerifyFull',
    credentialConfigured: true,
    trustServerCertificate: false,
    rootCertificateConfigured: true,
    timeoutSeconds: 15,
    ...overrides
  };
}

function profile(mode: 'LocalManaged' | 'Remote' = 'LocalManaged') {
  return mode === 'LocalManaged'
    ? { mode, primary: null, historianUsesPrimary: true, historianOverride: null }
    : { mode, primary: endpointStatus(), historianUsesPrimary: true, historianOverride: null };
}

function health(overrides: Record<string, unknown> = {}) {
  return {
    reachable: true,
    postgreSqlVersion: '18.1',
    postgreSqlMajor: 18,
    timescaleDbVersion: '2.29.2',
    timescaleCapable: true,
    schemaCompatible: true,
    checkedAtUtc: '2026-10-02T18:00:00Z',
    failureCode: null,
    diagnostic: null,
    ...overrides
  };
}

function topologyStatus(overrides: Record<string, unknown> = {}) {
  return {
    activeTopology: profile('LocalManaged'),
    previousTopology: null,
    pendingPhase: null,
    pendingOperationId: null,
    recoveryRequired: false,
    restartRequired: false,
    primaryHealth: health(),
    historianHealth: null,
    lastHealthCheckUtc: '2026-10-02T18:00:00Z',
    lastOperation: null,
    ...overrides
  };
}

function pending(phase: string = 'Prepared') {
  return {
    operationId,
    candidate: {
      mode: 'Remote',
      primary: {
        host: 'db-primary.example.internal',
        port: 5432,
        database: 'elitescada',
        username: 'elitescada_admin',
        credentialReference: 'db-redacted-ref',
        tlsMode: 'VerifyFull',
        rootCertificatePath: '/etc/elitescada/certs/db-root.pem',
        trustServerCertificate: false,
        timeoutSeconds: 15
      },
      historian: { usePrimary: true, override: null }
    },
    phase,
    startedAtUtc: '2026-10-02T18:10:00Z',
    updatedAtUtc: '2026-10-02T18:11:00Z',
    plan: {
      operationId,
      preparedAtUtc: '2026-10-02T18:10:00Z',
      historianUsesPrimary: true,
      timescaleRequired: true,
      durableDomains: [
        'elitescada.engineering_revisions',
        'elitescada.local_users',
        'elitescada.audit_events',
        'elitescada.server_memory_retained_values',
        'elitescada.tag_history'
      ],
      sourceMode: 'LocalManaged',
      targetMode: 'Remote'
    },
    verification: null,
    maintenanceLeaseExpiresAtUtc: null,
    failureCode: null,
    diagnostic: null
  };
}

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body)
  });
}

async function attachScreenshot(page: Page, testInfo: TestInfo, name: string) {
  await testInfo.attach(name, {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png'
  });
}

async function fillRemoteProfile(page: Page) {
  await page.getByLabel('Primary Host / FQDN').fill('db-primary.example.internal');
  await page.getByLabel('Primary Database').fill('elitescada');
  await page.getByLabel('Primary Username / identity').fill('elitescada_admin');
  await page.getByLabel('Primary Password / secret').fill(secret);
}

test('DB-B mounted workflow covers Local, Remote authoring, migration, cutover, restart and rollback', async ({ page }, testInfo) => {
  let currentStatus: any = topologyStatus();
  let statusGetCount = 0;
  let rollbackRequestCount = 0;

  await page.route('**/api/admin/database-topology/**', async route => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    const path = url.pathname;

    if (method === 'GET' && path === '/api/admin/database-topology/') {
      statusGetCount += 1;
      await fulfillJson(route, currentStatus);
      return;
    }

    if (method === 'POST' && path === '/api/admin/database-topology/test') {
      const request = route.request().postDataJSON();
      expect(JSON.stringify(request)).toContain(secret);
      await fulfillJson(route, health());
      return;
    }

    if (method === 'POST' && path === '/api/admin/database-topology/compatibility') {
      await fulfillJson(route, {
        compatible: true,
        primary: health(),
        historian: null,
        failureCode: null,
        diagnostic: null
      });
      return;
    }

    if (method === 'POST' && path === '/api/admin/database-topology/prepare') {
      const request = route.request().postDataJSON();
      expect(request.primary.password).toBe(secret);
      expect(request.primary.trustServerCertificate).toBe(false);
      const prepared = pending('Prepared');
      currentStatus = topologyStatus({
        pendingPhase: 'Prepared',
        pendingOperationId: operationId
      });
      await fulfillJson(route, prepared);
      return;
    }

    if (method === 'POST' && path.endsWith('/copy')) {
      currentStatus = topologyStatus({
        pendingPhase: 'Copied',
        pendingOperationId: operationId
      });
      await fulfillJson(route, pending('Copied'));
      return;
    }

    if (method === 'POST' && path.endsWith('/verify')) {
      currentStatus = topologyStatus({
        pendingPhase: 'Verified',
        pendingOperationId: operationId
      });
      await fulfillJson(route, {
        succeeded: true,
        sourceRows: { 'elitescada.engineering_revisions': 4, 'elitescada.tag_history': 1800 },
        targetRows: { 'elitescada.engineering_revisions': 4, 'elitescada.tag_history': 1800 },
        activeProjectKey: 'eee-demo',
        activeRevision: 12,
        failureCode: null,
        diagnostic: null
      });
      return;
    }

    if (method === 'POST' && path.endsWith('/commit')) {
      currentStatus = topologyStatus({
        activeTopology: profile('Remote'),
        previousTopology: profile('LocalManaged'),
        primaryHealth: health(),
        pendingPhase: null,
        pendingOperationId: null,
        restartRequired: true,
        lastOperation: {
          operationId,
          phase: 'Completed',
          completedAtUtc: '2026-10-02T18:20:00Z',
          failureCode: null,
          diagnostic: null
        }
      });
      await fulfillJson(route, {
        succeeded: true,
        rolledBack: false,
        restartRequired: true,
        status: currentStatus,
        failureCode: null,
        diagnostic: null
      });
      return;
    }

    if (method === 'POST' && path === '/api/admin/database-topology/rollback') {
      rollbackRequestCount += 1;
      const request = route.request().postDataJSON();
      expect(request.operationId).toBeNull();
      currentStatus = topologyStatus({
        activeTopology: profile('LocalManaged'),
        previousTopology: profile('Remote'),
        primaryHealth: health(),
        pendingPhase: null,
        pendingOperationId: null,
        restartRequired: true,
        lastOperation: {
          operationId,
          phase: 'RolledBack',
          completedAtUtc: '2026-10-02T18:30:00Z',
          failureCode: null,
          diagnostic: null
        }
      });
      await fulfillJson(route, {
        succeeded: false,
        rolledBack: true,
        restartRequired: true,
        status: currentStatus,
        failureCode: null,
        diagnostic: null
      });
      return;
    }

    await fulfillJson(route, { error: `Unhandled test route: ${method} ${path}` }, 500);
  });

  await page.goto(harnessPath + '?locale=en');
  await expect(page.getByTestId('database-topology-app')).toBeVisible();
  await expect(page.getByText('Local Managed', { exact: true }).first()).toBeVisible();
  await attachScreenshot(page, testInfo, '01-local-managed-healthy');

  await fillRemoteProfile(page);
  await attachScreenshot(page, testInfo, '02-remote-profile-write-only');

  await page.getByText('Advanced settings', { exact: true }).click();
  await expect(page.getByLabel('Primary CA / root certificate path')).toHaveCount(0);
  await page.getByLabel('Historian uses Primary').uncheck();
  await expect(page.getByLabel('Historian Host / FQDN')).toHaveValue('db-primary.example.internal');
  await expect(page.getByLabel('Historian Database')).toHaveValue('elitescada');
  await expect(page.getByLabel('Historian Username / identity')).toHaveValue('elitescada_admin');
  await expect(page.getByLabel('Historian Password / secret')).toHaveValue('');
  await page.getByLabel('Historian uses Primary').check();
  await page.getByText('Advanced settings', { exact: true }).click();

  await page.getByRole('button', { name: 'Validate target' }).click();
  await expect(page.getByTestId('database-validation-result')).toContainText('Compatible');
  await expect(page.getByTestId('database-validation-result')).toContainText('18.1');
  await expect(page.getByTestId('database-validation-result')).toContainText('2.29.2');
  await attachScreenshot(page, testInfo, '03-target-validation-success');

  await page.getByLabel('Primary Database').fill('elitescada_changed');
  await expect(page.getByTestId('database-validation-result')).toHaveCount(0);
  await page.getByLabel('Primary Database').fill('elitescada');
  await page.getByRole('button', { name: 'Validate target' }).click();
  await expect(page.getByTestId('database-validation-result')).toContainText('Compatible');

  await page.getByRole('button', { name: 'Prepare' }).click();
  await expect(page.getByTestId('database-migration-plan')).toContainText('LocalManaged');
  await expect(page.getByTestId('database-migration-plan')).toContainText('Remote');
  await expect(page.getByRole('status')).toContainText('Credentials configured');
  await expect(page.getByLabel('Primary Password / secret')).toHaveValue('');
  const browserStorage = await page.evaluate(() => ({
    local: JSON.stringify(window.localStorage),
    session: JSON.stringify(window.sessionStorage)
  }));
  expect(browserStorage.local).not.toContain(secret);
  expect(browserStorage.session).not.toContain(secret);
  await attachScreenshot(page, testInfo, '04-prepared-plan');

  currentStatus = topologyStatus({ pendingPhase: 'Quiescing', pendingOperationId: operationId });
  await page.reload();
  await expect(page.getByTestId('database-pending-phase')).toHaveText('Quiescing');
  const refreshBaseline = statusGetCount;
  await expect.poll(() => statusGetCount, { timeout: 12000 }).toBeGreaterThan(refreshBaseline);
  await attachScreenshot(page, testInfo, '05-quiescing-auto-refresh');

  currentStatus = topologyStatus({ pendingPhase: 'Copying', pendingOperationId: operationId });
  await expect(page.getByTestId('database-pending-phase')).toHaveText('Copying', { timeout: 12000 });
  await attachScreenshot(page, testInfo, '06-copying-auto-refresh');

  currentStatus = topologyStatus({ pendingPhase: 'Copied', pendingOperationId: operationId });
  await expect(page.getByTestId('database-pending-phase')).toHaveText('Copied', { timeout: 12000 });
  await page.getByRole('button', { name: 'Verify', exact: true }).click();
  await expect(page.getByTestId('database-pending-phase')).toHaveText('Verified');

  await page.getByRole('button', { name: 'Commit Cutover' }).click();
  await expect(page.getByRole('dialog')).toContainText('previous database will not be deleted');
  await expect(page.getByRole('dialog')).toContainText('Local Managed');
  await attachScreenshot(page, testInfo, '07-cutover-confirmation');
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm' }).click();

  await expect(page.getByText('Remote', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('Restart required', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('Completed', { exact: true }).first()).toBeVisible();
  await expect(page.getByLabel('Primary Host / FQDN')).toHaveValue('');
  await expect(page.getByTestId('database-validation-result')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Return to Local Managed' })).toHaveCount(0);
  await attachScreenshot(page, testInfo, '08-restart-required-completed');

  currentStatus = topologyStatus({
    activeTopology: profile('Remote'),
    previousTopology: profile('LocalManaged'),
    primaryHealth: health(),
    restartRequired: false,
    lastOperation: {
      operationId,
      phase: 'Completed',
      completedAtUtc: '2026-10-02T18:22:00Z',
      failureCode: null,
      diagnostic: null
    }
  });
  await page.reload();
  await expect(page.getByRole('button', { name: 'Return to Local Managed' })).toBeVisible();
  await expect(page.getByText('Operation ID', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Return to Local Managed' }).click();
  await expect(page.getByRole('dialog')).toContainText('No internal identifier is required.');
  await expect(page.getByRole('dialog')).toContainText('The current Remote database will not be deleted');
  await expect(page.getByRole('dialog')).toContainText('Local Managed');
  await attachScreenshot(page, testInfo, '09-return-to-local-confirmation');
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm' }).click();
  await expect(page.getByText('Local Managed · default', { exact: true })).toBeVisible();
  await expect(page.getByText('Restart required', { exact: true }).first()).toBeVisible();
  expect(rollbackRequestCount).toBe(1);
  await attachScreenshot(page, testInfo, '10-return-to-local-completed');

  currentStatus = topologyStatus({
    activeTopology: profile('Remote'),
    previousTopology: profile('LocalManaged'),
    pendingPhase: 'RollbackRequired',
    pendingOperationId: operationId,
    recoveryRequired: true,
    primaryHealth: health({ reachable: false, failureCode: 'connection-failed', diagnostic: 'Database connection failed.' })
  });
  await page.reload();
  await expect(page.getByTestId('database-pending-phase')).toHaveText('Rollback Required');
  await attachScreenshot(page, testInfo, '11-rollback-required');

  await page.getByRole('button', { name: 'Rollback', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('previous topology');
  await attachScreenshot(page, testInfo, '12-rollback-confirmation');
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm' }).click();
  await expect(page.getByText('Rolled Back', { exact: true }).first()).toBeVisible();
  expect(rollbackRequestCount).toBe(2);
  await attachScreenshot(page, testInfo, '13-rollback-result-local-preserved');
});

test('DB-B exposes sanitized auth/TLS failures and incompatible PostgreSQL without fabricating readiness', async ({ page }, testInfo) => {
  let connectionCase: 'auth' | 'tls' | 'ok' = 'auth';
  let compatibilityMode: 'ok' | 'version' = 'version';

  await page.route('**/api/admin/database-topology/**', async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (route.request().method() === 'GET') {
      await fulfillJson(route, topologyStatus());
      return;
    }
    if (path.endsWith('/test')) {
      if (connectionCase === 'auth') {
        await fulfillJson(route, health({
          reachable: false,
          postgreSqlVersion: null,
          postgreSqlMajor: null,
          timescaleDbVersion: null,
          timescaleCapable: false,
          schemaCompatible: false,
          failureCode: 'authentication-failed',
          diagnostic: 'Database authentication failed.'
        }));
      } else if (connectionCase === 'tls') {
        await fulfillJson(route, health({
          reachable: false,
          postgreSqlVersion: null,
          postgreSqlMajor: null,
          timescaleDbVersion: null,
          timescaleCapable: false,
          schemaCompatible: false,
          failureCode: 'tls-validation-failed',
          diagnostic: 'Database TLS/trust validation failed.'
        }));
      } else {
        await fulfillJson(route, health());
      }
      return;
    }
    if (path.endsWith('/compatibility')) {
      const incompatible = health({
        postgreSqlVersion: '16.9',
        postgreSqlMajor: 16,
        timescaleDbVersion: '2.29.2',
        failureCode: 'database-incompatible',
        diagnostic: 'PostgreSQL major 16 is outside the supported range.'
      });
      await fulfillJson(route, compatibilityMode === 'version' ? {
        compatible: false,
        primary: incompatible,
        historian: null,
        failureCode: 'database-incompatible',
        diagnostic: 'One or more database compatibility checks failed.'
      } : {
        compatible: true,
        primary: health(),
        historian: null,
        failureCode: null,
        diagnostic: null
      });
      return;
    }
    await fulfillJson(route, { error: 'Unexpected route.' }, 500);
  });

  await page.goto(harnessPath + '?locale=en');
  await fillRemoteProfile(page);

  await page.getByRole('button', { name: 'Validate target' }).click();
  await expect(page.getByTestId('database-validation-result')).toContainText('Database authentication failed.');
  await attachScreenshot(page, testInfo, '12-auth-failure');

  await page.getByText('Advanced settings', { exact: true }).click();
  await expect(page.getByLabel('Primary CA / root certificate path')).toHaveCount(0);
  await page.getByLabel('Primary TLS mode').selectOption('VerifyFull');
  await expect(page.getByLabel('Primary CA / root certificate path')).toBeVisible();
  await page.getByLabel('Primary CA / root certificate path').fill('/etc/elitescada/certs/db-root.pem');

  connectionCase = 'tls';
  await page.getByRole('button', { name: 'Validate target' }).click();
  await expect(page.getByTestId('database-validation-result')).toContainText('Database TLS/trust validation failed.');
  await attachScreenshot(page, testInfo, '13-tls-failure');

  connectionCase = 'ok';
  await page.getByRole('button', { name: 'Validate target' }).click();
  await expect(page.getByTestId('database-validation-result')).toContainText('16.9');
  await expect(page.getByTestId('database-validation-result')).toContainText('Incompatible');
  await expect(page.getByRole('button', { name: 'Prepare' })).toBeDisabled();
  await attachScreenshot(page, testInfo, '14-postgresql-version-incompatible');
});

test('DB-B renders pt-BR and es from the shared product locale without storing database secrets', async ({ browser }, testInfo) => {
  for (const locale of ['pt-BR', 'es'] as const) {
    const context = await browser.newContext({ locale });
    const page = await context.newPage();
    await page.route('**/api/admin/database-topology/**', route => fulfillJson(route, topologyStatus()));

    await page.goto(`${harnessPath}?locale=${locale}`);
    await expect(page.getByTestId('database-topology-app')).toBeVisible();
    if (locale === 'pt-BR') {
      await expect(page.getByRole('heading', { name: 'Topologia de Banco de Dados' })).toBeVisible();
      await expect(page.getByText('A migração não apaga o Local Managed anterior.', { exact: false })).toBeVisible();
    } else {
      await expect(page.getByRole('heading', { name: 'Topología de Base de Datos' })).toBeVisible();
      await expect(page.getByText('La migración no elimina la base Local Managed anterior.', { exact: false })).toBeVisible();
    }

    await page.getByLabel(locale === 'pt-BR' ? 'Primary Password / secret' : 'Primary Password / secret').fill(secret);
    const stored = await page.evaluate(() => ({
      local: Object.entries(window.localStorage),
      session: Object.entries(window.sessionStorage)
    }));
    expect(JSON.stringify(stored)).not.toContain(secret);
    await attachScreenshot(page, testInfo, `15-locale-${locale}`);
    await context.close();
  }
});
