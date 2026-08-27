import type { AuthProfile } from './AuthGate';

export type SessionLocale = 'pt-BR' | 'en' | 'es';

export type UserSessionMenuLabels = {
  account: string;
  roles: string;
  noRoles: string;
  logout: string;
  loggingOut: string;
  logoutFailed: string;
};

export type UserSessionPresentation = {
  displayName: string;
  secondaryIdentity: string | null;
  initials: string;
  roles: string[];
};

const labelsByLocale: Record<SessionLocale, UserSessionMenuLabels> = {
  'pt-BR': {
    account: 'Conta',
    roles: 'Funções',
    noRoles: 'Nenhuma função atribuída',
    logout: 'Sair',
    loggingOut: 'Saindo…',
    logoutFailed: 'Não foi possível encerrar a sessão.'
  },
  en: {
    account: 'Account',
    roles: 'Roles',
    noRoles: 'No roles assigned',
    logout: 'Sign out',
    loggingOut: 'Signing out…',
    logoutFailed: 'The session could not be ended.'
  },
  es: {
    account: 'Cuenta',
    roles: 'Roles',
    noRoles: 'No hay roles asignados',
    logout: 'Salir',
    loggingOut: 'Saliendo…',
    logoutFailed: 'No fue posible cerrar la sesión.'
  }
};

export function resolveSessionLocale(
  storedLocale?: string | null,
  browserLanguage?: string | null
): SessionLocale {
  if (storedLocale === 'pt-BR' || storedLocale === 'en' || storedLocale === 'es') {
    return storedLocale;
  }

  const browser = browserLanguage?.toLowerCase() ?? '';
  if (browser.startsWith('es')) return 'es';
  if (browser.startsWith('en')) return 'en';
  return 'pt-BR';
}

export function getUserSessionMenuLabels(locale: SessionLocale): UserSessionMenuLabels {
  return labelsByLocale[locale];
}

export function getSessionDisplayName(profile: AuthProfile): string {
  const displayName = profile.displayName?.trim();
  if (displayName) return displayName;

  const username = profile.username?.trim();
  if (username) return username;

  return profile.subjectId;
}

export function getSessionSecondaryIdentity(profile: AuthProfile): string | null {
  const username = profile.username?.trim();
  if (!username) return null;

  const displayName = profile.displayName?.trim();
  if (!displayName || displayName === username) return null;

  return `@${username}`;
}

export function normalizeSessionRoles(roles: readonly string[]): string[] {
  const normalized: string[] = [];
  const seen = new Set<string>();

  for (const role of roles) {
    const value = role.trim();
    if (!value || seen.has(value)) continue;
    seen.add(value);
    normalized.push(value);
  }

  return normalized;
}

export function getSessionInitials(profile: AuthProfile): string {
  const source = getSessionDisplayName(profile).trim();
  if (!source) return '?';

  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    const first = parts[0]?.[0] ?? '';
    const last = parts[parts.length - 1]?.[0] ?? '';
    return `${first}${last}`.toLocaleUpperCase();
  }

  return source.slice(0, 2).toLocaleUpperCase();
}

export function buildUserSessionPresentation(profile: AuthProfile | null): UserSessionPresentation | null {
  if (!profile) return null;

  return {
    displayName: getSessionDisplayName(profile),
    secondaryIdentity: getSessionSecondaryIdentity(profile),
    initials: getSessionInitials(profile),
    roles: normalizeSessionRoles(profile.roles)
  };
}
