import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const runtimeInstanceA = '11111111111111111111111111111111';
const runtimeInstanceB = '22222222222222222222222222222222';

test('Engineering diagnostics renders protected per-Data-Source communication state and drill-down', async ({ page }) => {
  await page.route('**/api/diagnostics/runtime', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        runtime: {
          communicationDrivers: [
            diagnostic('plc.a', 'PLC A', runtimeInstanceA, 2, 0, null),
            diagnostic('plc.b', 'PLC B', runtimeInstanceB, 4, 3, 'request timed out')
          ]
        }
      })
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Diagnósticos/ }).click();

  await expect(page.getByRole('heading', { name: 'Comunicação ativa' })).toBeVisible();
  await expect(page.getByText('PLC A', { exact: true })).toBeVisible();
  await expect(page.getByText('PLC B', { exact: true })).toBeVisible();
  await expect(page.getByText('Healthy', { exact: true })).toBeVisible();
  await expect(page.getByText('Reconnecting', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: /PLC B/ }).click();
  await expect(page.getByText(runtimeInstanceB, { exact: true })).toBeVisible();
  await expect(page.getByText('request timed out', { exact: true })).toBeVisible();
  await expect(page.getByText('Good 0 · BadComm 1 · Sem amostra 0', { exact: true })).toBeVisible();

  await page.getByLabel('Idioma').selectOption('en');
  await expect(page.getByRole('heading', { name: 'Active communication' })).toBeVisible();
  await expect(page.getByText('Good 0 · BadComm 1 · No sample 0', { exact: true })).toBeVisible();
});

function diagnostic(
  key: string,
  name: string,
  runtimeInstanceId: string,
  state: number,
  timeouts: number,
  lastError: string | null
) {
  return {
    dataSourceKey: key,
    dataSourceName: name,
    driverType: 'modbus.tcp',
    runtimeInstanceId,
    endpoint: key === 'plc.a' ? '10.0.0.1:502' : '10.0.0.2:502',
    state,
    stateChangedAt: '2026-08-27T11:00:00Z',
    capturedAt: '2026-08-27T11:00:05Z',
    lastSuccessfulCommunicationAt: '2026-08-27T11:00:04Z',
    lastFailedCommunicationAt: lastError ? '2026-08-27T11:00:05Z' : null,
    lastError,
    dataAge: '00:00:01',
    configuredScanInterval: '00:00:00.1000000',
    lastOperationDuration: '00:00:00.0150000',
    averageOperationDuration: '00:00:00.0120000',
    lastScanDuration: '00:00:00.0180000',
    recentFailureRate: lastError ? 0.25 : 0,
    associatedTagCount: 1,
    tagQuality: {
      good: lastError ? 0 : 1,
      badCommunication: lastError ? 1 : 0,
      uncertain: 0,
      bad: 0,
      badConfiguration: 0,
      badDevice: 0,
      stale: 0,
      disabled: 0,
      noCurrentSample: 0,
      total: 1
    },
    counters: {
      cycles: 20,
      requests: 22,
      successfulOperations: lastError ? 17 : 20,
      failedOperations: lastError ? 3 : 0,
      consecutiveFailures: lastError ? 3 : 0,
      timeouts,
      connections: lastError ? 2 : 1,
      disconnections: lastError ? 1 : 0,
      reconnects: lastError ? 1 : 0,
      readOperations: 20,
      writeOperations: 0,
      updatesPublished: lastError ? 17 : 20
    },
    protocolDetails: {
      host: key === 'plc.a' ? '10.0.0.1' : '10.0.0.2',
      port: '502'
    }
  };
}
