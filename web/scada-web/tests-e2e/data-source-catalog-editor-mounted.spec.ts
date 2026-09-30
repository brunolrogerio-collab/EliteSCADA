import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('mounted Data Source editor rebuilds driver-specific fields instead of reusing incompatible settings', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();

  const editor = page.getByTestId('schema-data-source-editor');
  await expect(editor).toBeVisible();
  await editor.locator('header button').first().click();

  const typePicker = page.getByTestId('data-source-type');
  await expect(typePicker).toHaveValue('');
  await typePicker.selectOption('modbus.tcp');
  await expect(page.getByTestId('data-source-setting-host')).toBeVisible();
  await page.getByTestId('data-source-setting-host').fill('10.0.0.50');

  await typePicker.selectOption('builtin.simulation');
  await expect(page.getByTestId('data-source-setting-host')).toHaveCount(0);
  const scanInterval = page.getByTestId('data-source-setting-scanIntervalMilliseconds');
  await expect(scanInterval).toBeVisible();
  await expect(scanInterval).toHaveValue('500');
});


test('mounted Data Source editor exposes catalog failure and recovers only after explicit reload', async ({ page }) => {
  let failCatalog = true;
  await page.route('**/api/engineering/data-source-types', async route => {
    if (failCatalog) {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Service unavailable' })
      });
      return;
    }
    await route.fallback();
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();

  const editor = page.getByTestId('schema-data-source-editor');
  await expect(editor).toBeVisible();
  await editor.locator('header button').first().click();

  const typePicker = page.getByTestId('data-source-type');
  await expect(page.getByTestId('data-source-catalog-error')).toBeVisible();
  await expect(page.getByTestId('data-source-catalog-error')).toContainText('503');
  await expect(typePicker).toBeDisabled();

  failCatalog = false;
  await page.getByTestId('data-source-catalog-reload').click();

  await expect(page.getByTestId('data-source-catalog-error')).toHaveCount(0);
  await expect(typePicker).toBeEnabled();
  const optionValues = await typePicker.locator('option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value));
  expect(optionValues).toContain('builtin.simulation');
  await typePicker.selectOption('builtin.simulation');
  await expect(typePicker).toHaveValue('builtin.simulation');
  await expect(page.getByTestId('data-source-setting-scanIntervalMilliseconds')).toBeVisible();
});


test('R2 structured TAG journey keeps one context and explicit bulk mode', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();
  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Demo.P01.Frequency');
  await expect(actions.getByTestId('engineering-bulk-panel')).toHaveCount(0);
  await expect(actions.getByRole('combobox')).toHaveCount(0);
  const historian = page.getByTestId('tag-historian-disclosure');
  await expect(historian).not.toHaveAttribute('open', '');
  await historian.locator('summary').click();
  await expect(historian).toHaveAttribute('open', '');
  await test.info().attach('r2-tag-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Data Source journey progressively reveals backend advanced fields', async ({ page, request }) => {
  const response = await request.get('/api/engineering/data-source-types');
  expect(response.ok()).toBeTruthy();
  const catalog = await response.json() as {
    dataSourceTypes: Array<{
      typeKey: string;
      configurationSchema?: { dataSourceFields?: Array<{ key: string; advanced: boolean }> };
    }>;
  };
  const type = catalog.dataSourceTypes.find(candidate =>
    candidate.configurationSchema?.dataSourceFields?.some(field => field.advanced));
  expect(type, 'At least one mounted driver should expose an advanced Data Source field').toBeTruthy();
  const advanced = type!.configurationSchema!.dataSourceFields!.find(field => field.advanced)!;

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();
  const editor = page.getByTestId('schema-data-source-editor');
  await editor.getByRole('button', { name: 'Nova Fonte de dados' }).click();
  await editor.getByTestId('data-source-type').selectOption(type!.typeKey);
  const advancedField = editor.getByTestId(`data-source-setting-${advanced.key}`);
  await expect(advancedField).not.toBeVisible();
  const disclosure = editor.getByTestId('data-source-advanced-disclosure');
  await disclosure.locator('summary').click();
  await expect(advancedField).toBeVisible();
  const actions = editor.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Novo rascunho');
  await expect(actions.getByTestId('engineering-delete')).toBeDisabled();
  await test.info().attach('r2-data-source-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Alarm journey binds actions to current Alarm and discloses behavior on demand', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Alarmes/ }).click();
  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.locator('.eng-editor-picker').getByRole('button', { name: /High discharge pressure/ }).click();
  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('High discharge pressure');
  await expect(actions.getByRole('combobox')).toHaveCount(0);
  const behavior = page.getByTestId('alarm-behavior-disclosure');
  await expect(behavior).not.toHaveAttribute('open', '');
  await behavior.locator('summary').click();
  await expect(behavior).toHaveAttribute('open', '');
  await test.info().attach('r2-alarm-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Operational Event journey separates core authoring from optional context', async ({ page }) => {
  await page.goto('/engineering');
  const navigation = page.locator('.eng-nav');
  await navigation.getByRole('button', { name: /Eventos Operacionais/ }).click();
  const editor = page.getByTestId('operational-event-engineering');
  await editor.getByTestId('operational-event-new').click();
  await expect(editor.getByLabel('Nome', { exact: true })).toBeVisible();
  await expect(editor.getByLabel('Chave', { exact: true })).toBeVisible();
  const context = editor.getByTestId('operational-event-context-disclosure');
  await expect(context).not.toHaveAttribute('open', '');
  await context.locator('summary').click();
  await expect(context.getByLabel('Área', { exact: true })).toBeVisible();
  await expect(context.locator('select')).toBeVisible();
  await test.info().attach('r2-operational-event-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});
