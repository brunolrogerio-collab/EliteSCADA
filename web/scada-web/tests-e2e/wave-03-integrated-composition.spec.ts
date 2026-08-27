import { expect, test } from '@playwright/test';

const projectKey = 'e2e-wave03';

test('Wave 03 integrated composition publishes, activates and operates through mounted product surfaces', async ({ page, request }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
  });

  const seeded = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 03 E2E' }
  });
  expect(seeded.ok()).toBeTruthy();

  await page.goto('/engineering');

  const lifecycle = page.locator('.eng-lifecycle-workspace');
  await expect(lifecycle).toHaveCount(1);
  await expect(lifecycle).toBeVisible();
  await expect(lifecycle.getByRole('heading', { name: 'Ciclo do Engineering' })).toBeVisible();
  await expect(lifecycle).toContainText('Wave 03 E2E');
  await expect(lifecycle).toContainText(/r\d+/);

  const publish = lifecycle.getByRole('button', { name: 'Publicar', exact: true }).first();
  await expect(publish).toBeEnabled();
  await publish.click();

  const publishConfirmation = lifecycle.getByRole('dialog');
  await expect(publishConfirmation).toContainText('Publicar a revisão?');
  await publishConfirmation.getByRole('button', { name: 'Publicar revisão' }).click();
  await expect(lifecycle).toContainText('Published');

  const activate = lifecycle.getByRole('button', { name: 'Ativar Published' });
  await expect(activate).toBeEnabled();
  await activate.click();

  const activationConfirmation = lifecycle.getByRole('dialog');
  await expect(activationConfirmation).toContainText('Ativar a revisão Published?');
  await activationConfirmation.getByRole('button', { name: 'Ativar Published' }).click();

  await expect(lifecycle).toContainText('Coincide com Active durável');

  const runtimeState = await request.get(`/api/engineering/persistence/${projectKey}/runtime`);
  expect(runtimeState.ok()).toBeTruthy();
  const runtime = await runtimeState.json() as { consistent: boolean; durable: { activeRevision?: number | null }; live: { revision?: number | null } };
  expect(runtime.consistent).toBeTruthy();
  expect(runtime.durable.activeRevision).toBeTruthy();
  expect(runtime.live.revision).toBe(runtime.durable.activeRevision);

  await page.goto('/');
  await expect(page.getByRole('region', { name: 'Central de alarmes' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Inspector de TAGs' })).toBeVisible();
  await expect(page.getByText(/ONLINE · 7 TAGs/)).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole('listbox', { name: 'Inspector de TAGs' }).getByText('Demo.P01.Current', { exact: true })).toBeVisible();
});
