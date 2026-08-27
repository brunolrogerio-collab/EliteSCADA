export type RuntimeOperationsLocale = 'pt-BR' | 'en' | 'es';

export type RuntimeDriverStatus = {
  driverId: string;
  name: string;
  state: string | number;
  timestamp: string;
  message?: string | null;
  updatesPublished: number;
};

export type CommunicationDriverCounters = {
  cycles: number;
  requests: number;
  successfulOperations: number;
  failedOperations: number;
  consecutiveFailures: number;
  timeouts: number;
  connections: number;
  disconnections: number;
  reconnects: number;
  readOperations: number;
  writeOperations: number;
  updatesPublished: number;
};

export type CommunicationTagQualitySummary = {
  good: number;
  badCommunication: number;
  uncertain: number;
  bad: number;
  badConfiguration: number;
  badDevice: number;
  stale: number;
  disabled: number;
  noCurrentSample: number;
  total: number;
};

export type CommunicationDriverDiagnostic = {
  dataSourceKey: string;
  dataSourceName: string;
  driverType: string;
  runtimeInstanceId: string;
  endpoint?: string | null;
  state: string | number;
  stateChangedAt: string;
  capturedAt: string;
  lastSuccessfulCommunicationAt?: string | null;
  lastFailedCommunicationAt?: string | null;
  lastError?: string | null;
  dataAge?: string | null;
  configuredScanInterval?: string | null;
  lastOperationDuration?: string | null;
  averageOperationDuration?: string | null;
  lastScanDuration?: string | null;
  recentFailureRate: number;
  associatedTagCount: number;
  tagQuality: CommunicationTagQualitySummary;
  counters: CommunicationDriverCounters;
  protocolDetails?: Record<string, string> | null;
};

export type RuntimeDescriptor = {
  mode: string;
  projectKey?: string | null;
  revision?: number | null;
  activatedAtUtc?: string | null;
  drivers: RuntimeDriverStatus[];
  communicationDrivers: CommunicationDriverDiagnostic[];
  tagCount: number;
  activeAlarmCount: number;
};

export type HistorianRuntimeDiagnostic = {
  provider: string;
  writtenSamples: number;
  pendingSamples: number;
};

export type RuntimeDiagnosticsPayload = {
  driver?: RuntimeDriverStatus | null;
  runtime: RuntimeDescriptor;
  historian: HistorianRuntimeDiagnostic;
  activeAlarms: number;
};

export type GatewayRuntimeDiagnostic = {
  routeId: string;
  key: string;
  name: string;
  enabled: boolean;
  state: string;
  sourceTagId: string;
  sourceTagPath: string;
  sourceDataSource?: string | null;
  destinationTagId: string;
  destinationTagPath: string;
  destinationDataSource?: string | null;
  lastSourceUpdateAtUtc?: string | null;
  lastSuccessfulTransferAtUtc?: string | null;
  lastFailedTransferAtUtc?: string | null;
  transferCount: number;
  skippedTransferCount: number;
  coalescedUpdateCount: number;
  writeFailureCount: number;
  consecutiveFailures: number;
  lastError?: string | null;
  hasPendingValue: boolean;
  transferMode: string;
  effectiveIntervalMilliseconds?: number | null;
};

export type RuntimeAlarm = {
  definitionId: string;
  name: string;
  tagId: string;
  type: string;
  priority: string;
  state: string;
  lastTransition: string;
  lastValue: unknown;
  area?: string | null;
  message?: string | null;
  acknowledgedBy?: string | null;
};

export type RuntimeOperationsEndpoint<T> =
  | { available: true; value: T }
  | { available: false; status?: number; error: string };

export type RuntimeOperationsSnapshot = {
  capturedAt: string;
  diagnostics: RuntimeOperationsEndpoint<RuntimeDiagnosticsPayload>;
  gateways: RuntimeOperationsEndpoint<GatewayRuntimeDiagnostic[]>;
  alarms: RuntimeOperationsEndpoint<RuntimeAlarm[]>;
};

export type OperationalTone = 'healthy' | 'attention' | 'danger' | 'quiet' | 'unknown';

export type RuntimeOperationsSummary = {
  overallTone: OperationalTone;
  runtimeTone: OperationalTone;
  communicationTone: OperationalTone;
  gatewayTone: OperationalTone;
  alarmTone: OperationalTone;
  driverCount: number;
  communicationSourceCount: number;
  healthyCommunicationSources: number;
  attentionCommunicationSources: number;
  faultedCommunicationSources: number;
  communicationGoodTags: number;
  communicationBadTags: number;
  communicationNoSampleTags: number;
  communicationTagCount: number;
  activeAlarmCount: number;
  gatewayCount: number;
  runningGateways: number;
  waitingGateways: number;
  degradedGateways: number;
  gatewayWriteFailures: number;
};
