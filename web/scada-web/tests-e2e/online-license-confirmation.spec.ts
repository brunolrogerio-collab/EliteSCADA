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
  await expect(page.getByRole('button', { name: 'Cancelar', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Concluir ativação' }).click();
  await expect(page.getByTestId('online-license-status')).toContainText('Vínculo salvo');
  expect(confirms).toBe(2);
});

test('allows cancelling a persisted confirmation request', async ({ page }) => {
  let pending = false;
  let confirmed = false;
  const prepared = () => ({ activationId: '00000000-0000-0000-0000-000000000123', expiresAtUtc: '2026-10-28T22:05:00Z',
    onlineActivationAvailable: true, confirmationRequested: confirmed,
    summary: { applicationName: 'Estação de bombeamento', licenseCode: 'SCV-EXAMPLE', term: 'annual',
      entitlementExpiresAtUtc: '2027-10-28T00:00:00Z', tier: 'Tags500', interactiveSeats: 2, viewOnlySeats: 3,
      haRuntime: true, assetProcessIntelligence: false, maxActivations: 1 } });
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.route('**/api/licensing/status', route => route.fulfill({ json: {
    license: { state: 'Demo', maximumTags: 200 }, runtime: { state: 'Idle' },
    onlineDevice: { configured: true, available: true, hasActivation: pending, leasePresent: false,
      requestPending: pending, confirmationPending: pending, installationPending: false, reauthenticationRequired: false }
  } }));
  await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'TEST' } }));
  await page.route('**/api/licensing/online/prepare', route => {
    pending = true; return route.fulfill({ json: { ...prepared(), confirmationRequested: false } });
  });
  await page.route('**/api/licensing/online/preparation', route => route.fulfill({ json: { ...prepared(), confirmationRequested: confirmed } }));
  await page.route('**/api/licensing/online/confirm', async route => {
    confirmed = true; await route.fulfill({ status: 503, json: { error: 'ONLINE_LICENSE_PORTAL_UNAVAILABLE' } });
  });
  await page.route('**/api/licensing/online/cancel', async route => {
    expect(route.request().postDataJSON()).toEqual({ activationId: prepared().activationId });
    pending = false; confirmed = false; await route.fulfill({ status: 204 });
  });
  await page.goto('/tests-e2e/licensing-harness.html');
  await page.getByLabel('Login ou e-mail').fill('operador');
  await page.getByLabel('Senha', { exact: true }).fill('senha-teste');
  await page.getByLabel('Código da licença', { exact: true }).fill('SCV-EXAMPLE');
  await page.getByRole('button', { name: 'Consultar licença' }).click();
  await page.getByRole('button', { name: 'Confirmar ativação' }).click();
  await expect(page.getByRole('button', { name: 'Cancelar', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(page.getByText('Consulta cancelada.')).toBeVisible();
});

test('an expired ambiguous confirmation can be discarded only after explicit notice', async ({ page }) => {
  let pending = true;
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.route('**/api/licensing/status', route => route.fulfill({ json: {
    license: { state: 'Demo', maximumTags: 200 }, runtime: { state: 'Idle' },
    onlineDevice: { configured: true, available: true, hasActivation: pending, leasePresent: false,
      requestPending: pending, confirmationPending: pending, installationPending: false, reauthenticationRequired: false }
  } }));
  await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'TEST' } }));
  await page.route('**/api/licensing/online/preparation', route => route.fulfill({ json: {
    activationId: '00000000-0000-0000-0000-000000000123', expiresAtUtc: '2020-01-01T00:00:00Z',
    onlineActivationAvailable: true, confirmationRequested: true,
    summary: { applicationName: 'Bomba', licenseCode: 'SCV-EXAMPLE', term: 'perpetual', entitlementExpiresAtUtc: null,
      tier: 'Tags500', interactiveSeats: 2, viewOnlySeats: 3, haRuntime: false, assetProcessIntelligence: false, maxActivations: 1 }
  } }));
  await page.route('**/api/licensing/online/abandon', async route => {
    expect(route.request().postDataJSON()).toEqual({ activationId: '00000000-0000-0000-0000-000000000123' });
    pending = false; await route.fulfill({ json: { abandoned: true, remoteRevocation: false } });
  });
  page.once('dialog', async dialog => {
    expect(dialog.message()).toContain('não libera uma vaga no Portal');
    await dialog.accept();
  });
  await page.goto('/tests-e2e/licensing-harness.html');
  await page.getByRole('button', { name: 'Descartar tentativa local' }).click();
  await expect(page.getByText('Tentativa local descartada. Confira a ativação no Portal.')).toBeVisible();
});


test('expired confirmation explains the refusal and remains safely cancellable', async ({ page }) => {
  let pending = true;
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await page.route('**/api/licensing/status', route => route.fulfill({ json: {
    license: { state: 'Demo', maximumTags: 200 }, runtime: { state: 'Idle' },
    onlineDevice: { configured: true, available: true, hasActivation: pending, leasePresent: false,
      requestPending: pending, confirmationPending: pending, installationPending: false, reauthenticationRequired: false }
  } }));
  await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'TEST' } }));
  await page.route('**/api/licensing/online/preparation', route => route.fulfill({ json: {
    activationId: '00000000-0000-0000-0000-000000000123', expiresAtUtc: '2026-10-29T15:09:52Z',
    onlineActivationAvailable: true, confirmationRequested: true,
    summary: { applicationName: 'Teste', licenseCode: 'SCV-EXAMPLE', term: 'annual',
      entitlementExpiresAtUtc: null, tier: 'Tags500', interactiveSeats: 2, viewOnlySeats: 3,
      haRuntime: false, assetProcessIntelligence: false, maxActivations: 1 }
  } }));
  await page.route('**/api/licensing/online/confirm', route => route.fulfill({ status: 409,
    json: { error: 'ONLINE_LICENSE_PREPARATION_EXPIRED' } }));
  await page.route('**/api/licensing/online/cancel', route => {
    pending = false; return route.fulfill({ status: 204 });
  });
  await page.goto('/tests-e2e/licensing-harness.html');
  await page.getByRole('button', { name: 'Concluir ativação' }).click();
  await expect(page.getByRole('alert')).toContainText('O prazo desta consulta terminou');
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(page.getByText('Consulta cancelada.')).toBeVisible();
  await expect(page.getByLabel('Login ou e-mail')).toBeVisible();
});
