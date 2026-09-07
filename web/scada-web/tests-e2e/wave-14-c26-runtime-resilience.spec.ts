import { expect, test, type Page } from '@playwright/test';

const runtimeProjection = {
  mode: 'engineering',
  projectKey: 'c26-runtime-resilience',
  projectName: 'C26 Runtime Resilience',
  revision: 26,
  activatedAtUtc: '2026-09-07T17:30:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 16,
    startupScreenId: '26000000-0000-0000-0000-000000000001',
    screens: [
      {
        id: '26000000-0000-0000-0000-000000000001',
        key: 'home',
        name: 'Home',
        elements: [
          {
            id: '26000000-0000-0000-0000-000000000101',
            key: 'open-secondary',
            type: 'core.button',
            properties: { x: 80, y: 80, width: 240, height: 64, text: 'Abrir secundária' },
            actions: [
              { eventKey: 'click', kind: 'NavigateScreen', targetKey: 'secondary', version: 1 }
            ]
          }
        ]
      },
      {
        id: '26000000-0000-0000-0000-000000000002',
        key: 'secondary',
        name: 'Secondary',
        elements: [
          {
            id: '26000000-0000-0000-0000-000000000201',
            key: 'secondary-label',
            type: 'core.text',
            properties: { x: 80, y: 80, width: 320, height: 64, text: 'Tela secundária ativa' }
          }
        ]
      }
    ],
    popups: [],
    dynamos: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: []
  }
} as const;

async function installRuntimeShellContract(page: Page) {
  await page.route('**/api/auth/config', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      localLoginEnabled: true,
      initialAdministratorRequired: false,
      initialAdministratorSetupAvailable: false,
      initialAdministratorBlockedReason: null,
      passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
    }
  }));
  await page.route('**/api/auth/me', route => route.fulfill({
    json: {
      subjectId: 'c26-po-id',
      username: 'c26-po',
      displayName: 'C26 Product Owner',
      roles: ['developer'],
      identityProvider: 'local'
    }
  }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 'c26-po' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
}

test('C26 Runtime preserves the selected Screen across one transient projection transport failure and recovery', async ({ page }) => {
  await installRuntimeShellContract(page);

  let projectionRequests = 0;
  let transportFailures = 0;
  let failNextTransport = false;
  await page.route('**/api/runtime/application', route => {
    projectionRequests++;
    if (failNextTransport) {
      failNextTransport = false;
      transportFailures++;
      return route.abort('failed');
    }
    return route.fulfill({ json: runtimeProjection });
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  const navigator = page.getByTestId('runtime-visual-navigator');
  await expect(runtime).toBeVisible();
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'home');

  await page.getByRole('button', { name: 'Abrir secundária' }).click();
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'secondary');
  await expect(page.getByText('Tela secundária ativa')).toBeVisible();

  failNextTransport = true;
  await expect.poll(() => transportFailures, { timeout: 5_000 }).toBe(1);

  await expect(page.getByTestId('runtime-application-error')).toHaveCount(0);
  await expect(runtime).toBeVisible();
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'secondary');
  await expect(page.getByText('Tela secundária ativa')).toBeVisible();

  const requestsAfterFailure = projectionRequests;
  await expect.poll(() => projectionRequests, { timeout: 5_000 }).toBeGreaterThan(requestsAfterFailure);
  await expect(page.getByTestId('runtime-application-error')).toHaveCount(0);
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'secondary');
});

test('C26 Runtime still blocks when the first Active projection load has no valid transport result', async ({ page }) => {
  await installRuntimeShellContract(page);
  await page.route('**/api/runtime/application', route => route.abort('failed'));

  await page.goto('/');
  const unavailable = page.getByTestId('runtime-application-error');
  await expect(unavailable).toBeVisible();
  await expect(unavailable).toContainText('HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE');
  await expect(page.getByTestId('runtime-engineering-application')).toHaveCount(0);
});

test('C26 Runtime does not retain a stale projection across an authoritative HTTP conflict', async ({ page }) => {
  await installRuntimeShellContract(page);

  let conflict = false;
  let conflictsServed = 0;
  await page.route('**/api/runtime/application', route => {
    if (conflict) {
      conflictsServed++;
      return route.fulfill({
        status: 409,
        json: { error: 'Active Runtime authority changed; retry against the canonical revision.' }
      });
    }
    return route.fulfill({ json: runtimeProjection });
  });

  await page.goto('/');
  const navigator = page.getByTestId('runtime-visual-navigator');
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'home');
  await page.getByRole('button', { name: 'Abrir secundária' }).click();
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'secondary');

  conflict = true;
  await expect.poll(() => conflictsServed, { timeout: 5_000 }).toBeGreaterThan(0);

  const unavailable = page.getByTestId('runtime-application-error');
  await expect(unavailable).toBeVisible();
  await expect(unavailable).toContainText('(409)');
  await expect(page.getByTestId('runtime-visual-navigator')).toHaveCount(0);
});
