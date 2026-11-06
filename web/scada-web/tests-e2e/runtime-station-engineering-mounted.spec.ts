import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

test('Runtime station profile supports up to four windows and validates without changing Working', async ({ page, request }, testInfo) => {
  const beforeResponse = await request.get('/api/engineering/workspace');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { changeVersion: number; isDirty: boolean };
  const packageResponse = await request.get('/api/engineering/export/json');
  expect(packageResponse.ok()).toBeTruthy();
  const source = await packageResponse.json() as { screens?: Array<{ key: string }> };
  expect(source.screens?.length ?? 0).toBeGreaterThan(0);

  await page.setViewportSize({ width: 1600, height: 1000 });
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

  await testInfo.attach('runtime-station-profile-four-windows', {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });

  await workspace.getByRole('button', { name: 'Validar' }).click();
  await expect(workspace.getByRole('status')).toContainText('Configuração válida');
  await expect(workspace.getByRole('button', { name: 'Aplicar ao Working' })).toBeEnabled();

  await testInfo.attach('runtime-station-profile-four-windows-validated', {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });

  const afterResponse = await request.get('/api/engineering/workspace');
  const after = await afterResponse.json() as { changeVersion: number; isDirty: boolean };
  expect(after.changeVersion).toBe(before.changeVersion);
  expect(after.isDirty).toBe(before.isDirty);
});

test('station profiles persist in Working after applying, returning from Runtime and reloading Engineering', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const original = await originalResponse.json() as {
    screens: Array<{ key: string; name?: string }>;
    runtimePresentation?: { stationProfiles?: Array<{ name: string; windows: Array<{ screenKey: string; mode: string }> }> } | null;
    [key: string]: unknown;
  };
  expect(original.screens.length).toBeGreaterThan(0);
  const name = `Estação persistida ${Date.now()}`;

  try {
    await page.goto('/engineering/stations');
    const editor = page.getByTestId('runtime-station-editor');
    await editor.getByRole('button', { name: 'Adicionar estação' }).click();
    const station = editor.getByTestId('runtime-station-profile').last();
    await station.getByLabel('Nome da estação').fill(name);
    await station.getByRole('button', { name: 'Adicionar janela' }).click();
    await station.getByLabel('Comportamento da janela 2').selectOption('fixed');
    const secondaryScreen = original.screens[1]?.key ?? original.screens[0].key;
    await station.getByLabel('Tela 2').selectOption(secondaryScreen);

    await expect(editor.getByTestId('runtime-station-draft-warning')).toBeVisible();
    await editor.getByRole('button', { name: 'Validar' }).click();
    await expect(editor.getByRole('button', { name: 'Aplicar ao Working' })).toBeEnabled();
    await editor.getByRole('button', { name: 'Aplicar ao Working' }).click();
    await expect(editor.getByRole('status')).toContainText('Perfil salvo no Working');

    await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const current = await response.json() as typeof original;
      const persisted = current.runtimePresentation?.stationProfiles?.find(profile => profile.name === name);
      return persisted ? persisted.windows.map(window => `${window.screenKey}:${window.mode}`) : null;
    }).toEqual([`${original.screens[0].key}:navigation`, `${secondaryScreen}:fixed`]);

    // Runtime consumes the Active revision, but visiting it must not roll back Working.
    await page.goto('/');
    await page.goto('/engineering/stations');
    await expect(page.getByTestId('runtime-station-profile').last().getByLabel('Nome da estação')).toHaveValue(name);
    await page.reload();
    const reopened = page.getByTestId('runtime-station-profile').last();
    await expect(reopened.getByLabel('Nome da estação')).toHaveValue(name);
    await expect(reopened.getByLabel('Tela 2')).toHaveValue(secondaryScreen);
    await expect(reopened.getByLabel('Comportamento da janela 2')).toHaveValue('fixed');
  } finally {
    const restored = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: original
    });
    expect(restored.ok()).toBeTruthy();
  }
});
