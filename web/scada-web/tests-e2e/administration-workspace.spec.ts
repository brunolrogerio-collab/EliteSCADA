import { expect, test } from '@playwright/test';

test('Administration requires confirmation before session-affecting account changes', async ({ page }) => {
  test.setTimeout(60_000);

  await page.goto('/engineering');
  await page.locator('#engineering-locale').selectOption('en');
  await page.locator('.eng-nav button').filter({ hasText: 'Security' }).click();

  const admin = page.getByTestId('user-administration');
  await expect(admin).toBeVisible();
  await expect(page.getByTestId('admin-summary')).toBeVisible();
  await expect(admin.locator('.user-admin-session-policy strong')).toHaveText('Session consequence');
  await expect(page.getByText(/does not enumerate individual sessions/)).toBeVisible();

  const username = `ergonomics-${Date.now()}`;
  const initialPassword = 'Ergonomics-password-123!';
  const replacementPassword = 'Ergonomics-password-456!';

  await page.getByTestId('admin-create-toggle').click();
  const create = page.getByTestId('create-user-form');
  await create.locator('input[name="new-username"]').fill(username);
  await create.locator('input[name="new-display-name"]').fill('Ergonomics Operator');
  await create.locator('input[name="new-password"]').fill(initialPassword);
  await create.getByText('Operator', { exact: true }).locator('../..').locator('input[type="checkbox"]').check();
  await create.locator('button[type="submit"]').click();

  await page.getByTestId('admin-search').fill(username);
  const row = page.getByTestId('user-list').locator('.user-row').filter({ hasText: username });
  await expect(row).toBeVisible();
  await row.click();

  let updateRequests = 0;
  let passwordResetRequests = 0;
  page.on('request', request => {
    if (request.method() === 'PUT' && /\/api\/auth\/users\/[^/]+$/.test(request.url())) updateRequests += 1;
    if (request.method() === 'POST' && /\/api\/auth\/users\/[^/]+\/password-reset$/.test(request.url())) passwordResetRequests += 1;
  });

  const edit = page.getByTestId('edit-user-form');
  await edit.locator('input[name="edit-display-name"]').fill('Ergonomics Operator Updated');
  await edit.locator('input[name="edit-enabled"]').uncheck();
  await edit.getByTestId('review-user-changes').click();

  const changeConfirmation = edit.getByTestId('confirm-user-changes');
  await expect(changeConfirmation).toBeVisible();
  await expect(changeConfirmation).toContainText('Display name will change');
  await expect(changeConfirmation).toContainText('Enabled/disabled status will change');
  await expect(changeConfirmation).toContainText('The account will be disabled');
  expect(updateRequests).toBe(0);

  await changeConfirmation.getByRole('button', { name: 'Confirm and invalidate previous sessions' }).click();
  await expect.poll(() => updateRequests).toBe(1);
  await expect(page.getByRole('status')).toContainText('Previous local sessions for this account were invalidated');

  await page.getByTestId('admin-status-filter').selectOption('disabled');
  await expect(row).toBeVisible();
  await page.getByTestId('admin-status-filter').selectOption('enabled');
  await expect(row).toHaveCount(0);
  await page.getByTestId('admin-status-filter').selectOption('all');

  const refreshedRow = page.getByTestId('user-list').locator('.user-row').filter({ hasText: username });
  await refreshedRow.click();
  const refreshedEdit = page.getByTestId('edit-user-form');
  await refreshedEdit.locator('input[name="reset-password"]').fill(replacementPassword);
  await refreshedEdit.getByRole('button', { name: 'Review password reset' }).click();

  const passwordConfirmation = refreshedEdit.getByTestId('confirm-password-reset');
  await expect(passwordConfirmation).toBeVisible();
  await expect(passwordConfirmation).toContainText('all previous local sessions for this account will be invalidated');
  expect(passwordResetRequests).toBe(0);

  await passwordConfirmation.getByRole('button', { name: 'Confirm new password' }).click();
  await expect.poll(() => passwordResetRequests).toBe(1);
  await expect(page.getByRole('status')).toContainText('Password reset');
});

test('Administration keeps localized major states in pt-BR, en and es', async ({ page }) => {
  await page.goto('/engineering');

  const expectations = [
    { locale: 'pt-BR', title: 'Administração', create: 'Novo usuário', search: 'Buscar usuários' },
    { locale: 'en', title: 'Administration', create: 'New user', search: 'Search users' },
    { locale: 'es', title: 'Administración', create: 'Nuevo usuario', search: 'Buscar usuarios' }
  ];

  for (const expected of expectations) {
    await page.locator('#engineering-locale').selectOption(expected.locale);
    await page.locator('.eng-nav button').filter({ hasText: /Security|Segurança|Seguridad/ }).click();
    const admin = page.getByTestId('user-administration');
    await expect(admin.getByRole('heading', { name: expected.title })).toBeVisible();
    await expect(page.getByTestId('admin-create-toggle')).toHaveText(expected.create);
    await expect(admin.getByText(expected.search, { exact: true })).toBeVisible();
  }
});


test('Authority administration keeps applied role keys stable and new draft role keys editable', async ({ page }) => {
  test.setTimeout(60_000);

  await page.goto('/engineering');
  await page.locator('#engineering-locale').selectOption('en');
  await page.locator('.eng-nav button').filter({ hasText: 'Security' }).click();

  const fixture = await page.evaluate(async () => {
    const [usersResponse, policyResponse] = await Promise.all([
      fetch('/api/auth/users'),
      fetch('/api/auth/authority-policy')
    ]);
    if (!usersResponse.ok || !policyResponse.ok) {
      throw new Error(`Authority fixture unavailable: users=${usersResponse.status}, policy=${policyResponse.status}`);
    }

    const users = await usersResponse.json() as Array<{ username: string; roles: string[] }>;
    const policy = await policyResponse.json() as { roles: Array<{ key: string }> };
    const known = new Set(policy.roles.map(role => role.key.toLowerCase()));
    for (const user of users) {
      const roleKey = user.roles.find(role => known.has(role.toLowerCase()));
      if (roleKey) return { username: user.username, roleKey };
    }

    throw new Error('No persisted local-user role assignment was available for the stable-key regression.');
  });

  const authority = page.getByTestId('authority-policy-administration');
  await expect(authority).toBeVisible();

  const baselineRole = authority.locator('.authority-role-item').filter({ hasText: fixture.roleKey }).first();
  await expect(baselineRole).toBeVisible();
  await baselineRole.click();

  const roleKey = authority.getByTestId('authority-role-key');
  expect(await roleKey.isEditable()).toBeFalsy();
  await expect(roleKey).toHaveValue(fixture.roleKey);
  await expect(authority.getByTestId('authority-role-key-note')).toContainText('persisted assignments');
  await expect(authority.locator('.authority-assignment-summary')).toContainText(fixture.username);

  await authority.getByRole('button', { name: 'New role' }).click();
  expect(await roleKey.isEditable()).toBeTruthy();

  const draftKey = `mounted-draft-${Date.now()}`;
  await roleKey.fill(draftKey);
  await expect(roleKey).toHaveValue(draftKey);
  await expect(authority.getByTestId('authority-role-key-note')).toContainText('has not been applied yet');

  await authority.getByRole('button', { name: 'Discard draft' }).click();
});
