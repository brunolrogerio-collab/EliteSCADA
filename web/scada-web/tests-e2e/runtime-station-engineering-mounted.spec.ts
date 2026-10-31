import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('Runtime station profile supports up to four windows and validates without changing Working', async ({ page, request }) => {
  const beforeResponse = await request.get('/api/engineering/workspace');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { changeVersion: number; isDirty: boolean };
  const packageResponse = await request.get('/api/engineering/export/json');
  expect(packageResponse.ok()).toBeTruthy();
  const source = await packageResponse.json() as { screens?: Array<{ key: string }> };
  expect(source.screens?.length ?? 0).toBeGreaterThan(0);

  await page.goto('/engineering/stations');
  const workspace = page.getByTestId('runtime-station-editor');
  await expect(workspace).toBeVisible();
  await workspace.getByRole('button', { name: 'Adicionar estação' }).click();
  const profile = workspace.getByTestId('runtime-station-profile');
  await profile.getByLabel('Nome da estação').fill('Operação EEE');
  await expect(profile.getByLabel('Tela 1')).toHaveValue(source.screens![0].key);
  for (let index = 0; index < 3; index++) await profile.getByRole('button', { name: 'Adicionar janela' }).click();
  await expect(profile.getByLabel('Tela 4')).toBeVisible();
  await expect(profile.getByRole('button', { name: 'Adicionar janela' })).toHaveCount(0);
  await profile.getByLabel('Comportamento da janela 4').selectOption('fixed');

  await workspace.getByRole('button', { name: 'Validar' }).click();
  await expect(workspace.getByRole('status')).toContainText('Configuração válida');
  await expect(workspace.getByRole('button', { name: 'Aplicar ao Working' })).toBeEnabled();

  const afterResponse = await request.get('/api/engineering/workspace');
  const after = await afterResponse.json() as { changeVersion: number; isDirty: boolean };
  expect(after.changeVersion).toBe(before.changeVersion);
  expect(after.isDirty).toBe(before.isDirty);
});
