import { expect, test } from '@playwright/test';

test('canonical Python editor mounts Preview/Test with bounded sample context', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /scripts/i }).click();
  await page.getByRole('button', { name: /Novo Script|New Script|Nuevo Script/i }).click();

  const editor = page.getByTestId('python-monaco-editor');
  await expect(editor).toBeVisible();

  const preview = editor.getByTestId('python-preview-test');
  await expect(preview).toBeVisible();
  await expect(preview.getByTestId('python-preview-result')).toHaveAttribute('data-state', 'idle');
  await expect(preview.locator('textarea')).toHaveCount(0);

  await page.getByRole('button', { name: /Adicionar entry point|Add entry point|Agregar entry point/i }).click();

  await expect(preview.locator('textarea')).toHaveValue(/"preview": true/);
  await expect(preview.locator('select')).toHaveValue('initialize');
});
