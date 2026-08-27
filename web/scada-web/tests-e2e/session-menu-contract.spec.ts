import { expect, test } from '@playwright/test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import type { AuthProfile } from '../src/auth/AuthGate';
import { UserSessionMenuView } from '../src/auth/UserSessionMenuView';
import {
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
});

test('session menu view exposes identity and roles without leaking subject identity when friendly identity exists', () => {
  const labels = getUserSessionMenuLabels('pt-BR');
  const markup = renderToStaticMarkup(createElement(UserSessionMenuView, {
    profile,
    labels,
    onLogout: async () => undefined
  }));

  expect(markup).toContain('<details');
  expect(markup).toContain('<summary');
  expect(markup).toContain('Bruno Rogerio');
  expect(markup).toContain('@bruno');
  expect(markup).toContain('developer');
  expect(markup).toContain('operator');
  expect(markup).toContain('type="button"');
  expect(markup).toContain('Sair');
  expect(markup).not.toContain('subject-123');
});

test('session menu view renders nothing when authentication is disabled or no profile exists', () => {
  const markup = renderToStaticMarkup(createElement(UserSessionMenuView, {
    profile: null,
    labels: getUserSessionMenuLabels('en'),
    onLogout: async () => undefined
  }));

  expect(markup).toBe('');
});
