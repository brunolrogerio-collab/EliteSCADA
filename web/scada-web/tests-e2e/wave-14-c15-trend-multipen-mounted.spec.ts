import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';

const harnessPath = '/tests-e2e/trend-visual-harness.html';
const tagOne = '11111111-1111-1111-1111-111111111111';
const tagTwo = '22222222-2222-2222-2222-222222222222';

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

function row(tagId: string, tagPath: string, timestamp: string, value: number, quality: string) {
  return {
    cells: {
      'tag.id': { kind: 'guid', value: tagId },
      'tag.path': { kind: 'string', value: tagPath },
      timestamp: { kind: 'dateTime', value: timestamp },
      value: { kind: 'number', value: String(value) },
      quality: { kind: 'enum', value: quality }
    }
  };
}

function historyResponse(rows: unknown[]) {
  const to = Date.now();
  return {
    version: 1,
    datasetKey: 'historian.samples',
    columns: [],
    rows,
    fromUtc: new Date(to - 3600_000).toISOString(),
    toUtc: new Date(to).toISOString(),
    nextCursor: null,
    pageSize: 200
  };
}

async function openHarness(page: Page, query = '') {
  await page.goto(`${harnessPath}${query}`);
  await expect(page.getByTestId('visual-trend').first()).toBeVisible();
}

test('C15 mounted canonical Trend renders multiple historian Pens through one protected query', async ({ page }) => {
  const requests: any[] = [];
  const now = Date.now();
  await page.route('**/api/historical/query', async route => {
    requests.push(route.request().postDataJSON());
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 10_000).toISOString(), 7.2, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 7.3, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), 20.5, 'Good')
    ]));
  });

  await openHarness(page, '?locale=en');
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-source', 'historian');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  await expect(page.getByTestId('visual-trend-legend')).toContainText('7.3 bar');
  await expect(page.getByTestId('visual-trend-legend')).toContainText('20.5 m3/h');
  await expect.poll(() => requests.length).toBeGreaterThanOrEqual(1);
  expect(requests[0]).toMatchObject({
    version: 1,
    datasetKey: 'historian.samples',
    timeRange: { kind: 'relative', durationSeconds: 3600, anchor: 'now' },
    filters: [{ field: 'tag.id', operator: 'in' }]
  });
  expect(requests[0].filters[0].values.map((item: any) => item.value)).toEqual([tagOne, tagTwo]);
});

test('C15 quality gaps are fail-safe even when quality chrome is hidden', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/query', async route => {
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 15_000).toISOString(), 10, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 10_000).toISOString(), 90, 'Uncertain'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 11, 'Good')
    ]));
  });

  await openHarness(page, '?locale=en&quality=off');
  const pressurePath = page.locator('path[data-testid="visual-trend-series"]').first();
  await expect(pressurePath).toHaveCount(1);
  const d = await pressurePath.getAttribute('d');
  expect(d).toBeTruthy();
  expect((d!.match(/M/g) ?? []).length).toBe(2);
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-quality-policy', 'good-only');
  await expect(page.getByTestId('visual-trend-legend')).not.toContainText('Uncertain');
});

test('C15 mounted Trend localizes no-data state in Spanish', async ({ page }) => {
  await page.route('**/api/historical/query', async route => {
    await fulfillJson(route, historyResponse([]));
  });

  await openHarness(page, '?locale=es');
  await expect(page.getByTestId('visual-trend-empty')).toHaveText('Sin datos');
});

test('C15 keeps two mounted Trend instances independent', async ({ page }) => {
  const requests: any[] = [];
  const now = Date.now();
  await page.route('**/api/historical/query', async route => {
    requests.push(route.request().postDataJSON());
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 7.1, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), 19.8, 'Good')
    ]));
  });

  await openHarness(page, '?locale=en&count=2');
  await expect(page.getByTestId('visual-trend')).toHaveCount(2);
  await expect(page.getByTestId('visual-trend').nth(0)).toHaveAttribute('data-trend-pen-count', '2');
  await expect(page.getByTestId('visual-trend').nth(1)).toHaveAttribute('data-trend-pen-count', '1');
  await expect(page.getByTestId('visual-trend-legend').nth(0)).toContainText('Pressure');
  await expect(page.getByTestId('visual-trend-legend').nth(1)).toContainText('Flow secondary');
  await expect.poll(() => requests.length).toBeGreaterThanOrEqual(2);
});

test('C15 live mode consumes canonical runtime TAG snapshot and WebSocket updates without historian polling', async ({ page }) => {
  const now = Date.now();
  let historicalRequests = 0;
  let socketRoute: WebSocketRoute | null = null;

  await page.route('**/api/historical/query', async route => {
    historicalRequests += 1;
    await fulfillJson(route, historyResponse([]));
  });
  await page.route('**/api/tags', async route => {
    await fulfillJson(route, [
      {
        id: tagOne,
        name: 'Pressure',
        path: 'Area/Pump/Pressure',
        dataType: 'double',
        engineeringUnit: 'bar',
        readOnly: true,
        current: {
          tagId: tagOne,
          value: 7.2,
          timestamp: new Date(now - 1000).toISOString(),
          sourceTimestamp: new Date(now - 1000).toISOString(),
          quality: 'Good'
        }
      },
      {
        id: tagTwo,
        name: 'Flow',
        path: 'Area/Pump/Flow',
        dataType: 'double',
        engineeringUnit: 'm3/h',
        readOnly: true,
        current: {
          tagId: tagTwo,
          value: 20.1,
          timestamp: new Date(now - 1000).toISOString(),
          quality: 'Good'
        }
      }
    ]);
  });
  await page.routeWebSocket('**/ws/tags', ws => {
    socketRoute = ws;
  });

  await openHarness(page, '?mode=live&locale=en');
  const trend = page.getByTestId('visual-trend');
  await expect(trend).toHaveAttribute('data-trend-source', 'runtime-tags');
  await expect(page.getByTestId('visual-trend-legend')).toContainText('7.2 bar');
  await expect.poll(() => socketRoute !== null).toBeTruthy();

  socketRoute!.send(JSON.stringify({
    type: 'tagValueChanged',
    tag: { id: tagOne, name: 'Pressure', path: 'Area/Pump/Pressure', engineeringUnit: 'bar' },
    value: 8.4,
    quality: 'Good',
    timestamp: new Date(now + 1000).toISOString(),
    source: 'e2e'
  }));

  await expect(page.getByTestId('visual-trend-legend')).toContainText('8.4 bar');
  expect(historicalRequests).toBe(0);
});
