import { expect, test } from '@playwright/test';

test('Monaco edits canonical Script source through Preview Apply and reload', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
  const scriptName = `Python Editor E2E ${suffix}`;
  const scriptPath = `scripts/python-editor-e2e-${suffix}.py`;
  const source = 'def initialize():\n    return 42\n';

  await page.goto('/engineering');
  await page.getByRole('button', { name: /scripts/i }).click();
  await page.getByRole('button', { name: /Novo Script|New Script|Nuevo Script/i }).click();

  const identityInputs = page.locator('.script-grid--two input');
  await expect(identityInputs).toHaveCount(3);
  await identityInputs.nth(0).fill(scriptName);
  await identityInputs.nth(1).fill(scriptPath);

  const editor = page.getByTestId('python-monaco-editor');
  await expect(editor).toBeVisible();
  const monacoSurface = editor.locator('.monaco-editor');
  await expect(monacoSurface).toBeVisible();
  await monacoSurface.click({ position: { x: 180, y: 120 } });
  await page.keyboard.press('Control+A');
  await page.keyboard.insertText(source);

  const previewButton = page.locator('.script-actions > button').nth(0);
  await expect(previewButton).toBeEnabled();
  await previewButton.click();

  const applyButton = page.locator('.script-actions > button').nth(1);
  await expect(applyButton).toBeEnabled();
  await applyButton.click();

  await expect(page.locator('.script-editor h3')).toHaveText(scriptName);
  await expect(page.getByTestId('python-monaco-editor').locator('.view-lines'))
    .toContainText('def initialize():');
  await expect(page.getByTestId('python-monaco-editor').locator('.view-lines'))
    .toContainText('return 42');

  await page.locator('.script-actions > button.danger').click();
  await expect(page.locator('.script-delete-confirm')).toBeVisible();
  await page.locator('.script-delete-confirm button.danger').click();
  await expect(page.locator('.script-list__item').filter({ hasText: scriptPath })).toHaveCount(0);
});
