import { expect, test } from '@playwright/test';

test('mounted Data Source editor rebuilds driver-specific fields instead of reusing incompatible settings', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();

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
  await page.getByRole('button', { name: /Data Sources/ }).click();

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
