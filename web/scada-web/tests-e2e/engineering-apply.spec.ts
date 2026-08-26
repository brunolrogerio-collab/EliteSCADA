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

test('backend rejects an Engineering candidate when the Workspace version is stale', async ({ request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    tags: Array<{ id?: string; path: string; description?: string | null }>;
    [key: string]: unknown;
  };
  const original = originalPackage.tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(original).toBeTruthy();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number };

  const advanceMarker = `Workspace advanced ${Date.now()}`;
  const staleMarker = `${advanceMarker} stale candidate`;
  const advancedPackage = structuredClone(originalPackage);
  const stalePackage = structuredClone(originalPackage);
  advancedPackage.tags.find(tag => tag.id === original!.id || tag.path === original!.path)!.description = advanceMarker;
  stalePackage.tags.find(tag => tag.id === original!.id || tag.path === original!.path)!.description = staleMarker;

  try {
    const advance = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: advancedPackage
    });
    expect(advance.ok()).toBeTruthy();

    const staleApply = await request.post('/api/engineering/import/json/apply', {
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'x-elitescada-workspace-version': String(workspaceBefore.changeVersion)
      },
      data: stalePackage
    });
    expect(staleApply.status()).toBe(409);
    const conflict = await staleApply.json() as {
      expectedChangeVersion: number;
      currentChangeVersion: number;
    };
    expect(conflict.expectedChangeVersion).toBe(workspaceBefore.changeVersion);
    expect(conflict.currentChangeVersion).toBeGreaterThan(workspaceBefore.changeVersion);

    const afterResponse = await request.get('/api/engineering/export/json');
    expect(afterResponse.ok()).toBeTruthy();
    const after = await afterResponse.json() as { tags: Array<{ id?: string; path: string; description?: string | null }> };
    expect(after.tags.find(tag => tag.id === original!.id || tag.path === original!.path)?.description).toBe(advanceMarker);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});
