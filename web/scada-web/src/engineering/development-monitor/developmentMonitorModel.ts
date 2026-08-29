import type { CommunicationDriverDiagnostic } from '../types';
import type {
  ClientMemoryDefinitionView,
  ProjectReferenceDescriptor
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

export function resolveMonitorQuickAdd(
  catalog: readonly ProjectReferenceDescriptor[],
  rawReference: string
): MonitorQuickAddResult {
  const candidate = rawReference.trim();
  if (!candidate) return Object.freeze({ status: 'notFound' });
  const exactReference = catalog.filter(item => item.reference === candidate);
  if (exactReference.length === 1) return Object.freeze({ status: 'found', reference: exactReference[0].reference });
  if (exactReference.length > 1) return Object.freeze({ status: 'ambiguous' });

  const exactLabel = catalog.filter(item => item.label === candidate);
  if (exactLabel.length === 1) return Object.freeze({ status: 'found', reference: exactLabel[0].reference });
  if (exactLabel.length > 1) return Object.freeze({ status: 'ambiguous' });
  return Object.freeze({ status: 'notFound' });
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
  const next = new Map(current);
  const tagByPath = new Map(tags.map(tag => [tag.path, tag]));
  const driverByKey = new Map(drivers.map(driver => [driver.dataSourceKey, driver]));

  for (const reference of selected) {
    const descriptor = descriptors.get(reference);
    if (!descriptor) continue;

    if (descriptor.family === 'tag' || descriptor.family === 'serverMemory') {
      const tag = tagByPath.get(reference);
      const currentValue = tag?.current;
      next.set(reference, tag && currentValue ? Object.freeze({
        reference,
        value: currentValue.value,
        dataType: tag.dataType || descriptor.dataType,
        quality: currentValue.quality ?? null,
        sourceTimestamp: currentValue.sourceTimestamp ?? currentValue.serverTimestamp ?? currentValue.timestamp ?? null,
        observedAt
      }) : unavailableSample(descriptor, observedAt));
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
  const reference = message.tag.path;
  if (!selectedReferences.has(reference)) return current;
  const descriptor = descriptors.get(reference);
  if (!descriptor || (descriptor.family !== 'tag' && descriptor.family !== 'serverMemory')) return current;

  const next = new Map(current);
  next.set(reference, Object.freeze({
    reference,
    value: message.value,
    dataType: descriptor.dataType,
    quality: message.quality,
    sourceTimestamp: message.timestamp,
    observedAt
  }));
  return next;
}

export function markMonitorUnavailable(
  current: ReadonlyMap<string, MonitorSample>,
  selected: readonly string[],
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>,
  observedAt = new Date().toISOString()
): ReadonlyMap<string, MonitorSample> {
  const next = new Map(current);
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
  if (sample.quality !== undefined && sample.quality !== null) {
    if (sample.quality === 0) return 'Good';
    return String(sample.quality);
  }
  if (sample.state !== undefined && sample.state !== null) return String(sample.state);
  return 'N/A';
}

export function monitorQualityClass(sample: MonitorSample | undefined): string {
  const value = formatMonitorQuality(sample).toLowerCase();
  return value.includes('good') || value.includes('available') || value.includes('connected') || value.includes('localsession') ? 'is-good'
    : value === 'n/a' ? '' : 'is-bad';
}

function unavailableSample(descriptor: ProjectReferenceDescriptor, observedAt: string): MonitorSample {
  return Object.freeze({
    reference: descriptor.reference,
    value: null,
    dataType: descriptor.dataType,
    state: 'Unavailable',
    sourceTimestamp: null,
    observedAt
  });
}

function sample(reference: string, value: unknown, dataType: string, state: string, observedAt: string): MonitorSample {
  return Object.freeze({ reference, value, dataType, state, observedAt });
}
