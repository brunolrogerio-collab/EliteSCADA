import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';
import { formatTrendAxisLabel, MAX_TREND_AXIS_LABEL_CHARACTERS } from '../src/engineering/visual-editor/trendAxisNumberFormat';

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

function historyResponse(rows: unknown[], request?: any) {
  const queryRange = request?.definition?.query?.timeRange;
  const to = queryRange?.kind === 'absolute' ? Date.parse(queryRange.toUtc) : Date.now();
  const from = queryRange?.kind === 'absolute'
    ? Date.parse(queryRange.fromUtc)
    : to - (queryRange?.durationSeconds ?? 3600) * 1000;
  return {
    version: 1,
    queryId: '48bba95f-2817-421e-b524-ac642ecc2eae',
    queryKey: 'runtime-trend-history-v1',
    datasetKey: 'historian.samples',
    retrievalMode: 'sampledFixedStep',
    columns: [],
    rows: rows.map(candidate => candidate && typeof candidate === 'object' && 'data' in candidate
      ? candidate
      : { data: candidate, provenance: { kind: 'measured' } }),
    fromUtc: new Date(from).toISOString(),
    toUtc: new Date(to).toISOString(),
    nextCursor: null,
    resultLimit: 200
  };
}

function gapRow(tagId: string, tagPath: string, timestamp: string) {
  return {
    data: {
      cells: {
        'tag.id': { kind: 'guid', value: tagId },
        'tag.path': { kind: 'string', value: tagPath },
        timestamp: { kind: 'dateTime', value: timestamp },
        value: { kind: 'null', value: null },
        quality: { kind: 'null', value: null }
      }
    },
    provenance: { kind: 'gap', reason: 'maximum-gap-exceeded' }
  };
}

async function openHarness(page: Page, query = '') {
  await page.goto(`${harnessPath}${query}`);
  await expect(page.getByTestId('visual-trend').first()).toBeVisible();
}

test('Trend axis labels adapt precision, locale and extreme values to seven characters', () => {
  expect(formatTrendAxisLabel(9999.99, 'pt-BR')).toBe('9999,99');
  expect(formatTrendAxisLabel(99999.9, 'pt-BR')).toBe('99999,9');
  expect(formatTrendAxisLabel(9999999, 'pt-BR')).toBe('9999999');
  expect(formatTrendAxisLabel(9999.99, 'en')).toBe('9999.99');
  expect(formatTrendAxisLabel(9999.99, 'es')).toBe('9999,99');
  expect(formatTrendAxisLabel(12345678, 'pt-BR')).toBe('1,23E7');
  expect(formatTrendAxisLabel(-12345678, 'pt-BR')).toBe('-1,23E7');
  expect(formatTrendAxisLabel(12345678, 'en')).toBe('1.23E7');
  expect(formatTrendAxisLabel(12345678, 'es')).toBe('1,23E7');

  for (const locale of ['pt-BR', 'en', 'es'] as const) {
    for (const value of [-9999999, -12345678, 12345678, Number.MAX_VALUE, -Number.MAX_VALUE, 1e-320, -1e-320]) {
      const label = formatTrendAxisLabel(value, locale);
      expect(label.length, locale + ' ' + value + ' => ' + label).toBeLessThanOrEqual(MAX_TREND_AXIS_LABEL_CHARACTERS);
    }
    expect(formatTrendAxisLabel(1e-9, locale)).not.toBe('0');
    expect(formatTrendAxisLabel(-1e-9, locale)).not.toBe('-0');
    expect(formatTrendAxisLabel(Number.POSITIVE_INFINITY, locale)).toBe('—');
    expect(formatTrendAxisLabel(Number.NaN, locale)).toBe('—');
  }
});

test('Trend axis labels remain inside the plot at narrow widths and large fonts', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    const request = route.request().postDataJSON();
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 1, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), -1, 'Good')
    ], request));
  });

  await openHarness(page, '?locale=pt-BR&controls=on&axisFontSize=32&width=320');
  const trend = page.getByTestId('visual-trend');
  const chart = page.getByTestId('visual-trend-chart');
  await chart.click({ button: 'right', position: { x: 80, y: 80 } });
  const menu = page.getByTestId('visual-trend-context-menu');
  await menu.getByTestId('trend-axis-left-minimum').fill('-9999999');
  await menu.getByTestId('trend-axis-left-maximum').fill('9999999');
  await menu.getByRole('button', { name: 'Aplicar escala' }).click();

  const labels = page.getByTestId('visual-trend-axis-label');
  await expect(labels).toHaveCount(12);
  const rendered = await labels.evaluateAll(elements => elements.map(element => {
    const label = element as SVGTextElement;
    const bounds = label.getBBox();
    return {
      text: label.textContent ?? '',
      x: bounds.x,
      right: bounds.x + bounds.width,
      side: label.getAttribute('data-axis-side')
    };
  }));
  expect(rendered.map(label => label.text)).toContain('9999999');
  expect(rendered.map(label => label.text)).toContain('-1E7');
  for (const label of rendered) {
    expect(label.text.length).toBeLessThanOrEqual(MAX_TREND_AXIS_LABEL_CHARACTERS);
    expect(label.x, label.side + ' axis label ' + label.text).toBeGreaterThanOrEqual(0);
    expect(label.right, label.side + ' axis label ' + label.text).toBeLessThanOrEqual(1000);
  }
  expect(await trend.evaluate(element => element.getBoundingClientRect().width)).toBeLessThanOrEqual(320);
});

test('C15 mounted canonical Trend renders multiple historian Pens through one bounded transient sampled query', async ({ page }) => {
  const requests: any[] = [];
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    const request = route.request().postDataJSON();
    requests.push(request);
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 10_000).toISOString(), 7.2, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 7.3, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), 20.5, 'Good')
    ], request));
  });

  await openHarness(page, '?locale=en');
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-source', 'historian');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  await expect(page.getByTestId('visual-trend-legend')).toContainText('7.3 bar');
  await expect(page.getByTestId('visual-trend-legend')).toContainText('20.5 m3/h');
  await expect.poll(() => requests.length).toBeGreaterThanOrEqual(1);
  expect(requests[0]).toMatchObject({
    definition: {
      version: 1,
      providerKey: 'historical',
      query: {
        version: 1,
        datasetKey: 'historian.samples',
        timeRange: { kind: 'relative', durationSeconds: 3600, anchor: 'now' },
        filters: [{ field: 'tag.id', operator: 'in' }]
      },
      historianRetrieval: { mode: 'sampledFixedStep' }
    },
    execution: {}
  });
  expect(requests[0].definition.query.filters[0].values.map((item: any) => item.value)).toEqual([tagOne, tagTwo]);
  expect(requests[0].definition.historianRetrieval.stepMilliseconds).toBeGreaterThan(30_000);
  expect(requests[0].definition.selectedFields).toEqual(['tag.id', 'tag.path', 'quality', 'value', 'timestamp']);
  expect(requests[0].definition.query.page.limit).toBe(200);
  expect(requests[0].definition.historianRetrieval.maximumGapMilliseconds).toBeGreaterThan(0);
});

test('Trend uses the full available width and applies configurable axis and legend font sizes', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 7.2, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), 20.5, 'Good')
    ]));
  });

  await openHarness(page, '?locale=pt-BR&controls=on&axisFontSize=24&legendFontSize=19');
  const trend = page.getByTestId('visual-trend');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  const chart = page.getByTestId('visual-trend-chart');
  const svg = chart.locator('svg');
  await expect(trend).toHaveAttribute('data-trend-axis-font-size', '24');
  await expect(trend).toHaveAttribute('data-trend-legend-font-size', '19');
  await expect(page.getByTestId('visual-trend-legend')).toHaveCSS('font-size', '19px');
  await expect(page.getByTestId('visual-trend-axis-label').first()).toHaveAttribute('font-size', '24');
  await expect(page.getByTestId('visual-trend-time-label')).toHaveCount(6);

  const axisGap = await page.evaluate(() => {
    const unit = document.querySelector('[data-testid="visual-trend-axis-unit"]') as SVGGraphicsElement | null;
    const maximum = document.querySelector('[data-testid="visual-trend-axis-label"]') as SVGGraphicsElement | null;
    if (!unit || !maximum) return null;
    const unitBox = unit.getBBox();
    const maximumBox = maximum.getBBox();
    return maximumBox.y - (unitBox.y + unitBox.height);
  });
  expect(axisGap).not.toBeNull();
  expect(axisGap!).toBeGreaterThanOrEqual(0);

  const widths = await Promise.all([
    trend.evaluate(element => element.getBoundingClientRect().width),
    chart.evaluate(element => element.getBoundingClientRect().width),
    svg.evaluate(element => element.getBoundingClientRect().width)
  ]);
  expect(widths[1]).toBeGreaterThan(widths[0] - 4);
  expect(widths[2]).toBeGreaterThan(widths[1] - 2);
});

test('Trend operator context menu provides zoom, temporary manual scales, reset and time-axis visibility', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 30 * 60_000).toISOString(), 7, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 20 * 60_000).toISOString(), 8, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 10 * 60_000).toISOString(), 9, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 20 * 60_000).toISOString(), 20, 'Good')
    ]));
  });

  await openHarness(page, '?locale=pt-BR&controls=on');
  const trend = page.getByTestId('visual-trend');
  const chart = page.getByTestId('visual-trend-chart');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  await expect(trend).toHaveAttribute('data-trend-time-axis-visible', 'true');
  await chart.click({ button: 'right', position: { x: 100, y: 80 } });
  const menu = page.getByTestId('visual-trend-context-menu');
  await expect(menu).toBeVisible();
  await menu.getByRole('button', { name: 'Ampliar' }).click();
  await expect(trend).toHaveAttribute('data-trend-zoom-factor', '1.5');
  await menu.getByTestId('trend-axis-left-minimum').fill('-10');
  await menu.getByTestId('trend-axis-left-maximum').fill('10');
  await menu.getByRole('button', { name: 'Aplicar escala' }).click();
  await expect(trend).toHaveAttribute('data-trend-left-domain', '-10,10');

  await chart.click({ button: 'right', position: { x: 100, y: 80 } });
  await page.getByTestId('visual-trend-context-menu').getByRole('button', { name: 'Usar escala configurada' }).click();
  await expect(trend).not.toHaveAttribute('data-trend-left-domain', '-10,10');

  await page.goto(`${harnessPath}?locale=pt-BR&timeAxis=off`);
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-time-axis-visible', 'false');
  await expect(page.getByTestId('visual-trend-time-label')).toHaveCount(0);
});

test('Trend reference line selects the nearest sample for every legend pen and can be switched off', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 40 * 60_000).toISOString(), 7.1, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 30 * 60_000).toISOString(), 7.5, 'Good'),
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 10 * 60_000).toISOString(), 9, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 30 * 60_000).toISOString(), 30.5, 'Good')
    ]));
  });

  await openHarness(page, '?locale=pt-BR&controls=on');
  const chart = page.getByTestId('visual-trend-chart');
  const legend = page.getByTestId('visual-trend-legend');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  await expect(legend).toContainText('9 bar');

  await chart.click({ button: 'right', position: { x: 400, y: 100 } });
  await page.getByTestId('trend-reference-line-toggle').click();
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-reference-enabled', 'true');
  await chart.hover({ position: { x: 400, y: 100 } });
  await expect(page.getByTestId('visual-trend-reference-line')).toBeVisible();
  const referenceValues = legend.getByTestId('visual-trend-reference-value');
  await expect(referenceValues).toHaveCount(2);
  await expect(referenceValues.nth(0)).toHaveAttribute('data-reference-value', '7.5');
  await expect(referenceValues.nth(1)).toHaveAttribute('data-reference-value', '30.5');
  await expect(legend).toContainText('7,5 bar');

  await chart.click({ button: 'right', position: { x: 400, y: 100 } });
  await page.getByTestId('trend-reference-line-toggle').click();
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-reference-enabled', 'false');
  await expect(page.getByTestId('visual-trend-reference-line')).toHaveCount(0);
  await expect(legend).toContainText('9 bar');
});

test('Dynamo content scales proportionally to the selected frame and stays clipped inside it', async ({ page }) => {
  await page.goto(`${harnessPath}?dynamo=on`);
  await expect(page.getByTestId('visual-dynamo-content')).toBeVisible();
  const frame = page.locator('[data-dynamo-instance-id="test-dynamo-instance"]');
  const child = page.locator('[data-object-id="test-dynamo-rect"]');
  await expect(frame).toHaveAttribute('data-dynamo-design-width', '100');
  await expect(frame).toHaveAttribute('data-dynamo-frame-width', '200');
  await expect(frame).toHaveAttribute('data-dynamo-frame-height', '150');
  await expect(child).toBeVisible();
  const geometry = await frame.evaluate((outer, childId) => {
    const inner = document.querySelector(`[data-object-id="${childId}"]`)!.getBoundingClientRect();
    const box = outer.getBoundingClientRect();
    return { x: inner.left - box.left, y: inner.top - box.top, width: inner.width, height: inner.height, overflow: getComputedStyle(outer).overflow };
  }, 'test-dynamo-rect');
  expect(geometry.x).toBeCloseTo(20, 0);
  expect(geometry.y).toBeCloseTo(30, 0);
  expect(geometry.width).toBeCloseTo(60, 0);
  expect(geometry.height).toBeCloseTo(60, 0);
  expect(geometry.overflow).toBe('hidden');
});

test('Trend time-range edits are staged and not interrupted by historian polling', async ({ page }) => {
  const requests: any[] = [];
  await page.route('**/api/historical/data-query', async route => {
    requests.push(route.request().postDataJSON());
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(Date.now() - 30_000).toISOString(), 7, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(Date.now() - 20_000).toISOString(), 20, 'Good')
    ]));
  });
  await page.goto(`${harnessPath}?controls=on`);
  const trend = page.getByTestId('visual-trend');
  const controls = page.getByTestId('historical-time-range-controls');
  await expect(page.getByTestId('visual-trend-series')).toHaveCount(2);
  const unit = controls.getByLabel('Unidade');
  await unit.focus();
  await unit.selectOption('hours');
  await expect(unit).toHaveValue('hours');
  await expect(unit).toBeEnabled();
  await controls.getByLabel('Quantidade').fill('1');
  await page.waitForTimeout(1_150);
  await expect(unit).toHaveValue('hours');
  await expect(unit).toBeEnabled();
  expect(requests).toHaveLength(1);
  await controls.getByRole('button', { name: 'Aplicar' }).click();
  await expect.poll(() => requests.length).toBe(2);
  expect(requests[1].definition.query.timeRange).toEqual({ kind: 'relative', durationSeconds: 3600, anchor: 'now' });
  await expect(trend).toHaveAttribute('data-trend-state', 'ready');
});

test('C15 quality gaps are fail-safe even when quality chrome is hidden', async ({ page }) => {
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 15_000).toISOString(), 10, 'Good'),
      gapRow(tagOne, 'Area/Pump/Pressure', new Date(now - 10_000).toISOString()),
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
  await page.route('**/api/historical/data-query', async route => {
    const request = route.request().postDataJSON();
    await fulfillJson(route, historyResponse([], request));
  });

  await openHarness(page, '?locale=es');
  await expect(page.getByTestId('visual-trend-empty')).toHaveText('Sin datos');
});

test('C15 keeps two mounted Trend instances independent', async ({ page }) => {
  const requests: any[] = [];
  const now = Date.now();
  await page.route('**/api/historical/data-query', async route => {
    const request = route.request().postDataJSON();
    requests.push(request);
    await fulfillJson(route, historyResponse([
      row(tagOne, 'Area/Pump/Pressure', new Date(now - 5_000).toISOString(), 7.1, 'Good'),
      row(tagTwo, 'Area/Pump/Flow', new Date(now - 5_000).toISOString(), 19.8, 'Good')
    ], request));
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

  await page.route('**/api/historical/data-query', async route => {
    historicalRequests += 1;
    await fulfillJson(route, historyResponse([], route.request().postDataJSON()));
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


test('R2C-P mounted canonical Trend preserves the required absolute interval and never persists operator range', async ({ browser }) => {
  const context = await browser.newContext({ timezoneId: 'America/Sao_Paulo', locale: 'pt-BR' });
  const page = await context.newPage();
  const requests: any[] = [];
  let engineeringWrites = 0;

  await page.route('**/api/engineering/**', async route => {
    engineeringWrites += 1;
    await route.abort();
  });
  await page.route('**/api/historical/data-query', async route => {
    const request = route.request().postDataJSON();
    requests.push(request);
    await fulfillJson(route, historyResponse([], request));
  });
  await page.route('**/api/tags', async route => { await fulfillJson(route, []); });
  await page.routeWebSocket('**/ws/tags', () => {});

  await openHarness(page, '?locale=pt-BR&controls=on');
  const controls = page.getByTestId('historical-time-range-controls');
  await controls.getByLabel('Período').selectOption('absolute');
  await controls.getByLabel('De', { exact: true }).fill('2026-09-27T01:00');
  await controls.getByLabel('Até', { exact: true }).fill('2026-09-28T12:00');
  await controls.getByRole('button', { name: 'Aplicar' }).click();

  const expectedAbsoluteRange = {
    kind: 'absolute',
    fromUtc: '2026-09-27T04:00:00.000Z',
    toUtc: '2026-09-28T15:00:00.000Z'
  };
  await expect.poll(() => requests.some(request =>
    request.definition.query.timeRange?.kind === expectedAbsoluteRange.kind &&
    request.definition.query.timeRange?.fromUtc === expectedAbsoluteRange.fromUtc &&
    request.definition.query.timeRange?.toUtc === expectedAbsoluteRange.toUtc
  )).toBeTruthy();
  const absolute = requests.find(request =>
    request.definition.query.timeRange?.kind === expectedAbsoluteRange.kind &&
    request.definition.query.timeRange?.fromUtc === expectedAbsoluteRange.fromUtc &&
    request.definition.query.timeRange?.toUtc === expectedAbsoluteRange.toUtc
  );
  expect(absolute.definition.query.timeRange).toEqual(expectedAbsoluteRange);
  expect(absolute.definition.query.filters[0].values.map((item: any) => item.value)).toEqual([tagOne, tagTwo]);
  expect(absolute.definition.historianRetrieval.mode).toBe('sampledFixedStep');
  expect(absolute.definition.historianRetrieval.stepMilliseconds).toBeGreaterThan(60_000);

  const countBeforeRefresh = requests.length;
  await controls.getByRole('button', { name: 'Aplicar' }).click();
  await expect.poll(() => requests.length).toBeGreaterThan(countBeforeRefresh);
  expect(requests.at(-1).definition.query.timeRange).toEqual(absolute.definition.query.timeRange);

  await controls.getByLabel('Período').selectOption('live');
  await controls.getByRole('button', { name: 'Aplicar' }).click();
  await expect(page.getByTestId('visual-trend')).toHaveAttribute('data-trend-source', 'runtime-tags');
  expect(engineeringWrites).toBe(0);
  await context.close();
});
