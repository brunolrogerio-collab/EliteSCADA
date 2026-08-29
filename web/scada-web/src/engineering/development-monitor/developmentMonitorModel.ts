import type { CommunicationDriverDiagnostic } from '../types';
import {
  resolveProjectReference,
  type ClientMemoryDefinitionView,
  type ProjectReferenceDescriptor
} from '../project-reference/projectReferenceModel';
import type {
  RuntimeTagRealtimeMessage,
  RuntimeTagSnapshot
} from '../../runtime/liveTagTransport';

export type MonitorSample = Readonly<{
  reference: string;
  value: unknown;
  dataType: string;
  quality?: string | number | null;
  state?: string | number | null;
  sourceTimestamp?: string | null;
  observedAt: string;
  detail?: string | null;
}>;

export type MonitorQuickAddResult = Readonly<{
  status: 'found' | 'ambiguous' | 'notFound';
  reference?: string;
}>;

const TAG_QUALITY_NAMES: Readonly<Record<number, string>> = Object.freeze({
  0: 'Good',
  1: 'Uncertain',
  2: 'Bad',
  3: 'BadCommunication',
  4: 'BadConfiguration',
  5: 'BadDevice',
  6: 'Stale',
  7: 'Disabled'
});

export function resolveMonitorQuickAdd(
  catalog: readonly ProjectReferenceDescriptor[],
  rawReference: string
): MonitorQuickAddResult {
  const resolved = resolveProjectReference(catalog, rawReference);
  return resolved.status === 'found' && resolved.descriptor
    ? Object.freeze({ status: 'found', reference: resolved.descriptor.reference })
    : Object.freeze({ status: resolved.status });
}

export function mergeMonitorBatchSamples(
  current: ReadonlyMap<string, MonitorSample>,
  selected: readonly string[],
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>,
  tags: readonly RuntimeTagSnapshot[],
  drivers: readonly CommunicationDriverDiagnostic[],
  clientDefinitions: readonly ClientMemoryDefinitionView[],
  readClientValue: (reference: string) => unknown,
  observedAt = new Date().toISOString()
): ReadonlyMap<string, MonitorSample> {
  const next = new Map<string, MonitorSample>(current);
  const tagByPath = new Map<string, RuntimeTagSnapshot>(tags.map(tag => [tag.path, tag] as const));
  const tagById = new Map<string, RuntimeTagSnapshot>(tags.map(tag => [normalizeIdentity(tag.id), tag] as const));
  const driverByKey = new Map<string, CommunicationDriverDiagnostic>(drivers.map(driver => [driver.dataSourceKey, driver] as const));

  for (const reference of selected) {
    const descriptor = descriptors.get(reference);
    if (!descriptor) continue;

    if (descriptor.family === 'tag' || descriptor.family === 'serverMemory') {
      const tagId = descriptor.tagReference?.tagId;
      const tag = tagId ? tagById.get(normalizeIdentity(tagId)) : tagByPath.get(reference);
      next.set(reference, projectSnapshotSample(descriptor, tag, observedAt));
      continue;
    }

    if (descriptor.family === 'clientMemory') {
      const definition = clientDefinitions.find(candidate => candidate.path === reference);
      next.set(reference, Object.freeze({
        reference,
        value: definition ? readClientValue(reference) : null,
        dataType: definition?.dataType ?? descriptor.dataType,
        state: definition ? 'LocalSession' : 'Unavailable',
        sourceTimestamp: null,
        observedAt
      }));
      continue;
    }

    if (reference === 'system.runtime.tagCount') {
      next.set(reference, sample(reference, tags.length, 'Int32', 'Available', observedAt));
      continue;
    }
    if (reference === 'system.runtime.driverCount') {
      next.set(reference, sample(reference, drivers.length, 'Int32', 'Available', observedAt));
      continue;
    }

    if (descriptor.family === 'driverDiagnostic') {
      const prefix = 'driver:';
      const separator = reference.lastIndexOf(':');
      const driverKey = reference.startsWith(prefix) && separator > prefix.length
        ? reference.slice(prefix.length, separator)
        : '';
      const field = separator >= 0 ? reference.slice(separator + 1) : '';
      const driver = driverByKey.get(driverKey);
      if (!driver || !field) {
        next.set(reference, unavailableSample(descriptor, observedAt));
        continue;
      }
      const driverRecord = driver as unknown as Record<string, unknown>;
      next.set(reference, Object.freeze({
        reference,
        value: driverRecord[field] ?? null,
        dataType: descriptor.dataType,
        state: driver.state,
        sourceTimestamp: driver.capturedAt ?? driver.stateChangedAt ?? null,
        observedAt,
        detail: driver.lastError ?? null
      }));
    }
  }
  return next;
}

export function applyMonitorRealtimeMessage(
  current: ReadonlyMap<string, MonitorSample>,
  message: RuntimeTagRealtimeMessage,
  selectedReferences: ReadonlySet<string>,
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>,
  observedAt = new Date().toISOString()
): ReadonlyMap<string, MonitorSample> {
  let next: Map<string, MonitorSample> | null = null;
  const messageTagId = normalizeIdentity(message.tag.id);

  for (const reference of selectedReferences) {
    const descriptor = descriptors.get(reference);
    if (!descriptor || (descriptor.family !== 'tag' && descriptor.family !== 'serverMemory')) continue;

    const descriptorTagId = descriptor.tagReference?.tagId;
    const matches = descriptorTagId
      ? normalizeIdentity(descriptorTagId) === messageTagId
      : reference === message.tag.path;
    if (!matches) continue;

    const projection = projectTagValue(descriptor, message.value);
    if (!next) next = new Map<string, MonitorSample>(current);
    next.set(reference, projection.ok ? Object.freeze({
      reference,
      value: projection.value,
      dataType: descriptor.dataType,
      quality: message.quality,
      sourceTimestamp: message.timestamp,
      observedAt
    }) : unavailableSample(descriptor, observedAt, projection.detail));
  }

  return next ?? current;
}

export function markMonitorUnavailable(
  current: ReadonlyMap<string, MonitorSample>,
  selected: readonly string[],
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>,
  observedAt = new Date().toISOString()
): ReadonlyMap<string, MonitorSample> {
  const next = new Map<string, MonitorSample>(current);
  for (const reference of selected) {
    const descriptor = descriptors.get(reference);
    if (descriptor) next.set(reference, unavailableSample(descriptor, observedAt));
  }
  return next;
}

export function formatMonitorValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '—';
  try { return JSON.stringify(value); } catch { return String(value); }
}

export function formatMonitorQuality(sample: MonitorSample | undefined): string {
  if (!sample) return 'Unavailable';
  if (sample.quality !== undefined && sample.quality !== null) return tagQualityLabel(sample.quality);
  if (sample.state !== undefined && sample.state !== null) return String(sample.state);
  return 'N/A';
}

export function monitorQualityClass(sample: MonitorSample | undefined): string {
  const value = formatMonitorQuality(sample).toLowerCase();
  return value.includes('good') || value.includes('available') || value.includes('connected') || value.includes('localsession') ? 'is-good'
    : value === 'n/a' ? '' : 'is-bad';
}

export function tagQualityLabel(value: string | number): string {
  if (typeof value === 'number') return TAG_QUALITY_NAMES[value] ?? `Unknown(${value})`;
  return value;
}

type TagValueProjection = Readonly<{
  ok: boolean;
  value?: unknown;
  detail?: string;
}>;

function projectSnapshotSample(
  descriptor: ProjectReferenceDescriptor,
  tag: RuntimeTagSnapshot | undefined,
  observedAt: string
): MonitorSample {
  const currentValue = tag?.current;
  if (!tag || !currentValue) return unavailableSample(descriptor, observedAt);

  const projection = projectTagValue(descriptor, currentValue.value);
  if (!projection.ok) return unavailableSample(descriptor, observedAt, projection.detail);

  return Object.freeze({
    reference: descriptor.reference,
    value: projection.value,
    dataType: descriptor.dataType,
    quality: currentValue.quality ?? null,
    sourceTimestamp: currentValue.sourceTimestamp ?? currentValue.serverTimestamp ?? currentValue.timestamp ?? null,
    observedAt
  });
}

function projectTagValue(descriptor: ProjectReferenceDescriptor, value: unknown): TagValueProjection {
  const selector = descriptor.tagReference?.selector;
  if (!selector) return Object.freeze({ ok: true, value });
  if (selector.kind !== 'bit' || !Number.isInteger(selector.index) || selector.index < 0) {
    return Object.freeze({ ok: false, detail: 'Invalid canonical TAG bit selector.' });
  }

  const integer = integerLikeToBigInt(value);
  if (integer === null) {
    return Object.freeze({
      ok: false,
      detail: 'The authoritative integer TAG value cannot be represented safely for bit projection.'
    });
  }

  return Object.freeze({
    ok: true,
    value: ((integer >> BigInt(selector.index)) & 1n) === 1n
  });
}

function integerLikeToBigInt(value: unknown): bigint | null {
  if (typeof value === 'bigint') return value;
  if (typeof value === 'number') {
    if (!Number.isFinite(value) || !Number.isInteger(value) || !Number.isSafeInteger(value)) return null;
    return BigInt(value);
  }
  if (typeof value === 'string' && /^[+-]?\d+$/.test(value.trim())) {
    try { return BigInt(value.trim()); } catch { return null; }
  }
  return null;
}

function normalizeIdentity(value: string): string {
  return value.trim().toLocaleLowerCase();
}

function unavailableSample(
  descriptor: ProjectReferenceDescriptor,
  observedAt: string,
  detail: string | null | undefined = null
): MonitorSample {
  return Object.freeze({
    reference: descriptor.reference,
    value: null,
    dataType: descriptor.dataType,
    state: 'Unavailable',
    sourceTimestamp: null,
    observedAt,
    ...(detail ? { detail } : {})
  });
}

function sample(reference: string, value: unknown, dataType: string, state: string, observedAt: string): MonitorSample {
  return Object.freeze({ reference, value, dataType, state, observedAt });
}
