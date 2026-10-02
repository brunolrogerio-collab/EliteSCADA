import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';

export const SIMULATION_DRIVER_TYPE = 'builtin.simulation';

const SIGNAL_TYPE_KEY = 'simulation.signalType';
const PARAMETER_KEYS = [
  'simulation.minimum',
  'simulation.maximum',
  'simulation.periodSeconds',
  'simulation.constantValue',
  'simulation.step'
] as const;

const NUMERIC_SIGNALS = [
  'Constant',
  'Random',
  'Sine',
  'Square',
  'RampUp',
  'RampDown',
  'RampUpDown',
  'Counter',
  'Manual'
] as const;

const BOOLEAN_SIGNALS = ['BooleanToggle', 'Constant', 'Manual'] as const;
const STRING_SIGNALS = ['Constant', 'Random', 'Sine', 'Square', 'RampUp', 'RampDown', 'RampUpDown', 'Counter', 'Manual'] as const;
const ENUM_SIGNALS = ['Constant', 'Counter', 'Manual'] as const;
const DATETIME_SIGNALS = ['CurrentTime'] as const;

const numericTypes = new Set(['int16', 'int32', 'int64', 'float', 'double']);

type SimulationParameterKey = typeof PARAMETER_KEYS[number];

export type SimulationFieldVisibility = Readonly<{
  minimum: boolean;
  maximum: boolean;
  periodSeconds: boolean;
  constantValue: boolean;
  step: boolean;
}>;

export function simulationSignalOptions(dataType: string): string[] {
  const normalized = dataType.trim().toLowerCase();
  if (normalized === 'datetime') return [...DATETIME_SIGNALS];
  if (normalized === 'boolean') return [...BOOLEAN_SIGNALS];
  if (normalized === 'string') return [...STRING_SIGNALS];
  if (normalized === 'enum') return [...ENUM_SIGNALS];
  if (numericTypes.has(normalized)) return [...NUMERIC_SIGNALS];
  return [...NUMERIC_SIGNALS];
}

export function simulationDefaultSignal(dataType: string): string {
  const normalized = dataType.trim().toLowerCase();
  if (normalized === 'datetime') return 'CurrentTime';
  if (normalized === 'boolean') return 'BooleanToggle';
  if (normalized === 'string') return 'Sine';
  if (normalized === 'enum') return 'Counter';
  return 'Sine';
}

export function simulationSignalForTag(tag: TagSourceAwareEngineering): string {
  const raw = tag.metadata?.[SIGNAL_TYPE_KEY]?.trim();
  return canonicalSignal(tag.dataType, raw) ?? simulationDefaultSignal(tag.dataType);
}

export function simulationFieldVisibility(signal: string): SimulationFieldVisibility {
  const range = signal === 'Random' || signal === 'Sine' || signal === 'Square' ||
    signal === 'RampUp' || signal === 'RampDown' || signal === 'RampUpDown' || signal === 'Counter';
  return {
    minimum: range,
    maximum: range,
    periodSeconds: signal === 'Sine' || signal === 'Square' || signal === 'RampUp' ||
      signal === 'RampDown' || signal === 'RampUpDown' || signal === 'BooleanToggle',
    constantValue: signal === 'Constant' || signal === 'Manual',
    step: signal === 'Counter'
  };
}

export function normalizeSimulationProfile(tag: TagSourceAwareEngineering): TagSourceAwareEngineering {
  const metadata = { ...(tag.metadata ?? {}) };
  const raw = metadata[SIGNAL_TYPE_KEY]?.trim();
  const signal = canonicalSignal(tag.dataType, raw) ?? simulationDefaultSignal(tag.dataType);

  if (raw && raw !== signal) metadata[SIGNAL_TYPE_KEY] = signal;
  pruneParameters(metadata, signal);

  const normalized = canonicalSimulationAuthority({ ...tag, metadata });
  if (
    sameMetadata(metadata, tag.metadata) &&
    tag.address == null &&
    tag.addressSelector == null &&
    tag.communicationBinding == null
  ) return tag;
  return normalized;
}

export function updateSimulationDataType(
  tag: TagSourceAwareEngineering,
  dataType: string
): TagSourceAwareEngineering {
  const current = tag.metadata?.[SIGNAL_TYPE_KEY]?.trim();
  const signal = canonicalSignal(dataType, current) ?? simulationDefaultSignal(dataType);
  const metadata = { ...(tag.metadata ?? {}), [SIGNAL_TYPE_KEY]: signal };
  pruneParameters(metadata, signal);
  return canonicalSimulationAuthority({ ...tag, dataType, metadata });
}

export function updateSimulationSignal(
  tag: TagSourceAwareEngineering,
  requestedSignal: string
): TagSourceAwareEngineering {
  const signal = canonicalSignal(tag.dataType, requestedSignal) ?? simulationDefaultSignal(tag.dataType);
  const metadata = { ...(tag.metadata ?? {}), [SIGNAL_TYPE_KEY]: signal };
  pruneParameters(metadata, signal);
  return canonicalSimulationAuthority({ ...tag, metadata });
}

export function updateSimulationMetadataNumber(
  tag: TagSourceAwareEngineering,
  key: SimulationParameterKey,
  value: number | null
): TagSourceAwareEngineering {
  const signal = simulationSignalForTag(tag);
  const metadata = { ...(tag.metadata ?? {}) };
  if (value === null) delete metadata[key];
  else metadata[key] = String(value);
  pruneParameters(metadata, signal);
  return canonicalSimulationAuthority({ ...tag, metadata });
}

function canonicalSignal(dataType: string, raw: string | undefined): string | null {
  if (!raw) return null;
  return simulationSignalOptions(dataType).find(option => option.toLowerCase() === raw.toLowerCase()) ?? null;
}

function pruneParameters(metadata: Record<string, string>, signal: string) {
  const visible = simulationFieldVisibility(signal);
  const keep = new Set<SimulationParameterKey>();
  if (visible.minimum) keep.add('simulation.minimum');
  if (visible.maximum) keep.add('simulation.maximum');
  if (visible.periodSeconds) keep.add('simulation.periodSeconds');
  if (visible.constantValue) keep.add('simulation.constantValue');
  if (visible.step) keep.add('simulation.step');

  for (const key of PARAMETER_KEYS) {
    if (!keep.has(key)) delete metadata[key];
  }
}

function canonicalSimulationAuthority(tag: TagSourceAwareEngineering): TagSourceAwareEngineering {
  return {
    ...tag,
    address: null,
    addressSelector: null,
    communicationBinding: null
  };
}

function sameMetadata(
  left: Readonly<Record<string, string>>,
  right: Readonly<Record<string, string>> | null | undefined
): boolean {
  const rightMap = right ?? {};
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(rightMap);
  if (leftKeys.length !== rightKeys.length) return false;
  return leftKeys.every(key => left[key] === rightMap[key]);
}
