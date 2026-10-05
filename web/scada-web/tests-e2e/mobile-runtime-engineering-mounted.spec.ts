import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('mobile orientation is configured from its dedicated Engineering section without applying a draft', async ({ page, request }) => {
  const beforeResponse = await request.get('/api/engineering/workspace');
  expect(beforeResponse.ok()).toBeTruthy();
  const before = await beforeResponse.json() as { changeVersion: number; isDirty: boolean };
  const packageResponse = await request.get('/api/engineering/export/json');
  expect(packageResponse.ok()).toBeTruthy();
  const beforePackage = await packageResponse.json() as { runtimePresentation?: { mobileOrientation?: 'landscape' | 'portrait' } };
  const initialOrientation = beforePackage.runtimePresentation?.mobileOrientation ?? 'landscape';

  await page.goto('/engineering/mobile');
  const workspace = page.getByTestId('mobile-runtime-editor');
  const orientation = workspace.getByTestId('mobile-runtime-orientation');
  await expect(workspace).toBeVisible();
  await expect(orientation).toHaveValue(initialOrientation);
  await expect(workspace).toContainText('Versões mobile por tela e cabeçalho mobile personalizado ainda não são configurados aqui.');

  await orientation.selectOption(initialOrientation === 'landscape' ? 'portrait' : 'landscape');
  const apply = workspace.getByRole('button', { name: 'Aplicar ao Working' });
  await expect(apply).toBeDisabled();
  await workspace.getByRole('button', { name: 'Validar preview' }).click();
  await expect(workspace.getByRole('status')).toContainText('Preview válido');
  await expect(apply).toBeEnabled();

  const afterResponse = await request.get('/api/engineering/workspace');
  expect(afterResponse.ok()).toBeTruthy();
  const after = await afterResponse.json() as { changeVersion: number; isDirty: boolean };
  expect(after.changeVersion).toBe(before.changeVersion);
  expect(after.isDirty).toBe(before.isDirty);
  const afterPackageResponse = await request.get('/api/engineering/export/json');
  expect(afterPackageResponse.ok()).toBeTruthy();
  const afterPackage = await afterPackageResponse.json() as { runtimePresentation?: { mobileOrientation?: 'landscape' | 'portrait' } };
  expect(afterPackage.runtimePresentation?.mobileOrientation ?? 'landscape').toBe(initialOrientation);
});
