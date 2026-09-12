import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

type SessionProfile = {
  subjectId: string;
  username: string;
  displayName?: string;
  roles: string[];
  identityProvider: string;
};

type LogoutMode = 'success' | 'failure';

const administrator: SessionProfile = {
  subjectId: 'local-admin-id',
  username: 'local-admin',
  displayName: 'Administrador Local',
  roles: ['developer'],
  identityProvider: 'local'
};

const runtimeOperator: SessionProfile = {
  subjectId: 'runtime-operator-id',
  username: 'runtime-operator',
  displayName: '   ',
  roles: ['runtime-only'],
  identityProvider: 'local'
};

const externalDeveloper: SessionProfile = {
  subjectId: 'external-developer-id',
  username: 'external-developer',
  displayName: 'External Developer',
  roles: ['developer'],
  identityProvider: 'jwt'
};

const authConfiguration = {
  authenticationEnabled: true,
  localLoginEnabled: true,
  initialAdministratorRequired: false,
  initialAdministratorSetupAvailable: false,
  initialAdministratorBlockedReason: null,
  passwordPolicy: {
    minimumLength: 8,
    maximumLength: 1024
  }
};

async function installSessionContract(
  page: Page,
  initialProfile: SessionProfile,
  initialLogoutMode: LogoutMode = 'success'
) {
  let currentProfile: SessionProfile | null = initialProfile;
  let logoutMode = initialLogoutMode;
  let logoutRequests = 0;
  let loginRequests = 0;
  let runtimeCapabilityRequests = 0;

  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfiguration }));
  await page.route('**/api/auth/me', route => {
    if (!currentProfile) return route.fulfill({ status: 401, body: '' });
    return route.fulfill({ json: currentProfile });
  });
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: {
      authenticated: currentProfile?.identityProvider === 'local',
      username: currentProfile?.identityProvider === 'local' ? currentProfile.username : null
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.route('**/api/auth/effective-capabilities', route => {
    const runtimeOnly = currentProfile?.username === runtimeOperator.username;
    if (runtimeOnly) runtimeCapabilityRequests++;
    return route.fulfill({
      json: runtimeOnly
        ? {
            authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
            authenticationEnabled: true,
            runtime: ['View'],
            workspace: []
          }
        : {
            authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
            authenticationEnabled: true,
            runtime: ['View', 'TrendUse', 'SystemAdmin'],
            workspace: ['EngineeringView', 'EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
          }
    });
  });
  await page.route('**/api/auth/logout', route => {
    logoutRequests++;
    if (logoutMode === 'failure') {
      return route.fulfill({ status: 503, json: { error: 'session invalidation unavailable' } });
    }

    currentProfile = null;
    return route.fulfill({ status: 204, body: '' });
  });
  await page.route('**/api/auth/login', async route => {
    loginRequests++;
    const request = route.request().postDataJSON() as { username?: string; password?: string };
    if (request.username !== runtimeOperator.username || request.password !== 'RuntimePass8') {
      return route.fulfill({ status: 401, body: '' });
    }

    currentProfile = runtimeOperator;
    return route.fulfill({ json: runtimeOperator });
  });

  return {
    setLogoutMode(next: LogoutMode) {
      logoutMode = next;
    },
    get logoutRequests() {
      return logoutRequests;
    },
    get loginRequests() {
      return loginRequests;
    },
    get runtimeCapabilityRequests() {
      return runtimeCapabilityRequests;
    }
  };
}

async function installEngineeringRuntimeProjection(page: Page) {
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: {
      mode: 'engineering',
      projectKey: 'c25-session-runtime',
      projectName: 'C25 Session Runtime',
      revision: 7,
      activatedAtUtc: '2026-09-06T12:00:00Z',
      package: {
        schema: 'elitescada.hmi',
        schemaVersion: 1,
        startupScreenId: 'screen-home-id',
        screens: [{
          id: 'screen-home-id',
          key: 'home',
          name: 'Home',
          elements: []
        }],
        popups: [],
        dynamos: [],
        scripts: [],
        scriptVisualEventReferences: [],
        visualAssets: []
      }
    }
  }));
}

test('external identity does not expose local switch-user even when local login is enabled', async ({ page }) => {
  await installSessionContract(page, externalDeveloper);

  await page.goto('/');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('External Developer');
  await page.getByTestId('session-menu-toggle').click();

  await expect(page.getByTestId('session-switch-user')).toHaveCount(0);
  await expect(page.getByTestId('session-logout')).toBeVisible();
});

test('failed server invalidation keeps the current identity and Runtime interactive', async ({ page }) => {
  const contract = await installSessionContract(page, administrator, 'failure');

  await page.goto('/');
  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await page.getByTestId('session-menu-toggle').click();

  await page.getByTestId('session-switch-user').click();
  await expect(page.getByRole('alert')).toHaveText('Não foi possível invalidar a sessão atual para trocar de usuário.');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.locator('input[name="username"]')).toHaveCount(0);

  await page.getByTestId('session-logout').click();
  await expect(page.getByRole('alert')).toHaveText('Não foi possível encerrar a sessão.');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await expect(page.locator('.app-bar')).toBeVisible();

  expect(contract.logoutRequests).toBe(2);
  expect(contract.loginRequests).toBe(0);
});

test('switch-user invalidates first, blocks the shell until login, then reloads backend capabilities', async ({ page }) => {
  const contract = await installSessionContract(page, administrator);

  await page.goto('/');
  await expect(page.locator('.app-navigation a[href="/engineering"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/audit"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/licensing"]')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');

  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('session-switch-user').click();

  await expect(page.locator('.app-bar')).toHaveCount(0);
  await expect(page.getByTestId('session-menu-toggle')).toHaveCount(0);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  await expect(page.getByText('Sessão anterior encerrada. Entre com o próximo usuário para continuar.')).toBeVisible();
  expect(contract.logoutRequests).toBe(1);
  expect(contract.loginRequests).toBe(0);

  await page.locator('input[name="username"]').fill(runtimeOperator.username);
  await page.locator('input[name="password"]').fill('RuntimePass8');
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText(runtimeOperator.username);
  await expect(page.locator('.app-navigation a[href="/"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/engineering"]')).toHaveCount(0);
  await expect(page.locator('.app-navigation a[href="/audit"]')).toHaveCount(0);
  await expect(page.locator('.app-navigation a[href="/licensing"]')).toHaveCount(0);
  await expect(page.locator('.runtime-view-navigation')).toHaveCount(0);

  expect(contract.loginRequests).toBe(1);
  expect(contract.runtimeCapabilityRequests).toBeGreaterThanOrEqual(2);
});

test('fullscreen Runtime keeps the system-owned session controls reachable', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();

  await runtime.getByRole('button', { name: 'Tela cheia' }).click();
  await expect(runtime).toHaveAttribute('data-runtime-fullscreen', 'true');

  const runtimeSession = runtime.getByTestId('session-menu-toggle');
  await expect(runtimeSession).toBeVisible();
  await expect(runtimeSession).toContainText('Administrador Local');
  await runtimeSession.click();
  await expect(runtime.getByTestId('session-switch-user')).toBeVisible();
  await expect(runtime.getByTestId('session-logout')).toBeVisible();
});

test('successful sign-out removes the current client authority only after server invalidation succeeds', async ({ page }) => {
  const contract = await installSessionContract(page, administrator);

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('session-logout').click();

  await expect(page.locator('.app-bar')).toHaveCount(0);
  await expect(page.getByTestId('session-menu-toggle')).toHaveCount(0);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  expect(contract.logoutRequests).toBe(1);
});
