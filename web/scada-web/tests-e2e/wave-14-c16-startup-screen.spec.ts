import { expect, test } from '@playwright/test';
import { resolveRuntimeStartupScreen } from '../src/runtime/application/runtimeStartupScreen';

const lexicalFirstId = '00000000-0000-0000-0000-000000000001';
const configuredHomeId = '99999999-9999-9999-9999-999999999999';

const screens = [
  { id: lexicalFirstId, key: '00-overview', name: 'Lexical first' },
  { id: configuredHomeId, key: '99-home', name: 'Configured Home' }
];

test('Runtime starts from persisted Home identity instead of lexical Screen order', () => {
  expect(resolveRuntimeStartupScreen({
    startupScreenId: configuredHomeId,
    screens
  })).toEqual({
    screenKey: '99-home',
    diagnosticCode: null,
    detail: null
  });
});

test('Runtime fails explicitly when Home is cleared instead of choosing another Screen', () => {
  const result = resolveRuntimeStartupScreen({ startupScreenId: null, screens });
  expect(result.screenKey).toBe('');
  expect(result.diagnosticCode).toBe('HMI_RUNTIME_STARTUP_SCREEN_REQUIRED');
});

test('Runtime fails explicitly when persisted Home identity is unresolved', () => {
  const result = resolveRuntimeStartupScreen({
    startupScreenId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    screens
  });
  expect(result.screenKey).toBe('');
  expect(result.diagnosticCode).toBe('HMI_RUNTIME_STARTUP_SCREEN_UNRESOLVED');
  expect(result.detail).toContain('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
});

test('Runtime Home stable identity comparison is case-insensitive', () => {
  const result = resolveRuntimeStartupScreen({
    startupScreenId: configuredHomeId.toUpperCase(),
    screens
  });
  expect(result.screenKey).toBe('99-home');
});