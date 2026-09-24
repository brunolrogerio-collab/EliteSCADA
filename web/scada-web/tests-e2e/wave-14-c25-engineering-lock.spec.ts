import { expect, test } from '@playwright/test';

const lockedStatus = { configured: true, locked: true };
const unlockedStatus = { configured: true, locked: false };

test.use({ locale: 'pt-BR' });

test('locked Engineering never mounts protected workspace and keeps restricted administration available', async ({ page }) => {
  let protectedWorkspaceRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: lockedStatus }));
  await page.route('**/api/engineering/workspace', route => {
    protectedWorkspaceRequests++;
    return route.fulfill({ status: 403, json: { error: 'Engineering is locked.' } });
  });

  await page.goto('/engineering');

  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Engineering bloqueado' })).toBeVisible();
  await expect(page.getByTestId('project-json-portability')).toBeVisible();
  await expect(page.getByTestId('project-package-portability')).toBeVisible();
  await expect(page.getByTestId('user-administration').getByRole('heading', { name: 'Administração', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Licenciamento' }).last()).toHaveAttribute('href', '/licensing');
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toHaveCount(0);
  expect(protectedWorkspaceRequests).toBe(0);

  await page.getByRole('combobox', { name: 'Idioma' }).selectOption('en');
  await expect(page.getByRole('heading', { name: 'Engineering is locked' })).toBeVisible();
  await page.getByRole('combobox', { name: 'Language' }).selectOption('es');
  await expect(page.getByRole('heading', { name: 'Engineering está bloqueado' })).toBeVisible();
  expect(protectedWorkspaceRequests).toBe(0);
});

test('wrong Engineering Lock secret stays restricted and does not expose protected content', async ({ page }) => {
  let protectedWorkspaceRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: lockedStatus }));
  await page.route('**/api/engineering/lock/unlock', async route => {
    expect(route.request().postDataJSON()).toEqual({ secret: 'wrong-secret' });
    await route.fulfill({ status: 403, json: { error: 'Engineering Lock remains locked.' } });
  });
  await page.route('**/api/engineering/workspace', route => {
    protectedWorkspaceRequests++;
    return route.fulfill({ status: 403, json: { error: 'Engineering is locked.' } });
  });

  await page.goto('/engineering');
  await page.getByTestId('engineering-lock-unlock-secret').fill('wrong-secret');
  await page.getByRole('button', { name: 'Desbloquear' }).click();

  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
  await expect(page.getByRole('alert')).toContainText('continua bloqueado');
  await expect(page.getByTestId('engineering-lock-unlock-secret')).toHaveValue('');
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toHaveCount(0);
  expect(protectedWorkspaceRequests).toBe(0);
});

test('correct Engineering Lock secret transitions to full Engineering and only then loads protected workspace', async ({ page }) => {
  let protectedWorkspaceRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: lockedStatus }));
  await page.route('**/api/engineering/lock/unlock', async route => {
    expect(route.request().postDataJSON()).toEqual({ secret: 'correct-secret' });
    await route.fulfill({ json: unlockedStatus });
  });
  await page.route('**/api/engineering/workspace', async route => {
    protectedWorkspaceRequests++;
    await route.continue();
  });

  await page.goto('/engineering');
  expect(protectedWorkspaceRequests).toBe(0);

  await page.getByTestId('engineering-lock-unlock-secret').fill('correct-secret');
  await page.getByRole('button', { name: 'Desbloquear' }).click();

  await expect(page.getByTestId('engineering-lock-restricted')).toHaveCount(0);
  await expect(page.getByTestId('engineering-lock-management')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toBeVisible();
  expect(protectedWorkspaceRequests).toBeGreaterThan(0);
});

test('unlocked Engineering can configure and lock without making frontend capability the authority', async ({ page }) => {
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: { configured: false, locked: false } }));
  await page.route('**/api/engineering/lock/configure', async route => {
    expect(route.request().postDataJSON()).toEqual({ secret: 'new-lock-secret', lockImmediately: true });
    await route.fulfill({ json: lockedStatus });
  });

  await page.goto('/engineering');

  await expect(page.getByTestId('engineering-lock-management')).toBeVisible();
  await expect(page.getByTestId('engineering-lock-configure-secret')).toHaveAttribute('type', 'password');
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toBeVisible();

  await page.getByTestId('engineering-lock-configure-secret').fill('new-lock-secret');
  await page.getByRole('button', { name: 'Configurar e bloquear' }).click();

  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Licenciamento' }).last()).toBeVisible();
});

test('configured compact Lock management remains discoverable and applies a backend locked state', async ({ page }) => {
  let lockRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: unlockedStatus }));
  await page.route('**/api/engineering/lock/lock', route => { lockRequests++; return route.fulfill({ json: lockedStatus }); });
  await page.setViewportSize({ width: 700, height: 720 });
  await page.goto('/engineering');

  const management = page.getByTestId('engineering-lock-management');
  await expect(management).toBeVisible();
  await management.locator('summary').click();
  await expect(page.getByRole('button', { name: 'Bloquear agora' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Remover segredo' })).toBeVisible();
  await page.getByRole('button', { name: 'Bloquear agora' }).click();
  await expect.poll(() => lockRequests).toBe(1);
  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
});

test('configured Lock clear path applies the backend cleared state', async ({ page }) => {
  let clearRequests = 0;
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: unlockedStatus }));
  await page.route('**/api/engineering/lock/clear', route => { clearRequests++; return route.fulfill({ json: { configured: false, locked: false } }); });
  await page.setViewportSize({ width: 700, height: 720 });
  await page.goto('/engineering');

  const management = page.getByTestId('engineering-lock-management');
  await management.locator('summary').click();
  await page.getByRole('button', { name: 'Remover segredo' }).click();
  await expect.poll(() => clearRequests).toBe(1);
  await expect(page.getByTestId('engineering-lock-configure-secret')).toBeVisible();
});
