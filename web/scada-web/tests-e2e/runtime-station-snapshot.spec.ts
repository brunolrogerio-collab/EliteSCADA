import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('editing a station survives an unrelated Working snapshot refresh before Apply', async ({ page }) => {
  await page.goto('/tests-e2e/runtime-station-snapshot-harness.html');
  const editor = page.getByTestId('runtime-station-editor');
  await editor.getByRole('button', { name: 'Adicionar estação' }).click();
  const profile = editor.getByTestId('runtime-station-profile');
  await profile.getByLabel('Nome da estação').fill('Painel local ainda não aplicado');
  await profile.getByRole('button', { name: 'Adicionar janela' }).click();
  await profile.getByLabel('Tela 2').selectOption('pumps');
  await expect(profile.getByLabel('Tela 2')).toHaveValue('pumps');

  // Reproduce the stale-props hydration path: the parent receives an updated
  // snapshot while this component still owns unapplied local form data.
  await page.getByTestId('simulate-station-snapshot-refresh').click();
  await expect(editor.getByTestId('runtime-station-profile')).toHaveCount(1);
  await expect(editor.getByLabel('Nome da estação')).toHaveValue('Painel local ainda não aplicado');
  await expect(editor.getByLabel('Tela 2')).toHaveValue('pumps');
  await expect(editor.getByRole('button', { name: 'Aplicar ao Working' })).toBeDisabled();
});
