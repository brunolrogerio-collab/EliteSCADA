import type { RuntimeAlarmCenterEndpoint, RuntimeAlarmSuppressionSnapshot } from './alarmCenterTypes';
import { loadRuntimeAlarmSuppressions } from './alarmCenterApi';

const REFRESH_INTERVAL_MS = 10_000;
type SnapshotEndpoint = RuntimeAlarmCenterEndpoint<RuntimeAlarmSuppressionSnapshot>;
type SnapshotListener = (endpoint: SnapshotEndpoint) => void;

const listeners = new Set<SnapshotListener>();
let currentEndpoint: SnapshotEndpoint | null = null;
let refreshInFlight: Promise<SnapshotEndpoint> | null = null;
let refreshSequence = 0;
let refreshTimer: number | null = null;

/** Share one canonical suppression snapshot among visible Runtime controls. */
export function subscribeRuntimeAlarmSuppressionSnapshot(listener: SnapshotListener): () => void {
  listeners.add(listener);
  if (currentEndpoint) listener(currentEndpoint);

  if (listeners.size === 1) {
    window.addEventListener('focus', handleFocus);
    document.addEventListener('visibilitychange', handleVisibilityChange);
    refreshTimer = window.setInterval(() => {
      if (document.visibilityState !== 'hidden') void refreshRuntimeAlarmSuppressionSnapshot();
    }, REFRESH_INTERVAL_MS);
    void refreshRuntimeAlarmSuppressionSnapshot(true);
  }

  return () => {
    listeners.delete(listener);
    if (listeners.size !== 0) return;
    if (refreshTimer !== null) window.clearInterval(refreshTimer);
    refreshTimer = null;
    window.removeEventListener('focus', handleFocus);
    document.removeEventListener('visibilitychange', handleVisibilityChange);
    refreshSequence++;
    currentEndpoint = null;
  };
}

/** Force a canonical refresh after a local mutation or external page focus. */
export function refreshRuntimeAlarmSuppressionSnapshot(force = false): Promise<SnapshotEndpoint> {
  if (refreshInFlight && !force) return refreshInFlight;
  const sequence = ++refreshSequence;
  const request = loadRuntimeAlarmSuppressions().then(endpoint => {
    if (sequence === refreshSequence && listeners.size > 0) {
      currentEndpoint = endpoint;
      for (const listener of listeners) listener(endpoint);
    }
    return endpoint;
  }).finally(() => {
    if (refreshInFlight === request) refreshInFlight = null;
  });
  refreshInFlight = request;
  return request;
}

function handleFocus() {
  void refreshRuntimeAlarmSuppressionSnapshot();
}

function handleVisibilityChange() {
  if (document.visibilityState !== 'hidden') void refreshRuntimeAlarmSuppressionSnapshot();
}
