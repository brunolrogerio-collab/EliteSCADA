import type {
  CommunicationDriverDiagnostic,
  GatewayRuntimeDiagnostic,
  OperationalTone,
  RuntimeDriverStatus,
  RuntimeOperationsSnapshot,
  RuntimeOperationsSummary
} from './operationsTypes';

const communicationNumericStates = ['Stopped', 'Starting', 'Healthy', 'Degraded', 'Reconnecting', 'Faulted', 'Stopping'];
const driverNumericStates = ['Stopped', 'Starting', 'Running', 'Faulted', 'Stopping'];

export function normalizeCommunicationState(state: string | number): string {
  if (typeof state === 'number') return communicationNumericStates[state] ?? String(state);
  return state;
}

export function normalizeDriverState(state: string | number): string {
  if (typeof state === 'number') return driverNumericStates[state] ?? String(state);
  return state;
}

export function communicationTone(item: CommunicationDriverDiagnostic): OperationalTone {
  switch (normalizeCommunicationState(item.state)) {
    case 'Healthy': return 'healthy';
    case 'Faulted': return 'danger';
    case 'Degraded':
    case 'Reconnecting':
    case 'Starting':
    case 'Stopping':
    case 'Stopped':
      return 'attention';
    default:
      return 'unknown';
  }
}

export function driverTone(driver: RuntimeDriverStatus): OperationalTone {
  switch (normalizeDriverState(driver.state)) {
    case 'Running': return 'healthy';
    case 'Faulted': return 'danger';
    case 'Starting':
    case 'Stopping':
    case 'Stopped':
      return 'attention';
    default:
      return 'unknown';
  }
}

export function gatewayTone(gateway: GatewayRuntimeDiagnostic): OperationalTone {
  switch (gateway.state) {
    case 'Running': return 'healthy';
    case 'WaitingForSource': return 'quiet';
    case 'Degraded': return 'attention';
    case 'Stopped': return 'quiet';
    default: return 'unknown';
  }
}

function maxTone(tones: OperationalTone[]): OperationalTone {
  if (tones.includes('danger')) return 'danger';
  if (tones.includes('attention')) return 'attention';
  if (tones.includes('healthy')) return 'healthy';
  if (tones.includes('quiet')) return 'quiet';
  return 'unknown';
}

export function buildRuntimeOperationsSummary(snapshot: RuntimeOperationsSnapshot): RuntimeOperationsSummary {
  const runtime = snapshot.diagnostics.available ? snapshot.diagnostics.value.runtime : null;
  const communications = runtime?.communicationDrivers ?? [];
  const gateways = snapshot.gateways.available ? snapshot.gateways.value : [];
  const alarms = snapshot.alarms.available ? snapshot.alarms.value : [];

  const runtimeTone = runtime
    ? maxTone(runtime.drivers.map(driverTone))
    : 'unknown';

  const communicationTones = communications.map(communicationTone);
  const communicationStateTone = communications.length === 0
    ? 'quiet'
    : maxTone(communicationTones);

  const gatewayTones = gateways.map(gatewayTone);
  const gatewayStateTone = gateways.length === 0
    ? 'quiet'
    : maxTone(gatewayTones);

  const alarmTone: OperationalTone = alarms.length > 0 ? 'attention' : 'quiet';

  const communicationGoodTags = communications.reduce((sum, item) => sum + item.tagQuality.good, 0);
  const communicationBadTags = communications.reduce((sum, item) =>
    sum
      + item.tagQuality.badCommunication
      + item.tagQuality.uncertain
      + item.tagQuality.bad
      + item.tagQuality.badConfiguration
      + item.tagQuality.badDevice
      + item.tagQuality.stale
      + item.tagQuality.disabled,
  0);
  const communicationNoSampleTags = communications.reduce((sum, item) => sum + item.tagQuality.noCurrentSample, 0);
  const communicationTagCount = communications.reduce((sum, item) => sum + item.associatedTagCount, 0);

  const degradedGateways = gateways.filter(item => item.state === 'Degraded').length;
  const runningGateways = gateways.filter(item => item.state === 'Running').length;
  const waitingGateways = gateways.filter(item => item.state === 'WaitingForSource').length;
  const gatewayWriteFailures = gateways.reduce((sum, item) => sum + item.writeFailureCount, 0);

  const overallInputs: OperationalTone[] = [runtimeTone, communicationStateTone, gatewayStateTone, alarmTone];
  if (!snapshot.diagnostics.available && !snapshot.gateways.available && !snapshot.alarms.available)
    overallInputs.push('unknown');

  return {
    overallTone: maxTone(overallInputs),
    runtimeTone,
    communicationTone: communicationStateTone,
    gatewayTone: gatewayStateTone,
    alarmTone,
    driverCount: runtime?.drivers.length ?? 0,
    communicationSourceCount: communications.length,
    healthyCommunicationSources: communicationTones.filter(tone => tone === 'healthy').length,
    attentionCommunicationSources: communicationTones.filter(tone => tone === 'attention').length,
    faultedCommunicationSources: communicationTones.filter(tone => tone === 'danger').length,
    communicationGoodTags,
    communicationBadTags,
    communicationNoSampleTags,
    communicationTagCount,
    activeAlarmCount: alarms.length,
    gatewayCount: gateways.length,
    runningGateways,
    waitingGateways,
    degradedGateways,
    gatewayWriteFailures
  };
}

export function sortCommunicationDiagnostics(items: CommunicationDriverDiagnostic[]): CommunicationDriverDiagnostic[] {
  const rank: Record<OperationalTone, number> = { danger: 0, attention: 1, unknown: 2, quiet: 3, healthy: 4 };
  return [...items].sort((left, right) => {
    const severity = rank[communicationTone(left)] - rank[communicationTone(right)];
    return severity !== 0 ? severity : left.dataSourceName.localeCompare(right.dataSourceName);
  });
}

export function sortGatewayDiagnostics(items: GatewayRuntimeDiagnostic[]): GatewayRuntimeDiagnostic[] {
  const rank: Record<OperationalTone, number> = { danger: 0, attention: 1, unknown: 2, quiet: 3, healthy: 4 };
  return [...items].sort((left, right) => {
    const severity = rank[gatewayTone(left)] - rank[gatewayTone(right)];
    return severity !== 0 ? severity : left.name.localeCompare(right.name);
  });
}
