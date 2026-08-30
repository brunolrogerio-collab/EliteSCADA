import { expect, test, type Page, type Route } from '@playwright/test';

const harnessPath = '/tests-e2e/historical-browser-harness.html';

function historicalResponse(datasetKey: 'historian.samples' | 'alarm.events', options: {
  value?: string;
  nextCursor?: string | null;
  state?: string;
} = {}) {
  if (datasetKey === 'historian.samples') {
    return {
      version: 1,
      datasetKey,
      columns: [
        { field: 'tag.id', type: 'guid', filterable: true, sortable: false, searchable: false },
        { field: 'tag.path', type: 'string', filterable: true, sortable: false, searchable: true },
        { field: 'quality', type: 'enum', filterable: true, sortable: false, searchable: false },
        { field: 'value', type: 'scalar', filterable: true, sortable: false, searchable: false },
        { field: 'timestamp', type: 'dateTime', filterable: true, sortable: true, searchable: false }
      ],
      rows: [{
        cells: {
          'tag.id': { kind: 'guid', value: '11111111-1111-1111-1111-111111111111' },
          'tag.path': { kind: 'string', value: 'Demo.Flow' },
          quality: { kind: 'enum', value: 'Good' },
          value: { kind: 'int64', value: options.value ?? '9223372036854775807' },
          timestamp: { kind: 'dateTime', value: '2026-08-30T01:00:00Z' }
        }
      }],
      fromUtc: '2026-08-30T00:00:00Z',
      toUtc: '2026-08-30T01:00:00Z',
      nextCursor: options.nextCursor ?? null,
      pageSize: 100
    };
  }

  return {
    version: 1,
    datasetKey,
    columns: [
      { field: 'alarm.id', type: 'guid', filterable: true, sortable: false, searchable: false },
      { field: 'tag.path', type: 'string', filterable: true, sortable: true, searchable: true },
      { field: 'state', type: 'enum', filterable: true, sortable: true, searchable: false },
      { field: 'priority', type: 'number', filterable: true, sortable: true, searchable: false },
      { field: 'message', type: 'string', filterable: true, sortable: false, searchable: true },
      { field: 'timestamp', type: 'dateTime', filterable: true, sortable: true, searchable: false }
    ],
    rows: [{
      cells: {
        'alarm.id': { kind: 'guid', value: '22222222-2222-2222-2222-222222222222' },
        'tag.path': { kind: 'string', value: 'Demo.Level' },
        state: { kind: 'enum', value: options.state ?? 'Active' },
        priority: { kind: 'number', value: '800' },
        message: { kind: 'string', value: 'High level' },
        timestamp: { kind: 'dateTime', value: '2026-08-30T01:05:00Z' }
      }
    }],
    fromUtc: '2026-08-30T00:00:00Z',
    toUtc: '2026-08-30T01:10:00Z',
    nextCursor: options.nextCursor ?? null,
    pageSize: 100
  };
}

async function openHarness(page: Page) {
  await page.goto(harnessPath);
  await expect(page.getByTestId('historical-data-browser-runtime')).toBeVisible();
}

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body)
  });
}

test('mounted Historical Browser queries historian data and preserves exact Int64 text', async ({ page }) => {
  let capturedRequest: any = null;
  await page.route('**/api/historical/query', async route => {
    capturedRequest = route.request().postDataJSON();
    await fulfillJson(route, historicalResponse('historian.samples'));
  });

  await openHarness(page);
  await page.getByRole('button', { name: 'Query' }).click();

  await expect(page.getByRole('cell', { name: '9223372036854775807' })).toBeVisible();
  expect(capturedRequest).toMatchObject({
    version: 1,
    datasetKey: 'historian.samples',
    timeRange: { kind: 'relative', durationSeconds: 3600, anchor: 'now' },
    page: { limit: 100 }
  });
  await expect(page.getByLabel('Historical search')).toBeEnabled();
  await expect(page.getByLabel('Historical sort field')).toContainText('timestamp');
  await expect(page.getByText('2026-08-30T00:00:00Z → 2026-08-30T01:00:00Z')).toBeVisible();
});

test('mounted Historical Browser alarm history remains read-only with no operational commands', async ({ page }) => {
  await page.route('**/api/historical/query', async route => {
    const request = route.request().postDataJSON();
    expect(request.datasetKey).toBe('alarm.events');
    await fulfillJson(route, historicalResponse('alarm.events'));
  });

  await openHarness(page);
  await page.getByLabel('Historical dataset').selectOption('alarm.events');
  await page.getByRole('button', { name: 'Query' }).click();
  await expect(page.getByRole('cell', { name: 'High level' })).toBeVisible();

  await page.getByRole('row', { name: /Demo.Level Active 800 High level/ }).click();
  await expect(page.getByTestId('historical-row-detail')).toContainText('Read-only context');
  await expect(page.getByTestId('historical-row-detail')).toContainText('High level');
  await expect(page.getByRole('button', { name: /ack|shelve|reset|command/i })).toHaveCount(0);
});

test('mounted Historical Browser transports opaque cursor unchanged for next-page query', async ({ page }) => {
  const requests: any[] = [];
  await page.route('**/api/historical/query', async route => {
    const request = route.request().postDataJSON();
    requests.push(request);
    if (requests.length === 1) {
      await fulfillJson(route, historicalResponse('historian.samples', { value: '1', nextCursor: 'opaque-page-2' }));
      return;
    }
    await fulfillJson(route, historicalResponse('historian.samples', { value: '2', nextCursor: null }));
  });

  await openHarness(page);
  await page.getByRole('button', { name: 'Query' }).click();
  await expect(page.getByRole('cell', { name: '1', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Next page' }).click();

  await expect(page.getByRole('cell', { name: '2', exact: true })).toBeVisible();
  await expect(page.getByText('Page 2', { exact: true })).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[1].page.cursor).toBe('opaque-page-2');
});

test('mounted Historical Browser exposes forbidden state without fabricating empty data', async ({ page }) => {
  await page.route('**/api/historical/query', async route => {
    await fulfillJson(route, { code: 'forbidden', error: 'Forbidden.' }, 403);
  });

  await openHarness(page);
  await page.getByRole('button', { name: 'Query' }).click();

  await expect(page.getByRole('alert')).toContainText('Not authorized to query this historical dataset.');
  await expect(page.locator('tbody tr')).toHaveCount(0);
});
