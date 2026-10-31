import type { RuntimeSessionConnectionClass } from '../runtimeSessionAdmissionApi';

export type RuntimeStationWindowAdmissionState =
  | 'pending'
  | 'admitted'
  | 'capacity-rejected'
  | 'unavailable'
  | 'not-required'
  | 'blocked';

export type RuntimeStationAdmissionMessage = Readonly<{
  runId: string;
  stationId: string;
  slot: number;
  status: Exclude<RuntimeStationWindowAdmissionState, 'blocked'>;
  requestedClass: RuntimeSessionConnectionClass;
  grantedClass: RuntimeSessionConnectionClass | null;
  capacityReasonCode: string | null;
}>;

const CHANNEL_PREFIX = 'scadivo.runtime.station-admission.';

export function createRuntimeStationRunId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return 'station-' + Date.now() + '-' + Math.random().toString(36).slice(2);
}

export function openRuntimeStationAdmissionChannel(runId: string): BroadcastChannel | null {
  if (typeof BroadcastChannel === 'undefined') return null;
  try { return new BroadcastChannel(CHANNEL_PREFIX + runId); }
  catch { return null; }
}

export function parseRuntimeStationAdmissionMessage(value: unknown): RuntimeStationAdmissionMessage | null {
  if (!value || typeof value !== 'object') return null;
  const candidate = value as Partial<RuntimeStationAdmissionMessage>;
  if (typeof candidate.runId !== 'string' || !candidate.runId ||
      typeof candidate.stationId !== 'string' || !candidate.stationId ||
      !Number.isInteger(candidate.slot) || candidate.slot! < 0 || candidate.slot! > 3 ||
      !['pending', 'admitted', 'capacity-rejected', 'unavailable', 'not-required'].includes(candidate.status ?? '') ||
      (candidate.requestedClass !== 'interactive' && candidate.requestedClass !== 'viewOnly') ||
      (candidate.grantedClass !== null && candidate.grantedClass !== 'interactive' && candidate.grantedClass !== 'viewOnly') ||
      (candidate.capacityReasonCode !== null && typeof candidate.capacityReasonCode !== 'string')) {
    return null;
  }
  return candidate as RuntimeStationAdmissionMessage;
}
