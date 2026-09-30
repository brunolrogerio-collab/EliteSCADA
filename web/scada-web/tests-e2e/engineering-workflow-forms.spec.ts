import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('TAG authoring keeps one entity context and reveals secondary historian settings on demand', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();

  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();

  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Demo.P01.Frequency');
  await expect(actions.getByTestId('engineering-bulk-panel')).toHaveCount(0);

  const historian = page.getByTestId('tag-historian-disclosure');
  await expect(historian).not.toHaveAttribute('open', '');
  await historian.locator('summary').click();
  await expect(historian).toHaveAttribute('open', '');
});

test('Data Source authoring hides backend-declared advanced protocol fields until requested', async ({ page, request }) => {
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
  await page.getByRole('button', { name: /Data Sources/ }).click();
  const editor = page.getByTestId('schema-data-source-editor');
  await editor.getByRole('button', { name: 'Nova Data Source' }).click();
  await editor.getByTestId('data-source-type').selectOption(type!.typeKey);

  const advancedField = editor.getByTestId(`data-source-setting-${advanced.key}`);
  await expect(advancedField).not.toBeVisible();
  const disclosure = editor.getByTestId('data-source-advanced-disclosure');
  await disclosure.locator('summary').click();
  await expect(advancedField).toBeVisible();

  const actions = editor.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Novo rascunho');
  await expect(actions.getByTestId('engineering-delete')).toBeDisabled();
});

test('Alarm authoring binds destructive actions to the selected Alarm and keeps behavior secondary', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Alarmes/ }).click();

  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.getByRole('button', { name: /High discharge pressure/ }).click();

  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('High discharge pressure');
  await expect(actions.getByRole('combobox')).toHaveCount(0);

  const behavior = page.getByTestId('alarm-behavior-disclosure');
  await expect(behavior).not.toHaveAttribute('open', '');
  await behavior.locator('summary').click();
  await expect(behavior).toHaveAttribute('open', '');
});

test('Operational Event first-use flow separates core definition from optional context', async ({ page }) => {
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
  await expect(context.getByLabel('TAG', { exact: true })).toBeVisible();
});
