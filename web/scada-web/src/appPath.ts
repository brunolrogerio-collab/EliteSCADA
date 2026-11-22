import { useSyncExternalStore } from 'react';

const pathChanged = 'scadivo:path-changed';

export function replaceAppPath(path: string) {
  if (window.location.pathname === path) return;
  window.history.replaceState(null, '', path);
  window.dispatchEvent(new Event(pathChanged));
}

function subscribePath(notify: () => void) {
  window.addEventListener(pathChanged, notify);
  window.addEventListener('popstate', notify);
  return () => {
    window.removeEventListener(pathChanged, notify);
    window.removeEventListener('popstate', notify);
  };
}

export function useAppPath() {
  return useSyncExternalStore(subscribePath, () => window.location.pathname);
}
