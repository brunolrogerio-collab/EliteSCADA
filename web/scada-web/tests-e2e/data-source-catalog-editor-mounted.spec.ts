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
  await expect(editor.getByText(/no configuration fields|não possui campos de configuração|no tiene campos de configuración/i)).toBeVisible();
});
