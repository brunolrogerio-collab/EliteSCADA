import { expect, test } from '@playwright/test';
import { describeLiveValueDiagnostics, describeLiveValueFreshness, latestLiveObservation } from '../src/runtime/liveValueFreshness';

test('live-value freshness reports only observed timestamps and has bounded stale semantics', () => {
  expect(describeLiveValueFreshness(null, 10_000)).toEqual({ state: 'unavailable', observedAt: null, ageMilliseconds: null });
  expect(describeLiveValueFreshness('2026-09-24T10:00:00.000Z', Date.parse('2026-09-24T10:00:10.000Z'), 30_000).state).toBe('fresh');
  expect(describeLiveValueFreshness('2026-09-24T10:00:00.000Z', Date.parse('2026-09-24T10:00:20.000Z'), 30_000).state).toBe('aging');
  expect(describeLiveValueFreshness('2026-09-24T10:00:00.000Z', Date.parse('2026-09-24T10:00:31.000Z'), 30_000).state).toBe('stale');
});

test('live-value freshness selects the newest valid authoritative observation without inventing one', () => {
  expect(latestLiveObservation(null, 'invalid', '2026-09-24T10:00:00.000Z', '2026-09-24T10:01:00.000Z')).toBe('2026-09-24T10:01:00.000Z');
  expect(latestLiveObservation(null, 'invalid')).toBeNull();
});

test('shared diagnostics distinguish failed transport from no observation and preserve request/success timestamps', () => {
  expect(describeLiveValueDiagnostics({ lastRequestAt: '2026-09-24T10:00:00.000Z', requestFailed: true }).reason).toBe('request-failed');
  expect(describeLiveValueDiagnostics({ lastRequestAt: '2026-09-24T10:00:00.000Z', lastSuccessAt: '2026-09-24T10:00:01.000Z' })).toMatchObject({ reason: 'no-observation', lastSuccessAt: '2026-09-24T10:00:01.000Z' });
  expect(describeLiveValueDiagnostics({ observedAt: '2026-09-24T10:00:00.000Z', now: Date.parse('2026-09-24T10:00:31.000Z'), staleAfterMilliseconds: 30_000 }).reason).toBe('stale');
});
