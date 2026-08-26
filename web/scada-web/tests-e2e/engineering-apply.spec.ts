import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('TAG editor only applies the exact candidate after a valid preview', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    schema: string;
    schemaVersion: number;
    tags: Array<{ id?: string; path: string; name: string; description?: string | null }>;
    [key: string]: unknown;
  };
  const original = originalPackage.tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(original).toBeTruthy();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number; isDirty: boolean };

  const marker = `Applied by secured editor ${Date.now()}`;
  const markerAfterEdit = `${marker} final`;

  try {
    await page.goto('/engineering');
    await page.getByRole('button', { name: /TAGs/ }).click();
    await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();

    const apply = page.getByTestId('engineering-apply');
    await expect(apply).toBeDisabled();

    await page.getByLabel('Descrição').fill(marker);
    await expect(apply).toBeDisabled();

    await page.getByRole('button', { name: 'Validar preview' }).click();
    await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();
    await expect(apply).toBeEnabled();

    // Any post-preview draft edit invalidates the retained candidate before it can be applied.
    await page.getByLabel('Descrição').fill(markerAfterEdit);
    await expect(apply).toBeDisabled();

    await page.getByRole('button', { name: 'Validar preview' }).click();
    await expect(apply).toBeEnabled();
    await apply.click();

    await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const model = await response.json() as { tags: Array<{ id?: string; path: string; description?: string | null }> };
      return model.tags.find(tag => tag.id === original!.id || tag.path === original!.path)?.description ?? null;
    }).toBe(markerAfterEdit);

    const workspaceAfterResponse = await request.get('/api/engineering/workspace');
    expect(workspaceAfterResponse.ok()).toBeTruthy();
    const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number; isDirty: boolean };
    expect(workspaceAfter.changeVersion).toBeGreaterThan(workspaceBefore.changeVersion);
    expect(workspaceAfter.isDirty).toBeTruthy();
  } finally {
    // Restore Engineering content so this mutation test does not leak changed demo data into the rest of the suite.
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});
