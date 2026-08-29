import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('Development Monitor quick-adds a canonical TAG and exposes source, type, quality/state and timestamp', async ({ page, request }) => {
  const exportResponse = await request.get('/api/engineering/export/json');
  expect(exportResponse.ok()).toBeTruthy();
  const model = await exportResponse.json() as {
    tags?: Array<{ name: string; path: string; dataType: string }>;
  };
  const tag = model.tags?.find(candidate => candidate.path?.trim());
  expect(tag, 'seeded Engineering must expose at least one TAG for monitor acceptance').toBeTruthy();

  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /^Monitoramento\b/ }).click();
  await expect(page.getByTestId('engineering-development-monitor')).toBeVisible();
  await expect(page.getByTestId('project-reference-browser')).toBeVisible();

  const quickAdd = page.getByLabel('Adicionar referência exata');
  await quickAdd.fill(tag!.path);
  await page.locator('.development-monitor__add').getByRole('button', { name: 'Adicionar', exact: true }).click();

  const row = page.locator(`[data-monitor-reference="${escapeCssAttribute(tag!.path)}"]`);
  await expect(row).toBeVisible();
  await expect(row).toContainText(tag!.path);
  await expect(row).toContainText(tag!.dataType);

  const cells = row.locator('td');
  await expect(cells.nth(4)).not.toHaveText('');
  await expect(cells.nth(5)).not.toHaveText('');

  await page.reload();
  await page.locator('.eng-nav').getByRole('button', { name: /^Monitoramento\b/ }).click();
  await expect(page.locator(`[data-monitor-reference="${escapeCssAttribute(tag!.path)}"]`)).toBeVisible();

  await page.getByTestId('engineering-development-monitor').getByRole('button', { name: 'Limpar', exact: true }).click();
  await expect(page.locator(`[data-monitor-reference="${escapeCssAttribute(tag!.path)}"]`)).toHaveCount(0);
  await expect(page.getByText('Nenhuma variável está sendo monitorada.', { exact: true })).toBeVisible();
});

function escapeCssAttribute(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}
