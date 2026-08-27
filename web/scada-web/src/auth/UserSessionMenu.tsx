import { useMemo } from 'react';
import { useAuth } from './AuthGate';
import { UserSessionMenuView } from './UserSessionMenuView';
import {
  getUserSessionMenuLabels,
  resolveSessionLocale,
  type SessionLocale
} from './sessionMenuModel';
import './user-session-menu.css';

const localeKey = 'elitescada.engineering.locale';

export type UserSessionMenuProps = {
  locale?: SessionLocale;
};

export function UserSessionMenu({ locale }: UserSessionMenuProps) {
  const { profile, logout } = useAuth();

  const resolvedLocale = useMemo(
    () => locale ?? resolveSessionLocale(window.localStorage.getItem(localeKey), navigator.language),
    [locale]
  );
  const labels = useMemo(() => getUserSessionMenuLabels(resolvedLocale), [resolvedLocale]);

  return <UserSessionMenuView profile={profile} labels={labels} onLogout={logout} />;
}
