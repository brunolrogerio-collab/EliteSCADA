import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const runtimeInstanceA = '11111111111111111111111111111111';
const runtimeInstanceB = '22222222222222222222222222222222';

test('Engineering diagnostics prioritizes communication health, filters sources and exposes technical drill-down', async ({ page }) => {
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
  await expect(page.getByText('Atenção', { exact: true })).toBeVisible();

  const sourceCards = page.locator('.eng-comm-source');
  await expect(sourceCards).toHaveCount(2);
  await expect(sourceCards.nth(0)).toContainText('PLC B');
  await expect(sourceCards.nth(0).locator('.eng-comm-status')).toHaveText(/Reconectando/);
  await expect(sourceCards.nth(1)).toContainText('PLC A');
  await expect(sourceCards.nth(1).locator('.eng-comm-status')).toHaveText(/Saudável/);

  await page.getByRole('button', { name: /PLC B/ }).click();
  await expect(page.getByText(runtimeInstanceB, { exact: true })).toBeVisible();
  await expect(page.getByText('request timed out', { exact: true })).toBeVisible();
  await expect(page.locator('.eng-comm-quality-item.badcomm')).toContainText('1');
  await expect(page.locator('.eng-comm-quality-item.badcomm')).toContainText('BadCommunication');
  await expect(page.getByText('10.0.0.2', { exact: true })).toBeVisible();
  await expect(page.getByText('100 ms', { exact: true })).toBeVisible();

  await page.getByLabel('Filtro').selectOption('attention');
  await expect(sourceCards).toHaveCount(1);
  await expect(sourceCards.nth(0)).toContainText('PLC B');
  await expect(page.getByRole('heading', { name: 'PLC B' })).toBeVisible();

  await page.getByLabel('Filtro').selectOption('all');
  await page.getByLabel('Buscar Data Source, driver ou endpoint').fill('plc.a');
  await expect(sourceCards).toHaveCount(1);
  await expect(sourceCards.nth(0)).toContainText('PLC A');
  await expect(page.getByRole('heading', { name: 'PLC A' })).toBeVisible();

  await page.getByLabel('Idioma').selectOption('en');
  await expect(page.getByRole('heading', { name: 'Active communication' })).toBeVisible();
  await expect(page.locator('.eng-comm-source').locator('.eng-comm-status')).toHaveText(/Healthy/);
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
