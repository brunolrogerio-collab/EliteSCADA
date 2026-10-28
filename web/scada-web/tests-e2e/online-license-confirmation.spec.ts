import { expect, test } from '@playwright/test';

test('review, cancel and recover confirmation without resending the password', async ({ page }) => {
  let pending = false;
  let confirmed = false;
  let active = false;
  let confirms = 0;
  const prepared = () => ({ activationId: '00000000-0000-0000-0000-000000000123', expiresAtUtc: '2026-10-28T22:05:00Z',
    onlineActivationAvailable: true, confirmationRequested: confirmed,
    summary: { applicationName: 'Estação de bombeamento', licenseCode: 'SCV-EXAMPLE', term: 'annual',
      entitlementExpiresAtUtc: '2027-10-28T00:00:00Z', tier: 'Tags500', interactiveSeats: 2, viewOnlySeats: 3,
      haRuntime: true, assetProcessIntelligence: false, maxActivations: 1 } });
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.route('**/api/licensing/status', route => route.fulfill({ json: {
    license: { state: active ? 'Valid' : 'Demo', maximumTags: 200 }, runtime: { state: 'Idle' },
    onlineDevice: { configured: true, available: true, hasActivation: pending || active, leasePresent: active,
      requestPending: pending, confirmationPending: pending, installationPending: false, reauthenticationRequired: false }
  } }));
  await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'TEST' } }));
  await page.route('**/api/licensing/online/prepare', async route => {
    expect(route.request().postDataJSON()).toEqual({ username: 'operador', password: 'senha-teste', licenseCode: 'SCV-EXAMPLE' });
    pending = true; await route.fulfill({ json: prepared() });
  });
  await page.route('**/api/licensing/online/preparation', route => route.fulfill({ json: prepared() }));
  await page.route('**/api/licensing/online/cancel', async route => { pending = false; confirmed = false; await route.fulfill({ status: 204 }); });
  await page.route('**/api/licensing/online/confirm', async route => {
    expect(route.request().postDataJSON()).toEqual({ activationId: prepared().activationId });
    confirms++; confirmed = true;
    if (confirms === 1) await route.fulfill({ status: 503, json: { error: 'ONLINE_LICENSE_PORTAL_UNAVAILABLE' } });
    else { pending = false; active = true; await route.fulfill({ json: { installed: true } }); }
  });
  await page.goto('/tests-e2e/licensing-harness.html');
  const enter = async () => {
    await page.getByLabel('Login ou e-mail').fill('operador');
    await page.getByLabel('Senha', { exact: true }).fill('senha-teste');
    await page.getByLabel('Código da licença', { exact: true }).fill('SCV-EXAMPLE');
    await page.getByRole('button', { name: 'Consultar licença' }).click();
    await expect(page.getByText('Estação de bombeamento')).toBeVisible();
  };
  await enter();
  expect(confirms).toBe(0);
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await enter();
  await page.getByRole('button', { name: 'Confirmar ativação' }).click();
  await expect(page.getByRole('button', { name: 'Concluir ativação' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Cancelar', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Concluir ativação' }).click();
  await expect(page.getByTestId('online-license-status')).toContainText('Vínculo salvo');
  expect(confirms).toBe(2);
});
