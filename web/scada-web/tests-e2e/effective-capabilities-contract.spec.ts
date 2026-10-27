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
