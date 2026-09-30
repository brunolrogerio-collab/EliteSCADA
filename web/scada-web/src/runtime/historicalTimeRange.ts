export type HistoricalTimeRangeMode = 'live' | 'relative' | 'absolute';
export type HistoricalTimeRangeUnit = 'seconds' | 'minutes' | 'hours' | 'days';

export type HistoricalTimeRangeState = Readonly<{
  mode: HistoricalTimeRangeMode;
  relativeAmount: number;
  relativeUnit: HistoricalTimeRangeUnit;
  absoluteFromLocal: string;
  absoluteToLocal: string;
}>;

export type HistoricalQueryTimeRange =
  | Readonly<{ kind: 'relative'; durationSeconds: number; anchor: 'now' }>
  | Readonly<{ kind: 'absolute'; fromUtc: string; toUtc: string }>;

export type HistoricalTimeRangeIssue =
  | 'relative-invalid'
  | 'range-too-large'
  | 'absolute-required'
  | 'absolute-order'
  | 'absolute-ambiguous';

export const HISTORICAL_MAX_RANGE_SECONDS = 31 * 24 * 60 * 60;

export const HISTORICAL_TIME_RANGE_PRESETS = Object.freeze([
  Object.freeze({ seconds: 15 * 60, label: '15 min' }),
  Object.freeze({ seconds: 60 * 60, label: '1 h' }),
  Object.freeze({ seconds: 6 * 60 * 60, label: '6 h' }),
  Object.freeze({ seconds: 24 * 60 * 60, label: '24 h' }),
  Object.freeze({ seconds: 7 * 24 * 60 * 60, label: '7 d' })
] as const);

const UNIT_SECONDS: Readonly<Record<HistoricalTimeRangeUnit, number>> = Object.freeze({
  seconds: 1,
  minutes: 60,
  hours: 60 * 60,
  days: 24 * 60 * 60
});

export function createHistoricalTimeRangeState(options: Readonly<{
  mode?: HistoricalTimeRangeMode;
  durationSeconds?: number;
  now?: Date;
}> = {}): HistoricalTimeRangeState {
  const durationSeconds = options.durationSeconds ?? 60 * 60;
  const relative = historicalDurationParts(durationSeconds);
  const now = options.now ?? new Date();
  return Object.freeze({
    mode: options.mode ?? 'relative',
    relativeAmount: relative.amount,
    relativeUnit: relative.unit,
    absoluteFromLocal: formatLocalDateTimeInput(new Date(now.getTime() - durationSeconds * 1000)),
    absoluteToLocal: formatLocalDateTimeInput(now)
  });
}

export function historicalDurationParts(durationSeconds: number): Readonly<{
  amount: number;
  unit: HistoricalTimeRangeUnit;
}> {
  if (Number.isSafeInteger(durationSeconds) && durationSeconds > 0) {
    if (durationSeconds % UNIT_SECONDS.days === 0) return Object.freeze({ amount: durationSeconds / UNIT_SECONDS.days, unit: 'days' });
    if (durationSeconds % UNIT_SECONDS.hours === 0) return Object.freeze({ amount: durationSeconds / UNIT_SECONDS.hours, unit: 'hours' });
    if (durationSeconds % UNIT_SECONDS.minutes === 0) return Object.freeze({ amount: durationSeconds / UNIT_SECONDS.minutes, unit: 'minutes' });
  }
  return Object.freeze({ amount: Math.max(1, Math.floor(durationSeconds)), unit: 'seconds' });
}

export function historicalDurationSeconds(range: Pick<HistoricalTimeRangeState, 'relativeAmount' | 'relativeUnit'>): number {
  if (!Number.isSafeInteger(range.relativeAmount) || range.relativeAmount <= 0) return Number.NaN;
  return range.relativeAmount * UNIT_SECONDS[range.relativeUnit];
}

export function historicalPresetSeconds(range: Pick<HistoricalTimeRangeState, 'relativeAmount' | 'relativeUnit'>): number | null {
  const seconds = historicalDurationSeconds(range);
  return HISTORICAL_TIME_RANGE_PRESETS.some(preset => preset.seconds === seconds) ? seconds : null;
}

export function applyHistoricalPreset(
  range: HistoricalTimeRangeState,
  seconds: number
): HistoricalTimeRangeState {
  const relative = historicalDurationParts(seconds);
  return Object.freeze({ ...range, relativeAmount: relative.amount, relativeUnit: relative.unit });
}

export function validateHistoricalTimeRange(range: HistoricalTimeRangeState): Readonly<{
  ok: boolean;
  issues: readonly HistoricalTimeRangeIssue[];
}> {
  const issues: HistoricalTimeRangeIssue[] = [];
  if (range.mode === 'live' || range.mode === 'relative') {
    const durationSeconds = historicalDurationSeconds(range);
    if (!Number.isSafeInteger(durationSeconds) || durationSeconds <= 0) issues.push('relative-invalid');
    else if (durationSeconds > HISTORICAL_MAX_RANGE_SECONDS) issues.push('range-too-large');
  } else {
    const from = parseLocalDateTime(range.absoluteFromLocal);
    const to = parseLocalDateTime(range.absoluteToLocal);
    if (!from || !to) issues.push('absolute-required');
    else if (from.ambiguous || to.ambiguous) issues.push('absolute-ambiguous');
    else if (from.date.getTime() >= to.date.getTime()) issues.push('absolute-order');
    else if ((to.date.getTime() - from.date.getTime()) / 1000 > HISTORICAL_MAX_RANGE_SECONDS) issues.push('range-too-large');
  }
  return Object.freeze({ ok: issues.length === 0, issues: Object.freeze(issues) });
}

export function historicalTimeRangeToQuery(range: HistoricalTimeRangeState): HistoricalQueryTimeRange {
  const validation = validateHistoricalTimeRange(range);
  if (!validation.ok) throw new Error('Historical time range is invalid: ' + validation.issues.join(', '));
  if (range.mode === 'absolute') {
    const from = parseLocalDateTime(range.absoluteFromLocal)!;
    const to = parseLocalDateTime(range.absoluteToLocal)!;
    return Object.freeze({
      kind: 'absolute',
      fromUtc: from.date.toISOString(),
      toUtc: to.date.toISOString()
    });
  }
  return Object.freeze({
    kind: 'relative',
    durationSeconds: historicalDurationSeconds(range),
    anchor: 'now'
  });
}

export function resolveHistoricalTimeRange(
  range: HistoricalTimeRangeState,
  now = new Date()
): Readonly<{ from: string; to: string }> {
  const query = historicalTimeRangeToQuery(range);
  if (query.kind === 'absolute') return Object.freeze({ from: query.fromUtc, to: query.toUtc });
  const to = now.getTime();
  return Object.freeze({
    from: new Date(to - query.durationSeconds * 1000).toISOString(),
    to: new Date(to).toISOString()
  });
}

export function formatLocalDateTimeInput(date: Date): string {
  if (!Number.isFinite(date.getTime())) return '';
  const pad = (value: number) => String(value).padStart(2, '0');
  return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) +
    'T' + pad(date.getHours()) + ':' + pad(date.getMinutes()) + ':' + pad(date.getSeconds());
}

export function localTimeZoneLabel(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'Local';
}

function parseLocalDateTime(value: string): Readonly<{ date: Date; ambiguous: boolean }> | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value.trim());
  if (!match) return null;
  const parts = match.slice(1).map(item => Number(item));
  const [year, month, day, hour, minute, second = 0] = parts;
  const date = new Date(year, month - 1, day, hour, minute, second, 0);
  if (!Number.isFinite(date.getTime()) || localKey(date) !== localKeyFromParts(year, month, day, hour, minute, second)) return null;

  const target = localKey(date);
  let ambiguous = false;
  for (let deltaMinutes = -180; deltaMinutes <= 180; deltaMinutes += 15) {
    if (deltaMinutes === 0) continue;
    const candidate = new Date(date.getTime() + deltaMinutes * 60_000);
    if (localKey(candidate) === target) {
      ambiguous = true;
      break;
    }
  }
  return Object.freeze({ date, ambiguous });
}

function localKey(date: Date): string {
  return localKeyFromParts(
    date.getFullYear(),
    date.getMonth() + 1,
    date.getDate(),
    date.getHours(),
    date.getMinutes(),
    date.getSeconds()
  );
}

function localKeyFromParts(year: number, month: number, day: number, hour: number, minute: number, second: number): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return String(year).padStart(4, '0') + '-' + pad(month) + '-' + pad(day) +
    'T' + pad(hour) + ':' + pad(minute) + ':' + pad(second);
}
