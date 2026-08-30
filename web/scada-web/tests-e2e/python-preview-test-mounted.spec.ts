import { expect, test } from '@playwright/test';

test('canonical Python editor mounts Preview/Test with bounded sample context', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /scripts/i }).click();
  await page.getByRole('button', { name: /Novo Script|New Script|Nuevo Script/i }).click();

  const editor = page.getByTestId('python-monaco-editor');
  await expect(editor).toBeVisible();

  const preview = editor.getByTestId('python-preview-test');
  await expect(preview).toBeVisible();
  await expect(preview.getByLabel(/Payload de evento|Event payload|Payload del evento/i)).toHaveValue(/"preview": true/);
  await expect(preview.getByTestId('python-preview-result')).toHaveAttribute('data-state', 'idle');
});
