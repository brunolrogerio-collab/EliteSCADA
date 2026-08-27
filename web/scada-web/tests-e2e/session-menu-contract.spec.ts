import { expect, test } from '@playwright/test';
import type { AuthProfile } from '../src/auth/AuthGate';
import {
  buildUserSessionPresentation,
  getSessionDisplayName,
  getSessionInitials,
  getSessionSecondaryIdentity,
  getUserSessionMenuLabels,
  normalizeSessionRoles,
  resolveSessionLocale
} from '../src/auth/sessionMenuModel';

const profile: AuthProfile = {
  subjectId: 'subject-123',
  username: 'bruno',
  displayName: 'Bruno Rogerio',
  roles: ['developer', 'operator', 'developer', '  ']
};

test('session menu model uses stable identity fallbacks and normalized role context', () => {
  expect(getSessionDisplayName(profile)).toBe('Bruno Rogerio');
  expect(getSessionSecondaryIdentity(profile)).toBe('@bruno');
  expect(getSessionInitials(profile)).toBe('BR');
  expect(normalizeSessionRoles(profile.roles)).toEqual(['developer', 'operator']);

  expect(getSessionDisplayName({ ...profile, displayName: ' ', username: 'operator-1' })).toBe('operator-1');
  expect(getSessionDisplayName({ ...profile, displayName: undefined, username: undefined })).toBe('subject-123');
});

test('session menu locale follows stored product locale before browser language', () => {
  expect(resolveSessionLocale('es', 'en-US')).toBe('es');
  expect(resolveSessionLocale(null, 'en-GB')).toBe('en');
  expect(resolveSessionLocale('invalid', 'es-AR')).toBe('es');
  expect(resolveSessionLocale(undefined, 'pt-BR')).toBe('pt-BR');

  expect(getUserSessionMenuLabels('pt-BR').logout).toBe('Sair');
  expect(getUserSessionMenuLabels('en').logout).toBe('Sign out');
  expect(getUserSessionMenuLabels('es').logout).toBe('Salir');
});

test('session presentation exposes friendly identity and roles without leaking subject identity', () => {
  const presentation = buildUserSessionPresentation(profile);

  expect(presentation).toEqual({
    displayName: 'Bruno Rogerio',
    secondaryIdentity: '@bruno',
    initials: 'BR',
    roles: ['developer', 'operator']
  });
  expect(JSON.stringify(presentation)).not.toContain('subject-123');
});

test('session presentation is absent when authentication is disabled or no profile exists', () => {
  expect(buildUserSessionPresentation(null)).toBeNull();
});
