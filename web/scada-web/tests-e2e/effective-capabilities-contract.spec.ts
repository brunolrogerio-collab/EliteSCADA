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
