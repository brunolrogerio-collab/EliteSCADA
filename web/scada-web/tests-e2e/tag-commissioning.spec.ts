import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('TAG commissioning tests a Modbus draft read without applying or mutating Active state', async ({ page }) => {
  let pointReadBody: any = null;
  let forbiddenMutationCount = 0;

  page.on('request', request => {
    const url = request.url();
    if (request.method() !== 'GET' &&
        !url.includes('/driver-tools/point-read-test') &&
        (url.includes('/apply') || url.includes('/activate') || url.includes('/api/tags') || url.includes('/historian'))) {
      forbiddenMutationCount += 1;
    }
  });

  await page.route('**/api/engineering/workspace', async route => {
    const response = await route.fetch();
    const snapshot = await response.json();
    snapshot.package.dataSources = [{
      id: '00000000-0000-0000-0000-00000000c390',
      key: 'modbus.commissioning',
      name: 'Modbus commissioning fixture',
      driver: 'modbus.tcp',
      enabled: true,
      settings: { host: '127.0.0.1', port: '1502', unitId: '1' },
      secretReferences: {}
    }];
    snapshot.package.tags = [{
      id: '00000000-0000-0000-0000-00000000c391',
      name: 'Commissioning Pressure',
      path: 'Commissioning.Pressure',
      dataType: 'float',
      source: 'modbus.commissioning',
      dataSourceId: '00000000-0000-0000-0000-00000000c390',
      address: 'holding:10',
      engineeringUnit: 'bar',
      readOnly: true,
      metadata: {},
      communicationBinding: {
        contractVersion: 1,
        schemaId: 'modbus.tcp.engineering',
        schemaVersion: 1,
        portableAddress: 'holding:10',
        settings: { 'modbus.unitId': '1', 'modbus.valueType': 'Float32', 'modbus.scale': '2', 'modbus.offset': '3' },
        valueTransform: { contractVersion: 1, byteSwap: false, wordSwap: false }
      }
    }];
    await route.fulfill({ response, json: snapshot });
  });

  await page.route('**/api/engineering/driver-tools/point-read-test', async route => {
    pointReadBody = route.request().postDataJSON();
    const monitor = pointReadBody.sampleCount === 5;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: monitor ? 'IntermittentOrUncertain' : 'Good',
        sanitizedEndpoint: '127.0.0.1:1502',
        portableAddress: 'holding:10',
        summary: {
          requestedSamples: monitor ? 5 : 1,
          completedSamples: monitor ? 5 : 1,
          goodSamples: monitor ? 4 : 1,
          uncertainSamples: monitor ? 1 : 0,
          badSamples: 0,
          noDataSamples: 0,
          minimumLatencyMilliseconds: 2,
          averageLatencyMilliseconds: 3,
          maximumLatencyMilliseconds: 5
        },
        samples: [{
          status: monitor ? 'IntermittentOrUncertain' : 'Good',
          observedAtUtc: '2026-09-29T20:00:00Z',
          sourceTimestampUtc: null,
          latencyMilliseconds: 3,
          quality: monitor ? 'Uncertain' : 'Good',
          raw: { kind: 'registers', hex: '3F800000', elements: ['0x3F80', '0x0000'], metadata: { byteSwap: String(Boolean(pointReadBody.binding.valueTransform?.byteSwap)), wordSwap: String(Boolean(pointReadBody.binding.valueTransform?.wordSwap)) } },
          decoded: { valueType: 'Float32', value: 1 },
          engineering: { valueType: 'Float', value: 5, engineeringUnit: 'bar' },
          effectiveValueTransform: pointReadBody.binding.valueTransform
        }],
        issues: []
      })
    });
  });

  await page.goto('/engineering/tags');
  await expect(page.getByTestId('tag-commissioning')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Testar leitura' })).toBeVisible();
  await page.getByTestId('tag-commissioning-byte-swap').check();
  await page.getByRole('button', { name: 'Testar leitura' }).click();

  await expect(page.getByTestId('tag-test-read-state')).toContainText('GOOD');
  await expect(page.getByTestId('tag-test-read-result')).toContainText('3F800000');
  await expect(page.getByTestId('tag-test-read-result')).toContainText('5');
  expect(pointReadBody.binding.valueTransform.byteSwap).toBe(true);
  expect(pointReadBody.sampleCount).toBe(1);
  expect(forbiddenMutationCount).toBe(0);

  await page.getByTestId('tag-short-monitor').click();
  await expect(page.getByTestId('tag-test-read-state')).toContainText('INTERMITTENT_OR_UNCERTAIN');
  expect(pointReadBody.sampleCount).toBe(5);
  expect(pointReadBody.sampleIntervalMilliseconds).toBe(500);
  await expect(page.getByTestId('tag-development-monitor-handoff')).toHaveAttribute('href', '/engineering/monitor');
  expect(forbiddenMutationCount).toBe(0);
});
