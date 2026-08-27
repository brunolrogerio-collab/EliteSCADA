import { expect, test } from '@playwright/test';
import {
  buildRuntimeOperationsSummary,
  communicationTone,
  gatewayTone,
  normalizeCommunicationState,
  normalizeDriverState,
  sortCommunicationDiagnostics
} from '../src/runtime/operationsModel';
import type {
  CommunicationDriverDiagnostic,
  GatewayRuntimeDiagnostic,
  RuntimeOperationsSnapshot
} from '../src/runtime/operationsTypes';

function communication(
  key: string,
  state: string | number,
  good: number,
  badCommunication: number,
  failedOperations = 0
): CommunicationDriverDiagnostic {
  return {
    dataSourceKey: key,
    dataSourceName: key.toUpperCase(),
    driverType: 'modbus.tcp',
    runtimeInstanceId: `instance-${key}`,
    endpoint: `127.0.0.1:${key === 'a' ? 1502 : 2502}`,
    state,
    stateChangedAt: '2026-08-27T15:00:00Z',
    capturedAt: '2026-08-27T15:01:00Z',
    lastSuccessfulCommunicationAt: '2026-08-27T15:00:59Z',
    lastFailedCommunicationAt: failedOperations > 0 ? '2026-08-27T15:00:58Z' : null,
    lastError: failedOperations > 0 ? 'TimeoutException: request timed out' : null,
    dataAge: '00:00:01',
    configuredScanInterval: '00:00:00.0500000',
    lastOperationDuration: '00:00:00.0020000',
    averageOperationDuration: '00:00:00.0030000',
    lastScanDuration: '00:00:00.0100000',
    recentFailureRate: failedOperations > 0 ? 0.2 : 0,
    associatedTagCount: good + badCommunication,
    tagQuality: {
      good,
      badCommunication,
      uncertain: 0,
      bad: 0,
      badConfiguration: 0,
      badDevice: 0,
      stale: 0,
      disabled: 0,
      noCurrentSample: 0,
      total: good + badCommunication
    },
    counters: {
      cycles: 10,
      requests: 10,
      successfulOperations: 10 - failedOperations,
      failedOperations,
      consecutiveFailures: failedOperations > 0 ? 1 : 0,
      timeouts: failedOperations,
      connections: 1,
      disconnections: failedOperations > 0 ? 1 : 0,
      reconnects: failedOperations > 0 ? 1 : 0,
      readOperations: 10,
      writeOperations: 0,
      updatesPublished: good * 10
    }
  };
}

function gateway(state: string, writeFailureCount = 0): GatewayRuntimeDiagnostic {
  return {
    routeId: `route-${state}`,
    key: `route-${state}`,
    name: `Route ${state}`,
    enabled: true,
    state,
    sourceTagId: '11111111-1111-1111-1111-111111111111',
    sourceTagPath: 'PLC_A.Source',
    sourceDataSource: 'plc.a',
    destinationTagId: '22222222-2222-2222-2222-222222222222',
    destinationTagPath: 'PLC_B.Destination',
    destinationDataSource: 'plc.b',
    transferCount: 5,
    skippedTransferCount: 0,
    coalescedUpdateCount: 0,
    writeFailureCount,
    consecutiveFailures: writeFailureCount > 0 ? 1 : 0,
    hasPendingValue: false,
    transferMode: 'OnChange'
  };
}

function snapshot(communications: CommunicationDriverDiagnostic[]): RuntimeOperationsSnapshot {
  return {
    capturedAt: '2026-08-27T15:01:00Z',
    diagnostics: {
      available: true,
      value: {
        runtime: {
          mode: 'engineering',
          projectKey: 'plant-a',
          revision: 7,
          activatedAtUtc: '2026-08-27T14:00:00Z',
          drivers: [{
            driverId: 'plc.a',
            name: 'PLC A',
            state: 'Running',
            timestamp: '2026-08-27T15:01:00Z',
            updatesPublished: 10
          }],
          communicationDrivers: communications,
          tagCount: communications.reduce((sum, item) => sum + item.associatedTagCount, 0),
          activeAlarmCount: 0
        },
        historian: { provider: 'timescaledb', writtenSamples: 50, pendingSamples: 0 },
        activeAlarms: 0
      }
    },
    gateways: { available: true, value: [gateway('Running')] },
    alarms: { available: true, value: [] }
  };
}

test('Runtime operations keeps healthy communication visually quiet and aggregates TAG quality', () => {
  const result = buildRuntimeOperationsSummary(snapshot([
    communication('a', 'Healthy', 4, 0),
    communication('b', 2, 3, 0)
  ]));

  expect(normalizeCommunicationState(2)).toBe('Healthy');
  expect(normalizeDriverState(2)).toBe('Running');
  expect(result.overallTone).toBe('healthy');
  expect(result.communicationTone).toBe('healthy');
  expect(result.communicationSourceCount).toBe(2);
  expect(result.healthyCommunicationSources).toBe(2);
  expect(result.communicationGoodTags).toBe(7);
  expect(result.communicationBadTags).toBe(0);
});

test('One failed Data Source is isolated and drives attention without contaminating the healthy peer', () => {
  const healthy = communication('a', 'Healthy', 4, 0);
  const failed = communication('b', 'Faulted', 0, 3, 2);
  const result = buildRuntimeOperationsSummary(snapshot([healthy, failed]));
  const sorted = sortCommunicationDiagnostics([healthy, failed]);

  expect(communicationTone(healthy)).toBe('healthy');
  expect(communicationTone(failed)).toBe('danger');
  expect(result.faultedCommunicationSources).toBe(1);
  expect(result.healthyCommunicationSources).toBe(1);
  expect(result.communicationBadTags).toBe(3);
  expect(result.overallTone).toBe('danger');
  expect(sorted.map(item => item.dataSourceKey)).toEqual(['b', 'a']);
});

test('Simulation runtime with no external Data Source does not fabricate communication failure', () => {
  const value = snapshot([]);
  if (!value.diagnostics.available) throw new Error('fixture');
  value.diagnostics.value.runtime.mode = 'simulation';
  value.diagnostics.value.runtime.projectKey = null;
  value.diagnostics.value.runtime.revision = null;
  value.gateways = { available: true, value: [] };

  const result = buildRuntimeOperationsSummary(value);

  expect(result.runtimeTone).toBe('healthy');
  expect(result.communicationSourceCount).toBe(0);
  expect(result.communicationTone).toBe('quiet');
  expect(result.overallTone).toBe('healthy');
});

test('Waiting Gateway is neutral while degraded Gateway remains operational attention', () => {
  expect(gatewayTone(gateway('WaitingForSource'))).toBe('quiet');
  expect(gatewayTone(gateway('Running'))).toBe('healthy');
  expect(gatewayTone(gateway('Degraded', 2))).toBe('attention');

  const value = snapshot([communication('a', 'Healthy', 1, 0)]);
  value.gateways = { available: true, value: [gateway('Degraded', 2)] };
  const result = buildRuntimeOperationsSummary(value);

  expect(result.degradedGateways).toBe(1);
  expect(result.gatewayWriteFailures).toBe(2);
  expect(result.overallTone).toBe('attention');
});

test('Endpoint authorization failure stays unknown instead of becoming fake device failure', () => {
  const value: RuntimeOperationsSnapshot = {
    capturedAt: '2026-08-27T15:01:00Z',
    diagnostics: { available: false, status: 403, error: '403 Forbidden' },
    gateways: { available: false, status: 403, error: '403 Forbidden' },
    alarms: { available: true, value: [] }
  };

  const result = buildRuntimeOperationsSummary(value);

  expect(result.runtimeTone).toBe('unknown');
  expect(result.communicationTone).toBe('quiet');
  expect(result.faultedCommunicationSources).toBe(0);
  expect(result.overallTone).not.toBe('danger');
});
