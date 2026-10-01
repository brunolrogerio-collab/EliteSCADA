import { expect, test } from '@playwright/test';

test('Gateway Engineering config uses canonical Preview Apply and shows runtime diagnostics', async ({ page }) => {
  const sourceId = '71000000-0000-0000-0000-000000000001';
  const destinationId = '71000000-0000-0000-0000-000000000002';
  const clientId = '71000000-0000-0000-0000-000000000003';
  const fanoutId = '71000000-0000-0000-0000-000000000004';
  const workspace = {
    projectKey: 'gateway-ui-e2e', projectName: 'Gateway UI E2E', baseRevision: 1,
    checkedOutAtUtc: new Date().toISOString(), lastSavedAtUtc: new Date().toISOString(),
    isDirty: false, changeVersion: 9, tagCount: 4, alarmCount: 0, dataSourceCount: 3,
    templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0, securityRoleCount: 0
  };
  const engineering = {
    schema: 'scada.engineering', schemaVersion: 9, exportedAt: new Date().toISOString(),
    tags: [
      { id: sourceId, name: 'Source', path: 'Server.Source', dataType: 'int16', source: 'memory.server.e2e', readOnly: false },
      { id: destinationId, name: 'Destination', path: 'PLC.Destination', dataType: 'int16', source: 'plc.e2e', address: 'holding:10', readOnly: false },
      { id: clientId, name: 'Local', path: 'UI.Local', dataType: 'int16', source: 'memory.client.e2e', readOnly: false },
      { id: fanoutId, name: 'Fanout', path: 'PLC.Fanout', dataType: 'int16', source: 'plc.e2e', address: 'holding:11', readOnly: false }
    ],
    alarms: [],
    dataSources: [
      { id: '72000000-0000-0000-0000-000000000001', key: 'memory.server.e2e', name: 'Server Memory', driver: 'builtin.memory.server', enabled: true },
      { id: '72000000-0000-0000-0000-000000000002', key: 'plc.e2e', name: 'PLC', driver: 'modbus.tcp', enabled: true, settings: { host: '127.0.0.1' } },
      { id: '72000000-0000-0000-0000-000000000003', key: 'memory.client.e2e', name: 'Client Memory', driver: 'builtin.memory.client', enabled: true }
    ],
    templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [], commands: [], gateways: [{
      id: '73000000-0000-0000-0000-000000000010', key: 'route-a', name: 'Route A', enabled: true,
      sourceTagId: sourceId, sourceTagPath: 'Server.Source', destinationTagId: destinationId, destinationTagPath: 'PLC.Destination',
      transferMode: 'onChange', qualityPolicy: 'goodOnly', conversionPolicy: 'exact',
      initialTransferPolicy: 'synchronizeFirstAcceptableValue', minimumIntervalMilliseconds: 100
    }]
  };

  let previewBody: any = null;
  let applyBody: any = null;
  let applyVersion: string | null = null;

  await page.route('**/api/engineering/workspace', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(workspace) }));
  await page.route('**/api/engineering/export/json', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(engineering) }));
  await page.route('**/api/gateway/diagnostics', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify([{
      routeId: '73000000-0000-0000-0000-000000000001', key: 'active.route', name: 'Active route', enabled: true,
      state: 'Running', sourceTagId: sourceId, sourceTagPath: 'Server.Source', sourceDataSource: 'memory.server.e2e',
      destinationTagId: destinationId, destinationTagPath: 'PLC.Destination', destinationDataSource: 'plc.e2e',
      lastSourceUpdateAtUtc: new Date().toISOString(), lastSuccessfulTransferAtUtc: new Date().toISOString(), lastFailedTransferAtUtc: null,
      transferCount: 12, skippedTransferCount: 3, coalescedUpdateCount: 2, writeFailureCount: 1, consecutiveFailures: 0,
      lastError: null, hasPendingValue: false, transferMode: 'OnChange', effectiveIntervalMilliseconds: 100
    }])
  }));
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewBody = route.request().postDataJSON();
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      mode: 'CreateAndUpdate', createCount: 1, updateCount: 0, skipCount: 0, errorCount: 0,
      items: [], canApply: true
    }) });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    applyBody = route.request().postDataJSON();
    applyVersion = route.request().headers()['x-elitescada-workspace-version'] ?? null;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      mode: 'CreateAndUpdate', created: 1, updated: 0, skipped: 0, issues: []
    }) });
  });

  await page.goto('/engineering');
  const engineeringNavigation = page.getByRole('complementary', { name: 'Engineering' });
  const dataSourcesNav = engineeringNavigation.getByRole('button', { name: /Fontes de dados|Fuentes de datos|Data Sources/ });
  const gatewayNav = engineeringNavigation.getByRole('button', { name: /TAG Gateway/ });
  await expect(gatewayNav).toContainText('1');
  await dataSourcesNav.click();
  await expect(page.getByTestId('gateway-engineering-panel')).toHaveCount(0);
  await gatewayNav.click();

  const panel = page.getByTestId('gateway-engineering-panel');
  await expect(panel).toBeVisible();
  await expect(page.getByTestId('gateway-diagnostics')).toContainText('active.route');
  await expect(page.getByTestId('gateway-diagnostics')).toContainText('Running');
  await expect(page.getByTestId('gateway-route-inventory')).toContainText('route-a');
  await expect(page.getByTestId('gateway-route-count')).toContainText('1');
  await page.getByTestId('gateway-new-route').click();

  const source = page.getByTestId('gateway-source');
  const destination = page.getByTestId('gateway-destination');
  await expect(source.locator('option')).toHaveCount(4);
  await expect(source).not.toContainText('UI.Local');
  await source.selectOption(sourceId);
  await destination.selectOption(fanoutId);

  await page.getByTestId('gateway-key').fill('server-to-plc');
  await panel.locator('.eng-mutation-card').first().locator('input').nth(1).fill('Server to PLC');
  await page.getByTestId('gateway-mode').selectOption('periodic');
  await page.getByTestId('gateway-period').fill('250');
  await page.getByTestId('gateway-preview').click();

  await expect.poll(() => previewBody?.gateways?.length ?? 0).toBe(2);
  expect(previewBody.gateways[0].key).toBe('route-a');
  const createdRoute = previewBody.gateways.find((route: any) => route.key === 'server-to-plc');
  expect(createdRoute).toBeTruthy();
  expect(createdRoute.sourceTagId).toBe(sourceId);
  expect(createdRoute.sourceTagPath).toBe('Server.Source');
  expect(createdRoute.destinationTagId).toBe(fanoutId);
  expect(createdRoute.destinationTagPath).toBe('PLC.Fanout');
  expect(createdRoute.transferMode).toBe('periodic');
  expect(createdRoute.periodMilliseconds).toBe(250);
  expect(createdRoute.qualityPolicy).toBe('goodOnly');
  expect(createdRoute.id).toMatch(/[0-9a-f-]{36}/i);

  await expect(page.getByTestId('gateway-preview-result')).toContainText(/valid|válido|válida/i);
  await expect(page.getByTestId('gateway-apply')).toBeEnabled();
  await page.getByTestId('gateway-apply').click();

  await expect.poll(() => applyBody?.gateways?.length ?? 0).toBe(2);
  expect(applyBody.gateways.map((route: any) => route.key)).toEqual(['route-a', 'server-to-plc']);
  expect(applyVersion).toBe('9');
});
