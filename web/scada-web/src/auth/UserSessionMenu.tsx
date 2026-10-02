import { useMemo } from 'react';
import { useAuth } from './AuthGate';
import { UserSessionMenuView } from './UserSessionMenuView';
import { RuntimeSessionClassPanel } from '../runtime/application/RuntimeSessionClassPanel';
import {
  getUserSessionMenuLabels,
  resolveSessionLocale,
  type SessionLocale
} from './sessionMenuModel';
import './user-session-menu.css';

const localeKey = 'elitescada.engineering.locale';

export type UserSessionMenuProps = {
  locale?: SessionLocale;
  includeRuntimeSessionControls?: boolean;
};

export function UserSessionMenu({ locale, includeRuntimeSessionControls = false }: UserSessionMenuProps) {
  const { profile, logout, switchUser, canSwitchUser } = useAuth();

  const resolvedLocale = useMemo(
    () => locale ?? resolveSessionLocale(window.localStorage.getItem(localeKey), navigator.language),
    [locale]
  );
  const labels = useMemo(() => getUserSessionMenuLabels(resolvedLocale), [resolvedLocale]);

  return (
    <UserSessionMenuView
      profile={profile}
      labels={labels}
      canSwitchUser={canSwitchUser}
      onSwitchUser={switchUser}
      onLogout={logout}
      runtimeSessionControls={includeRuntimeSessionControls ? <RuntimeSessionClassPanel locale={resolvedLocale} /> : undefined}
    />
  );
}
