import { expect, test } from '@playwright/test';
import {
  resolveAppSurfaceAccess,
  type EffectiveCapabilities,
  type SecurityCapability
} from '../src/auth/effectiveCapabilities';

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

  expect(resolveAppSurfaceAccess(capabilities([], ['EngineeringModify']))).toEqual({
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

test('licensing remains reachable from workspace EngineeringModify before Runtime grants exist', () => {
  const access = resolveAppSurfaceAccess(capabilities([], ['EngineeringModify']));
  expect(access.licensing).toBe(true);
  expect(access.runtime).toBe(false);
  expect(access.audit).toBe(false);
});
