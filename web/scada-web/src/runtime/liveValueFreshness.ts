export type LiveValueFreshness = 'unavailable' | 'fresh' | 'aging' | 'stale';

export type LiveValueFreshnessSnapshot = Readonly<{
  state: LiveValueFreshness;
  observedAt: string | null;
  ageMilliseconds: number | null;
}>;

export type LiveValueDiagnostics = Readonly<LiveValueFreshnessSnapshot & {
  lastRequestAt: string | null;
  lastSuccessAt: string | null;
  reason: 'no-observation' | 'invalid-observation' | 'request-failed' | 'fresh' | 'aging' | 'stale';
}>;

export function describeLiveValueFreshness(
  observedAt: string | null | undefined,
  now = Date.now(),
  staleAfterMilliseconds = 30_000
): LiveValueFreshnessSnapshot {
  if (!observedAt) return { state: 'unavailable', observedAt: null, ageMilliseconds: null };

  const observedMilliseconds = Date.parse(observedAt);
  if (!Number.isFinite(observedMilliseconds)) return { state: 'unavailable', observedAt, ageMilliseconds: null };

  const ageMilliseconds = Math.max(0, now - observedMilliseconds);
  const threshold = Math.max(1, staleAfterMilliseconds);
  const state = ageMilliseconds >= threshold
    ? 'stale'
    : ageMilliseconds >= threshold / 2
      ? 'aging'
      : 'fresh';

  return { state, observedAt, ageMilliseconds };
}

export function latestLiveObservation(...timestamps: Array<string | null | undefined>): string | null {
  let latest: string | null = null;
  let latestMilliseconds = Number.NEGATIVE_INFINITY;

  for (const timestamp of timestamps) {
    if (!timestamp) continue;
    const milliseconds = Date.parse(timestamp);
    if (!Number.isFinite(milliseconds) || milliseconds < latestMilliseconds) continue;
    latest = timestamp;
    latestMilliseconds = milliseconds;
  }

  return latest;
}

/** Shared request/success/freshness view. Browser time is used only to calculate age;
 * it never becomes an observation timestamp. */
export function describeLiveValueDiagnostics(input: {
  observedAt?: string | null;
  lastRequestAt?: string | null;
  lastSuccessAt?: string | null;
  requestFailed?: boolean;
  now?: number;
  staleAfterMilliseconds?: number;
}): LiveValueDiagnostics {
  const freshness = describeLiveValueFreshness(
    input.observedAt,
    input.now,
    input.staleAfterMilliseconds
  );
  const invalidObservation = Boolean(freshness.observedAt && freshness.ageMilliseconds === null);
  const reason = input.requestFailed
    ? 'request-failed'
    : invalidObservation
      ? 'invalid-observation'
      : freshness.state === 'unavailable'
        ? 'no-observation'
        : freshness.state;
  return { ...freshness, lastRequestAt: input.lastRequestAt ?? null, lastSuccessAt: input.lastSuccessAt ?? null, reason };
}
