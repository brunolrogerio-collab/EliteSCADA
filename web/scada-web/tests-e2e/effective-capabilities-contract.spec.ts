import { expect, test } from '@playwright/test';
import {
  resolveAppSurfaceAccess,
  type EffectiveCapabilities,
  type SecurityCapability
} from '../src/auth/effectiveCapabilities';
import {
  displayLicenseSchema,
  displaySignedHaEntitlement,
  displaySignedSeatTotal
} from '../src/licensing/licensingEntitlementPresentation';

for (const [locale, used, unavailable] of [
  ['pt-BR', 'Em uso', 'Indisponível'],
  ['en', 'In use', 'Unavailable'],
  ['es', 'En uso', 'No disponible']
]) {
  for (const width of [390, 1280]) {
    test(`license shows current usage or unavailable (${locale}, ${width}px)`, async ({ page }) => {
      await page.setViewportSize({ width, height: 844 });
      await page.addInitScript(value => localStorage.setItem('elitescada.engineering.locale', value), locale);
      let active = true;
      await page.route('**/api/licensing/status', route => route.fulfill({ json: {
        license: { state: 'Demo', maximumTags: 200, demoMaximumContinuousMinutes: null },
        runtime: { state: active ? 'Running' : 'Idle' },
        usage: active ? { tags: 37, interactive: 1, viewOnly: 0, observedAtUtc: '2026-10-28T00:00:00Z' } : null,
        sessionLimits: { interactiveSeats: 2, viewOnlySeats: 2 }
      } }));
      await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'fictitious' } }));
      await page.goto('/tests-e2e/licensing-harness.html');
      await expect(page.getByTestId('license-tags-total')).toContainText('200');
      await expect(page.getByTestId('license-tags-usage')).toContainText(`${used}: 37`);
      await expect(page.getByTestId('license-interactive-total')).toContainText('2');
      await expect(page.getByTestId('license-interactive-usage')).toContainText(`${used}: 1`);
      await expect(page.getByTestId('license-view-only-usage')).toContainText(`${used}: 0`);
      active = false;
      await page.getByRole('button', { name: /Atualizar|Refresh|Actualizar/ }).click();
      await expect(page.getByTestId('license-tags-usage')).toContainText(unavailable);
      await expect(page.getByTestId('license-interactive-usage')).toContainText(unavailable);
      expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
    });
  }
}

for (const [locale, label] of [['pt-BR', 'Sem limite de tempo'], ['en', 'No time limit'], ['es', 'Sin límite de tiempo']]) {
  for (const runtimeState of ['Idle', 'Running']) {
    test(`Demo duration is explicit without countdown (${locale}, ${runtimeState})`, async ({ page }) => {
      await page.setViewportSize({ width: 390, height: 844 });
      await page.addInitScript(value => window.localStorage.setItem('elitescada.engineering.locale', value), locale);
      await page.route('**/api/licensing/status', route => route.fulfill({ json: {
        license: { state: 'Demo', maximumTags: 200, demoMaximumContinuousMinutes: null },
        runtime: { state: runtimeState, activeLicenseState: runtimeState === 'Running' ? 'Demo' : null,
          maximumTags: runtimeState === 'Running' ? 200 : null, demoRemaining: null, demoExpiresAtUtc: null }
      } }));
      await page.route('**/api/licensing/request', route => route.fulfill({ json: {
        requestCode: 'ESREQ1-fictitious', machineFingerprint: 'fictitious'
      } }));
      await page.goto('/tests-e2e/licensing-harness.html');
      await expect(page.getByTestId('demo-duration')).toHaveText(label);
      await expect(page.getByText(/Demo restante|Demo remaining/)).toHaveCount(0);
      await expect(page.getByText('300')).toHaveCount(0);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
      expect(overflow).toBe(false);
    });
  }
}

for (const [locale, title, stored, unavailable, leaseLabel, yes] of [
  ['pt-BR', 'Licença online', 'Vínculo salvo', 'Não foi possível ler o estado online.', 'Lease armazenado', 'Sim'],
  ['en', 'Online license', 'Binding saved', 'Online status could not be read.', 'Stored lease', 'Yes'],
  ['es', 'Licencia online', 'Vínculo guardado', 'No fue posible leer el estado online.', 'Lease almacenado', 'Sí']
]) {
  test(`online license status is localized and remains read-only (${locale})`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.addInitScript(value => localStorage.setItem('elitescada.engineering.locale', value), locale);
    let available = true;
    await page.route('**/api/licensing/status', route => route.fulfill({ json: {
      license: { state: 'Demo', maximumTags: 200 },
      runtime: { state: 'Running', activeLicenseState: 'Demo', maximumTags: 200 },
      usage: { tags: 37, interactive: 1, viewOnly: 0, observedAtUtc: '2026-10-28T00:00:00Z' },
      onlineDevice: {
        configured: true, available, hasActivation: true, leasePresent: true,
        issuedAtUtc: '2026-10-28T12:00:00Z', notAfterUtc: '2026-10-31T12:00:00Z',
        renewalDueUtc: '2026-10-30T12:00:00Z', requestPending: false,
        clockRollbackDetected: false, isReset: false, reauthenticationRequired: false
      }
    } }));
    await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'fictitious' } }));
    await page.goto('/tests-e2e/licensing-harness.html');
    await expect(page.getByRole('heading', { name: title })).toBeVisible();
    await expect(page.getByTestId('online-license-status')).toContainText(stored);
    await expect(page.getByTestId('online-license-status')).toContainText(leaseLabel);
    await expect(page.getByTestId('online-license-status')).toContainText(yes);
    await expect(page.getByTestId('license-tags-usage')).toContainText('37');
    expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(false);

    available = false;
    await page.getByRole('button', { name: /Atualizar|Refresh|Actualizar/ }).click();
    await expect(page.getByTestId('online-license-status')).toContainText(unavailable);
    await expect(page.getByTestId('license-tags-total')).toContainText('200');
  });
}

for (const width of [390, 1280]) {
  test(`machine and online licensing are grouped in separate panels (${width}px)`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
    await page.route('**/api/licensing/status', route => route.fulfill({ json: {
      license: { state: 'Demo', maximumTags: 200 },
      runtime: { state: 'Idle' },
      onlineDevice: {
        configured: true, available: true, hasActivation: true, leasePresent: true,
        requestPending: false, clockRollbackDetected: false, isReset: false,
        reauthenticationRequired: false
      }
    } }));
    await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'ESREQ1-fictitious' } }));
    await page.goto('/tests-e2e/licensing-harness.html');

    await expect(page.locator('.licensing-grid > section > h2')).toHaveText([
      'Licença', 'Runtime', 'Solicitação da máquina', 'Licença online'
    ]);
    const machinePanel = page.getByTestId('machine-license-panel');
    await expect(machinePanel.getByRole('heading', { name: 'Instalar licença' })).toBeVisible();
    await expect(machinePanel.getByLabel('Solicitação da máquina')).toHaveValue('ESREQ1-fictitious');
    await expect(machinePanel.getByLabel('Código de licença assinado')).toBeVisible();

    const onlinePanel = page.getByTestId('online-license-panel');
    await expect(onlinePanel.getByTestId('online-license-status')).toBeVisible();
    await expect(onlinePanel.getByRole('button', { name: 'Remover vínculo local' })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(false);
  });
}

test('an unavailable duration is not presented as unlimited', async ({ page }) => {
  await page.route('**/api/licensing/status', route => route.fulfill({ json: {
    license: { state: 'Demo', maximumTags: 200 }, runtime: { state: 'Idle' }
  } }));
  await page.route('**/api/licensing/request', route => route.fulfill({ json: { requestCode: 'fictitious' } }));
  await page.goto('/tests-e2e/licensing-harness.html');
  await expect(page.getByTestId('demo-duration')).not.toHaveText(/Sem limite|No time limit|Sin límite/);
});

function capabilities(
  runtime: readonly SecurityCapability[] = [],
  workspace: readonly SecurityCapability[] = []
): EffectiveCapabilities {
  return {
    authenticationEnabled: true,
    runtime: new Set(runtime),
    workspace: new Set(workspace)
  };
}

test('application surfaces mirror independent backend capability gates', () => {
  expect(resolveAppSurfaceAccess(capabilities(['View']))).toEqual({
    runtime: true,
    history: false,
    engineering: false,
    audit: false,
    licensing: false
  });

  expect(resolveAppSurfaceAccess(capabilities(['TrendUse']))).toEqual({
    runtime: false,
    history: true,
    engineering: false,
    audit: false,
    licensing: false
  });

  expect(resolveAppSurfaceAccess(capabilities([], ['EngineeringView']))).toEqual({
    runtime: false,
    history: false,
    engineering: true,
    audit: false,
    licensing: true
  });

  expect(resolveAppSurfaceAccess(capabilities(['SystemAdmin']))).toEqual({
    runtime: false,
    history: false,
    engineering: false,
    audit: true,
    licensing: false
  });
});

test('Engineering or SystemAdmin never imply historian TrendUse', () => {
  expect(resolveAppSurfaceAccess(capabilities(['View', 'SystemAdmin'], ['EngineeringModify'])).history).toBe(false);
});

test('licensing remains reachable from workspace EngineeringView before Runtime grants exist', () => {
  const access = resolveAppSurfaceAccess(capabilities([], ['EngineeringView']));
  expect(access.licensing).toBe(true);
  expect(access.runtime).toBe(false);
  expect(access.audit).toBe(false);
});


test('licensing presentation distinguishes legacy ESLIC1 from signed ESLIC2 entitlements', () => {
  const copy = {
    none: 'Not provided',
    legacy: 'legacy',
    legacyNotSpecified: 'Not specified by ESLIC1',
    yes: 'Licensed',
    no: 'Not licensed'
  };

  const legacy = { schemaVersion: 1, interactiveSeats: null, viewOnlySeats: null, haRuntime: null };
  expect(displayLicenseSchema(legacy, copy)).toBe('ESLIC1 — legacy');
  expect(displaySignedSeatTotal(legacy.interactiveSeats, legacy, copy)).toBe('Not specified by ESLIC1');
  expect(displaySignedSeatTotal(legacy.viewOnlySeats, legacy, copy)).toBe('Not specified by ESLIC1');
  expect(displaySignedHaEntitlement(legacy.haRuntime, legacy, copy)).toBe('Not specified by ESLIC1');

  const v2 = { schemaVersion: 2, interactiveSeats: 3, viewOnlySeats: 7, haRuntime: true };
  expect(displayLicenseSchema(v2, copy)).toBe('ESLIC2');
  expect(displaySignedSeatTotal(v2.interactiveSeats, v2, copy)).toBe('3');
  expect(displaySignedSeatTotal(v2.viewOnlySeats, v2, copy)).toBe('7');
  expect(displaySignedHaEntitlement(v2.haRuntime, v2, copy)).toBe('Licensed');
});
