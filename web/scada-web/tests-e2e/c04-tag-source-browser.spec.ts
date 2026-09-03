import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('TAG Source selector sends stable Data Source identity through real Preview without mutating Workspace', async ({ page, request }) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as {
    isDirty: boolean;
    changeVersion: number;
  };

  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineering = await engineeringResponse.json() as {
    tags: Array<{
      id?: string;
      path: string;
      name: string;
      source?: string | null;
      dataSourceId?: string | null;
    }>;
    dataSources: Array<{
      id?: string;
      key: string;
      name: string;
      driver: string;
    }>;
  };

  const source = engineering.dataSources.find(candidate =>
    candidate.driver === 'builtin.simulation' && Boolean(candidate.id));
  expect(source, 'Demo Engineering must expose a stable builtin.simulation Data Source for C04 browser acceptance.').toBeTruthy();

  const original = engineering.tags.find(tag => tag.source === source!.key);
  expect(original, `Expected at least one TAG associated with '${source!.key}'.`).toBeTruthy();

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: new RegExp(escapeRegex(original!.path)) }).first().click();

  const search = page.getByTestId('tag-source-search');
  const selector = page.getByTestId('tag-source-select');
  await expect(search).toBeVisible();
  await expect(selector).toBeVisible();

  await search.fill(source!.key);
  const identity = `id:${source!.id}`;
  await expect(selector.locator(`option[value="${identity}"]`)).toHaveCount(1);
  await selector.selectOption(identity);
  await expect(selector).toHaveValue(identity);

  await page.getByLabel('Nome').fill(`${original!.name} C04 preview`);

  const previewRequestPromise = page.waitForRequest(request =>
    request.method() === 'POST' && request.url().includes('/api/engineering/import/json/preview'));
  await page.getByRole('button', { name: 'Validar preview' }).click();
  const previewRequest = await previewRequestPromise;
  const candidate = previewRequest.postDataJSON() as {
    tags: Array<{
      id?: string;
      path: string;
      source?: string | null;
      dataSourceId?: string | null;
    }>;
  };

  const previewedTag = candidate.tags.find(tag =>
    (original!.id && tag.id === original!.id) || tag.path === original!.path);
  expect(previewedTag).toBeTruthy();
  expect(previewedTag?.source).toBe(source!.key);
  expect(previewedTag?.dataSourceId).toBe(source!.id);

  await expect(page.getByText('Preview não altera o Workspace nem o runtime.', { exact: true })).toBeVisible();

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as {
    isDirty: boolean;
    changeVersion: number;
  };
  expect(workspaceAfter).toEqual(workspaceBefore);
});

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
