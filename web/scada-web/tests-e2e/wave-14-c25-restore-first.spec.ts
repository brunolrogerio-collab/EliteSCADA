import { expect, test } from '@playwright/test';

const localProfile = {
  subjectId: '94000000-0000-0000-0000-000000000010',
  username: 'administrator',
  displayName: 'Administrator',
  roles: ['developer'],
  identityProvider: 'local'
};

function authConfig(initialAdministratorRequired: boolean) {
  return {
    authenticationEnabled: true,
    localLoginEnabled: true,
    initialAdministratorRequired,
    initialAdministratorSetupAvailable: initialAdministratorRequired,
    initialAdministratorBlockedReason: null,
    passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
  };
}

test.use({ locale: 'pt-BR' });

test('fresh true-empty bootstrap exposes Restore backup before disposable Administrator creation', async ({ page }) => {
  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(true) }));
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401 }));

  await page.goto('/');

  await expect(page.getByRole('button', { name: 'Restaurar backup' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Criar Administrador' })).toBeVisible();

  await page.getByRole('button', { name: 'Restaurar backup' }).click();
  await expect(page.getByTestId('restore-first-bootstrap')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Restaurar backup' })).toBeVisible();
  await expect(page.getByLabel('Usuário')).toHaveCount(0);
});

test('bootstrap recovery restores Authority then requires real restored login before application Apply', async ({ page }) => {
  let authorityRestored = false;
  let anonymousApplicationApply = 0;

  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(!authorityRestored) }));
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401 }));
  await page.route('**/api/auth/bootstrap/authority-backup/preview', async route => {
    expect(route.request().postDataJSON()).toEqual({ backup: '{"backup":true}', password: 'authority-secret' });
    await route.fulfill({ json: { userCount: 1, enabledAdministratorCount: 1 } });
  });
  await page.route('**/api/system-recovery/bootstrap/application/preview', async route => {
    expect(route.request().postDataBuffer()?.length ?? 0).toBeGreaterThan(0);
    await route.fulfill({ json: { canApply: true, blockers: [] } });
  });
  await page.route('**/api/auth/bootstrap/authority-backup/apply', async route => {
    authorityRestored = true;
    await route.fulfill({ json: { applied: true, signInRequired: true } });
  });
  await page.route('**/api/system-recovery/application/apply', async route => {
    anonymousApplicationApply++;
    await route.fulfill({ json: { recovered: true, stage: 'complete' } });
  });

  await page.goto('/');
  await page.getByRole('button', { name: 'Restaurar backup' }).click();
  await page.getByTestId('recovery-application-file').setInputFiles({
    name: 'plant.escadapkg',
    mimeType: 'application/vnd.elitescada.project-package',
    buffer: Buffer.from('application-package')
  });
  await page.getByTestId('recovery-authority-file').setInputFiles({
    name: 'authority.json',
    mimeType: 'application/json',
    buffer: Buffer.from('{"backup":true}')
  });
  await page.getByTestId('recovery-authority-password').fill('authority-secret');
  await page.getByRole('button', { name: 'Validar backups' }).click();
  await expect(page.getByText('Backups válidos. A Authority pode ser restaurada sem criar um usuário provisório.')).toBeVisible();
  await page.getByRole('button', { name: 'Restaurar Authority' }).click();

  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
  await expect(page.getByText('Authority restaurada. Entre com um Administrador restaurado para continuar a recuperação da aplicação.')).toBeVisible();
  expect(anonymousApplicationApply).toBe(0);
});

test('restored local Administrator can recover application without creating disposable project; optional license failure is non-blocking', async ({ page }) => {
  let firstProjectPosts = 0;
  let applicationApplies = 0;
  let licenseInstalls = 0;

  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(false) }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: localProfile }));
  await page.route('**/api/auth/local-session', route => route.fulfill({ json: { authenticated: true, username: 'administrator' } }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({ json: { enabled: true, hasProjects: false } }));
  await page.route('**/api/engineering/persistence/projects/first', async route => {
    firstProjectPosts++;
    await route.fulfill({ status: 201, json: {} });
  });
  await page.route('**/api/system-recovery/application/preview', route => route.fulfill({
    json: {
      canApply: true,
      projectCatalogEmpty: true,
      runtimeBindingMatches: true,
      blockers: [],
      currentUserAdmission: { allowed: true }
    }
  }));
  await page.route('**/api/system-recovery/application/apply', async route => {
    applicationApplies++;
    await route.fulfill({ json: { recovered: true, stage: 'complete', issues: [] } });
  });
  await page.route('**/api/licensing/install', async route => {
    licenseInstalls++;
    await route.fulfill({ status: 400, json: { error: 'License is not valid for this machine.' } });
  });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Criar novo projeto' })).toBeVisible();
  await page.getByRole('button', { name: 'Importar aplicação' }).click();
  await expect(page.getByTestId('restore-first-application')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Importar aplicação' })).toBeVisible();

  await page.getByTestId('recovery-application-file').setInputFiles({
    name: 'plant.escadapkg',
    mimeType: 'application/vnd.elitescada.project-package',
    buffer: Buffer.from('application-package')
  });
  await page.getByTestId('recovery-license-file').setInputFiles({
    name: 'license.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('OPTIONAL-LICENSE-CODE')
  });
  await page.getByRole('button', { name: 'Validar aplicação' }).click();
  await expect(page.getByText('Pacote válido e compatível com esta recuperação.')).toBeVisible();
  await page.getByRole('button', { name: 'Importar aplicação' }).click();

  await expect(page.getByText('A aplicação foi recuperada, mas a licença opcional não pôde ser instalada.')).toBeVisible();
  expect(applicationApplies).toBe(1);
  expect(licenseInstalls).toBe(1);
  expect(firstProjectPosts).toBe(0);

  await page.getByRole('button', { name: 'Continuar sem licença' }).click();
  await expect(page.getByTestId('restore-first-application')).toHaveCount(0);
  expect(firstProjectPosts).toBe(0);
});


test('restore-first keeps the prevalidated Application and optional license bound through restored Administrator login', async ({ page }) => {
  let authorityRestored = false;
  let loggedIn = false;
  let canonicalApplicationPreviews = 0;
  let applicationApplies = 0;
  let licenseInstalls = 0;

  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(!authorityRestored) }));
  await page.route('**/api/auth/me', route => route.fulfill(loggedIn ? { json: localProfile } : { status: 401 }));
  await page.route('**/api/auth/login', async route => {
    loggedIn = true;
    await route.fulfill({ json: localProfile });
  });
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: false }
  }));
  await page.route('**/api/auth/bootstrap/authority-backup/preview', route => route.fulfill({
    json: { userCount: 1, enabledAdministratorCount: 1 }
  }));
  await page.route('**/api/system-recovery/bootstrap/application/preview', route => route.fulfill({
    json: { canApply: true, blockers: [] }
  }));
  await page.route('**/api/auth/bootstrap/authority-backup/apply', async route => {
    authorityRestored = true;
    await route.fulfill({ json: { applied: true, signInRequired: true } });
  });
  await page.route('**/api/system-recovery/application/preview', async route => {
    canonicalApplicationPreviews++;
    expect(route.request().postDataBuffer()?.toString()).toBe('application-package');
    await route.fulfill({
      json: {
        canApply: true,
        projectCatalogEmpty: true,
        runtimeBindingMatches: true,
        blockers: [],
        currentUserAdmission: { allowed: true }
      }
    });
  });
  await page.route('**/api/system-recovery/application/apply', async route => {
    applicationApplies++;
    expect(route.request().postDataBuffer()?.toString()).toBe('application-package');
    await route.fulfill({ json: { recovered: true, stage: 'complete', issues: [] } });
  });
  await page.route('**/api/licensing/install', async route => {
    licenseInstalls++;
    expect(route.request().postDataJSON()).toEqual({ licenseCode: 'OPTIONAL-LICENSE-CODE' });
    await route.fulfill({ json: { installed: true } });
  });

  await page.goto('/');
  await page.getByRole('button', { name: 'Restaurar backup' }).click();
  await page.getByTestId('recovery-application-file').setInputFiles({
    name: 'plant.escadapkg',
    mimeType: 'application/vnd.elitescada.project-package',
    buffer: Buffer.from('application-package')
  });
  await page.getByTestId('recovery-authority-file').setInputFiles({
    name: 'authority.json',
    mimeType: 'application/json',
    buffer: Buffer.from('{"backup":true}')
  });
  await page.getByTestId('recovery-license-file').setInputFiles({
    name: 'license.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('OPTIONAL-LICENSE-CODE')
  });
  await page.getByTestId('recovery-authority-password').fill('authority-secret');

  await page.getByRole('button', { name: 'Validar backups' }).click();
  await page.getByRole('button', { name: 'Restaurar Authority' }).click();

  await page.getByLabel('Usuário').fill('administrator');
  await page.getByLabel('Senha').fill('restored-secret');
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page.getByTestId('restore-first-application')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Continuar recuperação' })).toBeVisible();
  await expect(page.getByTestId('recovery-continuation-summary')).toContainText('plant.escadapkg');
  await expect(page.getByTestId('recovery-continuation-summary')).toContainText('license.txt');
  await expect(page.getByTestId('recovery-application-file')).toHaveCount(0);
  await expect(page.getByTestId('recovery-license-file')).toHaveCount(0);
  await expect(page.getByTestId('recovery-authority-password')).toHaveCount(0);

  await page.getByRole('button', { name: 'Continuar recuperação' }).click();
  expect(canonicalApplicationPreviews).toBe(1);

  await page.getByRole('button', { name: 'Importar aplicação selecionada' }).click();
  await expect(page.getByTestId('restore-first-application')).toHaveCount(0);
  expect(applicationApplies).toBe(1);
  expect(licenseInstalls).toBe(1);
});

test('interrupted post-Authority recovery is explicit about browser file loss and requires reselect before fresh Preview', async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem('elitescada.auth.restore-first-continuation', JSON.stringify({
      version: 1,
      authorityRestored: true,
      application: { name: 'plant.escadapkg', size: 19 },
      license: { name: 'license.txt', size: 21 }
    }));
  });
  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(false) }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: localProfile }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 'administrator' }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: false }
  }));

  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Continuar recuperação' })).toBeVisible();
  await expect(page.getByTestId('recovery-continuation-summary')).toContainText('plant.escadapkg');
  await expect(page.getByText(/navegador perdeu os arquivos selecionados/i)).toBeVisible();
  await expect(page.getByTestId('recovery-application-file')).toBeVisible();
  await expect(page.getByTestId('recovery-license-file')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Importar aplicação selecionada' })).toBeDisabled();
});

test('auth and restore system surfaces consume the active semantic theme in Light and Dark', async ({ page }) => {
  await page.addInitScript(() => {
    if (!localStorage.getItem('elitescada.app.theme'))
      localStorage.setItem('elitescada.app.theme', 'light');
  });
  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfig(true) }));
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401 }));

  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');

  const light = await page.locator('.auth-card').evaluate(element => ({
    card: getComputedStyle(element).backgroundColor,
    page: getComputedStyle(document.querySelector('.auth-page')!).backgroundColor,
    input: getComputedStyle(document.querySelector('input[name="bootstrap-username"]')!).backgroundColor
  }));

  await page.getByRole('button', { name: 'Restaurar backup' }).click();
  await expect(page.getByTestId('recovery-application-file')).toBeVisible();
  const lightFile = await page.getByTestId('recovery-application-file').evaluate(element => getComputedStyle(element).backgroundColor);

  await page.evaluate(() => localStorage.setItem('elitescada.app.theme', 'dark'));
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'dark');

  const dark = await page.locator('.auth-card').evaluate(element => ({
    card: getComputedStyle(element).backgroundColor,
    page: getComputedStyle(document.querySelector('.auth-page')!).backgroundColor,
    input: getComputedStyle(document.querySelector('input[name="bootstrap-username"]')!).backgroundColor
  }));

  expect(light.card).not.toBe(dark.card);
  expect(light.page).not.toBe(dark.page);
  expect(light.input).not.toBe(dark.input);

  await page.getByRole('button', { name: 'Restaurar backup' }).click();
  const darkFile = await page.getByTestId('recovery-application-file').evaluate(element => getComputedStyle(element).backgroundColor);
  expect(lightFile).not.toBe(darkFile);
});
