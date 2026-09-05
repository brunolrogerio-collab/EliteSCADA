import { useCallback, useEffect, useState } from 'react';

export type AppTheme = 'dark' | 'light';

const STORAGE_KEY = 'elitescada.app.theme';
const ATTRIBUTE = 'data-app-theme';

export function resolveAppTheme(): AppTheme {
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === 'dark' || stored === 'light') return stored;
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

export function applyAppTheme(theme: AppTheme) {
  document.documentElement.setAttribute(ATTRIBUTE, theme);
  document.documentElement.style.colorScheme = theme;
}

export function setStoredAppTheme(theme: AppTheme) {
  window.localStorage.setItem(STORAGE_KEY, theme);
  applyAppTheme(theme);
}

export function initializeAppTheme(): AppTheme {
  const theme = resolveAppTheme();
  applyAppTheme(theme);
  return theme;
}

export function useAppTheme() {
  const [theme, setTheme] = useState<AppTheme>(() => initializeAppTheme());

  useEffect(() => { applyAppTheme(theme); }, [theme]);

  const selectTheme = useCallback((next: AppTheme) => {
    setStoredAppTheme(next);
    setTheme(next);
  }, []);

  return { theme, selectTheme } as const;
}
