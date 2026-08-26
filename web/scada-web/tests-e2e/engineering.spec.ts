import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('Runtime exposes an entry to the Engineering workspace', async ({ page }) => {
  await page.goto('/');

  const engineeringLink = page.getByRole('link', { name: 'Engineering' });
  await expect(engineeringLink).toBeVisible();
  await engineeringLink.click();

  await expect(page).toHaveURL(/\/engineering$/);
  await expect(page.getByText('EliteSCADA Engineering')).toBeVisible();
});

test('Engineering workspace renders the public model and switches locale without changing Engineering identifiers', async ({ page, request }) => {
  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineering = await engineeringResponse.json() as {
    schema: string;
    schemaVersion: number;
    tags: Array<{ path: string }>;
  };

  await page.goto('/engineering');

  await expect(page.getByText('EliteSCADA Engineering')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toBeVisible();
  await expect(page.getByText(`${engineering.schema} v${engineering.schemaVersion}`)).toBeVisible();
  await expect(page.getByText(String(engineering.tags.length), { exact: true }).first()).toBeVisible();

  await page.getByRole('button', { name: /TAGs/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor estruturado de TAGs' })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();
  await expect(page.getByText('Demo.Tank01.Level', { exact: true })).toBeVisible();

  const locale = page.getByLabel('Idioma');
  await locale.selectOption('en');
  await expect(page.getByText('Project', { exact: true })).toBeVisible();
  await expect(page.getByText('Overview', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Structured TAG editor' })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();

  await page.getByLabel('Language').selectOption('es');
  await expect(page.getByText('Proyecto', { exact: true })).toBeVisible();
  await expect(page.getByText('Vista general', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Editor estructurado de TAGs' })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();

  await page.reload();
  await expect(page.getByLabel('Idioma')).toHaveValue('es');
  await expect(page.getByText('Proyecto', { exact: true })).toBeVisible();
});

test('Engineering navigation exposes current domains and structured preview editors', async ({ page }) => {
  await page.goto('/engineering');

  const sections = [
    { button: /Data Sources/, heading: 'Editor estruturado de Data Sources', expected: 'builtin.simulation' },
    { button: /Alarmes/, heading: 'Alarmes', expected: 'High discharge pressure' },
    { button: /Templates/, heading: 'Templates', expected: 'pump.standard' },
    { button: /Equipamentos/, heading: 'Equipamentos', expected: 'Demo.P01' },
    { button: /Dínamos/, heading: 'Dínamos', expected: 'dynamo.pump.standard' },
    { button: /Telas/, heading: 'Telas', expected: 'demo.overview' },
    { button: /Popups/, heading: 'Popups', expected: 'popup.pump.standard' },
    { button: /Segurança/, heading: 'Papéis e capacidades', expected: 'operator' }
  ];

  for (const section of sections) {
    await page.getByRole('button', { name: section.button }).click();
    await expect(page.getByRole('heading', { name: section.heading })).toBeVisible();
    await expect(page.getByText(section.expected, { exact: true }).first()).toBeVisible();
  }
});

test('TAG editor validates drafts without mutating Engineering Workspace', async ({ page, request }) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as { isDirty: boolean; changeVersion: number };

  const engineeringBeforeResponse = await request.get('/api/engineering/export/json');
  expect(engineeringBeforeResponse.ok()).toBeTruthy();
  const engineeringBefore = await engineeringBeforeResponse.json() as {
    tags: Array<{ id?: string; path: string; name: string }>;
  };
  const original = engineeringBefore.tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(original).toBeTruthy();

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();

  await page.getByLabel('Nome').fill('Frequency preview edit');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();
  await expect(page.getByText('Preview não altera o Workspace nem o runtime.', { exact: true })).toBeVisible();

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { isDirty: boolean; changeVersion: number };
  expect(workspaceAfter).toEqual(workspaceBefore);

  const engineeringAfterResponse = await request.get('/api/engineering/export/json');
  expect(engineeringAfterResponse.ok()).toBeTruthy();
  const engineeringAfter = await engineeringAfterResponse.json() as {
    tags: Array<{ id?: string; path: string; name: string }>;
  };
  const unchanged = engineeringAfter.tags.find(tag => tag.id === original!.id || tag.path === original!.path);
  expect(unchanged?.name).toBe(original!.name);
  expect(unchanged?.path).toBe(original!.path);

  await page.getByLabel('Path').fill('Demo Invalid Path');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('O rascunho possui erros', { exact: true })).toBeVisible();
  await expect(page.getByText('TAG_PATH_WHITESPACE', { exact: true })).toBeVisible();
});

test('Data Source editor previews public settings without exposing secret values', async ({ page, request }) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as { isDirty: boolean; changeVersion: number };

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor estruturado de Data Sources' })).toBeVisible();
  await expect(page.getByText('Referências de segredo', { exact: true })).toBeVisible();
  await expect(page.getByText('Somente referências são exibidas; nenhum segredo é materializado no editor.', { exact: true })).toBeVisible();

  await page.getByLabel('Nome').fill('Simulation preview edit');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { isDirty: boolean; changeVersion: number };
  expect(workspaceAfter).toEqual(workspaceBefore);
});
