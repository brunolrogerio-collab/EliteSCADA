import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

test('Screen editor previews, applies and reopens canonical Screen metadata', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    screens?: Array<{ id?: string; key: string; name: string; route?: string | null }>;
    [key: string]: unknown;
  };
  const originalScreen = originalPackage.screens?.[0];
  expect(originalScreen).toBeTruthy();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number };
  const nextRoute = `/wave-08-screen-${Date.now()}`;

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.getByTestId('visual-editor-canonical-renderer')).toBeVisible();
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    const screenList = page.locator('.visual-editor-screen-list');
    await screenList.getByRole('button').filter({ hasText: originalScreen!.key }).click();

    const route = page.getByLabel('Rota');
    await expect(route).toHaveValue(originalScreen!.route ?? '');
    await route.fill(nextRoute);

    const apply = page.getByTestId('visual-editor-apply');
    await expect(apply).toBeDisabled();

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(apply).toBeEnabled();

    page.once('dialog', dialog => dialog.accept());
    await apply.click();

    await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const model = await response.json() as { screens?: Array<{ id?: string; key: string; route?: string | null }> };
      return model.screens?.find(screen =>
        (originalScreen!.id && screen.id === originalScreen!.id) || screen.key === originalScreen!.key)?.route ?? null;
    }).toBe(nextRoute);

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.getByLabel('Rota')).toHaveValue(nextRoute);

    const workspaceAfterResponse = await request.get('/api/engineering/workspace');
    expect(workspaceAfterResponse.ok()).toBeTruthy();
    const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
    expect(workspaceAfter.changeVersion).toBeGreaterThan(workspaceBefore.changeVersion);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});
