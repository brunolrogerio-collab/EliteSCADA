import { expect, test } from '@playwright/test';

test('Engineering administers local users without exposing credentials and invalidates changed sessions', async ({ page, browser }) => {
  await page.goto('/engineering');
  await expect(page.locator('.eng-shell')).toBeVisible();

  await page.locator('#engineering-locale').selectOption('en');
  await page.locator('.eng-nav button').filter({ hasText: 'Security' }).click();
  await expect(page.getByTestId('user-administration')).toBeVisible();

  const username = `operator-${Date.now()}`;
  const initialPassword = 'Initial-user-password-123!';
  const replacementPassword = 'Replacement-password-456!';

  const create = page.getByTestId('create-user-form');
  await create.locator('input[name="new-username"]').fill(username);
  await create.locator('input[name="new-display-name"]').fill('Shift Operator');
  await create.locator('input[name="new-password"]').fill(initialPassword);
  await create.getByText('Operator', { exact: true }).locator('..').locator('input[type="checkbox"]').check();
  await create.locator('button[type="submit"]').click();

  const row = page.getByTestId('user-list').locator('.user-row').filter({ hasText: username });
  await expect(row).toBeVisible();

  const usersResponse = await page.evaluate(async () => {
    const response = await fetch('/api/auth/users');
    return { status: response.status, body: await response.json() };
  });
  expect(usersResponse.status).toBe(200);
  const created = (usersResponse.body as Array<Record<string, unknown>>)
    .find(user => user.username === username);
  expect(created).toBeTruthy();
  expect(created).not.toHaveProperty('credential');
  expect(created).not.toHaveProperty('passwordHash');
  expect(created).not.toHaveProperty('passwordSalt');
  const userId = String(created!.id);

  const localContext = await browser.newContext({
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: { Authorization: '' }
  });
  const localPage = await localContext.newPage();

  try {
    await localPage.goto('/');
    await expect(localPage.locator('.auth-card')).toBeVisible();
    await localPage.locator('input[name="username"]').fill(username);
    await localPage.locator('input[name="password"]').fill(initialPassword);
    await localPage.locator('button[type="submit"]').click();
    await expect(localPage.locator('.shell')).toBeVisible();

    const beforeChange = await localPage.evaluate(async () => (await fetch('/api/auth/me')).status);
    expect(beforeChange).toBe(200);

    const updateStatus = await page.evaluate(async ({ id }) => {
      const response = await fetch(`/api/auth/users/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Shift Operator Updated', isEnabled: true, roles: ['operator'] })
      });
      return response.status;
    }, { id: userId });
    expect(updateStatus).toBe(200);

    const afterProfileChange = await localPage.evaluate(async () => (await fetch('/api/auth/me')).status);
    expect(afterProfileChange).toBe(401);

    const resetStatus = await page.evaluate(async ({ id, password }) => {
      const response = await fetch(`/api/auth/users/${id}/password-reset`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password })
      });
      return response.status;
    }, { id: userId, password: replacementPassword });
    expect(resetStatus).toBe(204);
  } finally {
    await localContext.close();
  }

  const oldPasswordContext = await browser.newContext({
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: { Authorization: '' }
  });
  const oldPage = await oldPasswordContext.newPage();
  try {
    await oldPage.goto('/');
    await oldPage.locator('input[name="username"]').fill(username);
    await oldPage.locator('input[name="password"]').fill(initialPassword);
    await oldPage.locator('button[type="submit"]').click();
    await expect(oldPage.locator('.auth-error')).toBeVisible();
  } finally {
    await oldPasswordContext.close();
  }

  const replacementContext = await browser.newContext({
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: { Authorization: '' }
  });
  const replacementPage = await replacementContext.newPage();
  try {
    await replacementPage.goto('/');
    await replacementPage.locator('input[name="username"]').fill(username);
    await replacementPage.locator('input[name="password"]').fill(replacementPassword);
    await replacementPage.locator('button[type="submit"]').click();
    await expect(replacementPage.locator('.shell')).toBeVisible();
  } finally {
    await replacementContext.close();
  }
});
