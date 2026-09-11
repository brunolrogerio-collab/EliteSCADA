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
  await expect(page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /Demo\.Tank01\.Level/ }).first()).toBeVisible();

  const locale = page.getByLabel('Idioma');
  await locale.selectOption('en');
  const navigation = page.getByRole('navigation');
  await expect(navigation.getByText('Project', { exact: true })).toBeVisible();
  await expect(navigation.getByText('Overview', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Structured TAG editor' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).first()).toBeVisible();

  await page.getByLabel('Language').selectOption('es');
  await expect(navigation.getByText('Proyecto', { exact: true })).toBeVisible();
  await expect(navigation.getByText('Vista general', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Editor estructurado de TAGs' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).first()).toBeVisible();

  await page.reload();
  await expect(page.getByLabel('Idioma')).toHaveValue('es');
  await expect(page.getByRole('navigation').getByText('Proyecto', { exact: true })).toBeVisible();
});

test('Engineering navigation exposes current domains and structured preview editors', async ({ page }) => {
  await page.goto('/engineering');

  const sections = [
    { button: /Data Sources/, heading: 'Editor de Data Source', expected: 'builtin.simulation' },
    { button: /Alarmes/, heading: 'Editor estruturado de Alarmes', expected: 'High discharge pressure' },
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

test('TAG editor protects changed drafts when switching entities', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();

  await page.getByLabel('Nome').fill('Protected frequency draft');

  page.once('dialog', async dialog => {
    expect(dialog.type()).toBe('confirm');
    expect(dialog.message()).toContain('alterações não aplicadas');
    await dialog.dismiss();
  });
  await page.getByRole('button', { name: /Demo\.Tank01\.Level/ }).click();
  await expect(page.getByLabel('Nome')).toHaveValue('Protected frequency draft');
  await expect(page.getByLabel('Path')).toHaveValue('Demo.P01.Frequency');

  page.once('dialog', async dialog => {
    expect(dialog.type()).toBe('confirm');
    await dialog.accept();
  });
  await page.getByRole('button', { name: /Demo\.Tank01\.Level/ }).click();
  await expect(page.getByLabel('Path')).toHaveValue('Demo.Tank01.Level');
});

test('TAG editor previews a new TAG as a create without applying it', async ({ page, request }) => {
  const beforeResponse = await request.get('/api/engineering/export/json');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { tags: Array<{ path: string }> };

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: 'Nova TAG' }).click();
  await expect(page.getByText('Novo', { exact: true })).toBeVisible();

  await page.getByLabel('Nome').fill('Preview Created Tag');
  await page.getByLabel('Path').fill('Demo.Preview.CreatedTag');
  await page.getByRole('button', { name: 'Validar preview' }).click();

  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();
  await expect(page.getByTestId('preview-create-count')).toContainText('1 criações');

  const afterResponse = await request.get('/api/engineering/export/json');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { tags: Array<{ path: string }> };
  expect(after.tags).toEqual(before.tags);
  expect(after.tags.some(tag => tag.path === 'Demo.Preview.CreatedTag')).toBeFalsy();
});

test('Data Source editor uses the backend catalog and previews without mutating Engineering Workspace', async ({ page, request }) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as { isDirty: boolean; changeVersion: number };

  const catalogResponse = await request.get('/api/engineering/data-source-types');
  expect(catalogResponse.ok()).toBeTruthy();
  const catalog = await catalogResponse.json() as { dataSourceTypes: Array<{ typeKey: string; displayName: string }> };
  expect(catalog.dataSourceTypes.length).toBeGreaterThan(0);

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor de Data Source' })).toBeVisible();

  const typePicker = page.getByTestId('data-source-type');
  await expect(typePicker).toBeVisible();
  const optionValues = await typePicker.locator('option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value));
  expect(optionValues).toEqual(expect.arrayContaining(catalog.dataSourceTypes.map(type => type.typeKey)));

  const form = page.locator('.eng-editor-form-panel');
  await form.getByLabel('Nome').fill('Simulation preview edit');
  await page.getByTestId('data-source-preview').click();
  await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { isDirty: boolean; changeVersion: number };
  expect(workspaceAfter).toEqual(workspaceBefore);
});

test('Data Source editor rebuilds settings when source type changes and previews a new source without applying it', async ({ page, request }) => {
  const beforeResponse = await request.get('/api/engineering/export/json');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { dataSources: Array<{ key: string }> };

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();
  await page.getByRole('button', { name: 'Nova Data Source' }).click();

  const form = page.locator('.eng-editor-form-panel');
  await form.getByLabel('Nome').fill('Preview Simulation Source');
  await form.getByLabel('Chave').fill('preview.simulation');
  await page.getByTestId('data-source-type').selectOption('builtin.simulation');
  await expect(page.getByTestId('data-source-type')).toHaveValue('builtin.simulation');
  const scanInterval = page.getByTestId('data-source-setting-scanIntervalMilliseconds');
  await expect(scanInterval).toBeVisible();
  await expect(scanInterval).toHaveValue('500');

  await page.getByTestId('data-source-preview').click();
  await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();

  const afterResponse = await request.get('/api/engineering/export/json');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { dataSources: Array<{ key: string }> };
  expect(after.dataSources).toEqual(before.dataSources);
  expect(after.dataSources.some(source => source.key === 'preview.simulation')).toBeFalsy();
});

test('Alarm editor validates existing drafts and TAG references without mutating Workspace', async ({ page, request }) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as { isDirty: boolean; changeVersion: number };

  const engineeringBeforeResponse = await request.get('/api/engineering/export/json');
  expect(engineeringBeforeResponse.ok()).toBeTruthy();
  const engineeringBefore = await engineeringBeforeResponse.json() as {
    alarms: Array<{ id?: string; name: string; tagPath?: string | null; message?: string | null }>;
  };
  const original = engineeringBefore.alarms.find(alarm => alarm.name === 'High discharge pressure');
  expect(original).toBeTruthy();

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Alarmes/ }).click();
  await page.getByRole('button', { name: /High discharge pressure/ }).click();
  await expect(page.getByLabel('TAG associado')).toHaveValue('Demo.Discharge.Pressure');

  await page.getByLabel('Mensagem').fill('Pressure preview edit');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { isDirty: boolean; changeVersion: number };
  expect(workspaceAfter).toEqual(workspaceBefore);

  const engineeringAfterResponse = await request.get('/api/engineering/export/json');
  expect(engineeringAfterResponse.ok()).toBeTruthy();
  const engineeringAfter = await engineeringAfterResponse.json() as {
    alarms: Array<{ id?: string; name: string; message?: string | null }>;
  };
  const unchanged = engineeringAfter.alarms.find(alarm => alarm.id === original!.id || alarm.name === original!.name);
  expect(unchanged?.message).toBe(original!.message);

  await page.getByLabel('TAG associado').fill('Demo.Missing.Tag');
  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('O rascunho possui erros', { exact: true })).toBeVisible();
  await expect(page.getByText('ALARM_TAG_NOT_FOUND', { exact: true })).toBeVisible();
});

test('Alarm editor previews a new alarm as a create without applying it', async ({ page, request }) => {
  const beforeResponse = await request.get('/api/engineering/export/json');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { alarms: Array<{ name: string }> };

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Alarmes/ }).click();
  await page.getByRole('button', { name: 'Novo Alarme' }).click();
  await expect(page.getByText('Novo', { exact: true })).toBeVisible();

  await page.getByLabel('Nome').fill('Preview pressure alarm');
  await page.getByLabel('TAG associado').fill('Demo.Discharge.Pressure');
  await page.getByLabel('Setpoint').fill('9.5');
  await page.getByLabel('Área').fill('Demo');
  await page.getByLabel('Mensagem').fill('Preview-only pressure alarm');
  await page.getByRole('button', { name: 'Validar preview' }).click();

  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();
  await expect(page.getByTestId('preview-create-count')).toContainText('1 criações');

  const afterResponse = await request.get('/api/engineering/export/json');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { alarms: Array<{ name: string }> };
  expect(after.alarms).toEqual(before.alarms);
  expect(after.alarms.some(alarm => alarm.name === 'Preview pressure alarm')).toBeFalsy();
});
