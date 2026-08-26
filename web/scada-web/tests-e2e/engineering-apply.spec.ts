import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

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

test('dependency-aware TAG Delete fails closed and reports blockers', async ({ request }) => {
  const exportResponse = await request.get('/api/engineering/export/json');
  expect(exportResponse.ok()).toBeTruthy();
  const model = await exportResponse.json() as {
    tags: Array<{ id?: string; path: string }>;
  };
  const tag = model.tags.find(candidate => candidate.path === 'Demo.P01.Frequency');
  expect(tag?.id).toBeTruthy();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as { changeVersion: number };

  const deletion = await request.delete(`/api/engineering/tags/${tag!.id}`, {
    headers: { 'x-elitescada-workspace-version': String(workspace.changeVersion) }
  });
  expect(deletion.status()).toBe(409);
  const conflict = await deletion.json() as {
    error: string;
    dependencies: Array<{ entityKind: string; entityKey: string; relation: string }>;
  };
  expect(conflict.dependencies.length).toBeGreaterThan(0);
  expect(conflict.dependencies.some(dependency =>
    ['alarm', 'command', 'equipment', 'screen', 'popup', 'security-role'].includes(dependency.entityKind))).toBeTruthy();

  const afterResponse = await request.get('/api/engineering/export/json');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { tags: Array<{ id?: string; path: string }> };
  expect(after.tags.some(candidate => candidate.id === tag!.id || candidate.path === tag!.path)).toBeTruthy();
});

test('explicit Alarm Delete removes only the requested entity and can be restored', async ({ request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    alarms: Array<{ id?: string; name: string }>;
    [key: string]: unknown;
  };
  const alarm = originalPackage.alarms.find(candidate => Boolean(candidate.id));
  expect(alarm?.id).toBeTruthy();

  try {
    const workspaceResponse = await request.get('/api/engineering/workspace');
    expect(workspaceResponse.ok()).toBeTruthy();
    const workspace = await workspaceResponse.json() as { changeVersion: number };

    const deletion = await request.delete(`/api/engineering/alarms/${alarm!.id}`, {
      headers: { 'x-elitescada-workspace-version': String(workspace.changeVersion) }
    });
    expect(deletion.ok()).toBeTruthy();

    const afterResponse = await request.get('/api/engineering/export/json');
    expect(afterResponse.ok()).toBeTruthy();
    const after = await afterResponse.json() as { alarms: Array<{ id?: string; name: string }> };
    expect(after.alarms.some(candidate => candidate.id === alarm!.id)).toBeFalsy();
    expect(after.alarms.length).toBe(originalPackage.alarms.length - 1);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

test('bulk TAG edit previews affected count and applies only after that Workspace version', async ({ request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    tags: Array<{ id?: string; path: string; readOnly: boolean }>;
    [key: string]: unknown;
  };
  const tag = originalPackage.tags.find(candidate => candidate.path === 'Demo.P01.Frequency');
  expect(tag?.id).toBeTruthy();

  const bulkRequest = {
    entityKind: 'tag',
    entityIds: [tag!.id],
    tags: { readOnly: !tag!.readOnly }
  };

  try {
    const previewResponse = await request.post('/api/engineering/bulk/preview', {
      data: bulkRequest
    });
    expect(previewResponse.ok()).toBeTruthy();
    const preview = await previewResponse.json() as {
      changeVersion: number;
      entityKind: string;
      affectedCount: number;
      preview: { canApply: boolean; updateCount: number; errorCount: number };
    };
    expect(preview.entityKind).toBe('tag');
    expect(preview.affectedCount).toBe(1);
    expect(preview.preview.canApply).toBeTruthy();
    expect(preview.preview.updateCount).toBe(1);
    expect(preview.preview.errorCount).toBe(0);

    const applyResponse = await request.post('/api/engineering/bulk/apply', {
      headers: { 'x-elitescada-workspace-version': String(preview.changeVersion) },
      data: bulkRequest
    });
    expect(applyResponse.ok()).toBeTruthy();
    const applied = await applyResponse.json() as {
      changeVersion: number;
      affectedCount: number;
      result: { updated: number };
    };
    expect(applied.affectedCount).toBe(1);
    expect(applied.result.updated).toBe(1);
    expect(applied.changeVersion).toBeGreaterThan(preview.changeVersion);

    const afterResponse = await request.get('/api/engineering/export/json');
    expect(afterResponse.ok()).toBeTruthy();
    const after = await afterResponse.json() as {
      tags: Array<{ id?: string; path: string; readOnly: boolean }>;
    };
    expect(after.tags.length).toBe(originalPackage.tags.length);
    expect(after.tags.find(candidate => candidate.id === tag!.id)?.readOnly).toBe(!tag!.readOnly);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

test('TAG Delete panel surfaces dependency conflict without removing the TAG', async ({ page, request }) => {
  const exportResponse = await request.get('/api/engineering/export/json');
  expect(exportResponse.ok()).toBeTruthy();
  const model = await exportResponse.json() as { tags: Array<{ id?: string; path: string }> };
  const tag = model.tags.find(candidate => candidate.path === 'Demo.P01.Frequency');
  expect(tag?.id).toBeTruthy();

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  const panel = page.locator('.eng-mutation-panel');
  await expect(panel).toBeVisible();
  await panel.getByLabel('Entidade').selectOption(tag!.id!);

  page.once('dialog', dialog => dialog.accept());
  await panel.getByTestId('engineering-delete').click();
  await expect(panel.locator('.eng-mutation-error')).toContainText('dependencies');

  const afterResponse = await request.get('/api/engineering/export/json');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { tags: Array<{ id?: string; path: string }> };
  expect(after.tags.some(candidate => candidate.id === tag!.id)).toBeTruthy();
});

test('TAG Bulk panel gates Apply behind Preview and shows affected quantity', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    tags: Array<{ id?: string; path: string; readOnly: boolean }>;
    [key: string]: unknown;
  };
  const tag = originalPackage.tags.find(candidate => candidate.path === 'Demo.P01.Frequency');
  expect(tag?.id).toBeTruthy();

  try {
    await page.goto('/engineering');
    await page.getByRole('button', { name: /TAGs/ }).click();
    const panel = page.locator('.eng-mutation-panel');
    const entity = panel.locator('.eng-bulk-entities label').filter({ hasText: tag!.path });
    await entity.getByRole('checkbox').check();

    const valueSelect = panel.getByLabel('Valor');
    await valueSelect.selectOption(String(!tag!.readOnly));

    const apply = panel.getByTestId('engineering-bulk-apply');
    await expect(apply).toBeDisabled();
    await panel.getByTestId('engineering-bulk-preview').click();
    await expect(panel.getByTestId('engineering-bulk-affected')).toHaveText('1');
    await expect(apply).toBeEnabled();

    page.once('dialog', dialog => dialog.accept());
    await apply.click();

    await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const after = await response.json() as { tags: Array<{ id?: string; readOnly: boolean }> };
      return after.tags.find(candidate => candidate.id === tag!.id)?.readOnly ?? null;
    }).toBe(!tag!.readOnly);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});
