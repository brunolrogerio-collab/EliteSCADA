import { expect, test, type Route } from '@playwright/test';

const inspectorHarness = '/tests-e2e/trend-property-inspector-harness.html';
const trendHarness = '/tests-e2e/trend-visual-harness.html';

const localeCases = [
  {
    locale: 'pt-BR',
    modeLabel: 'Modo do Trend',
    modeOptions: ['Histórico', 'Tempo real'],
    legendLabel: 'Mostrar legenda',
    trueLabel: 'Verdadeiro',
    resetLabel: 'Usar padrão'
  },
  {
    locale: 'en',
    modeLabel: 'Trend mode',
    modeOptions: ['History', 'Live'],
    legendLabel: 'Show legend',
    trueLabel: 'True',
    resetLabel: 'Use default'
  },
  {
    locale: 'es',
    modeLabel: 'Modo del Trend',
    modeOptions: ['Histórico', 'En vivo'],
    legendLabel: 'Mostrar leyenda',
    trueLabel: 'Verdadero',
    resetLabel: 'Usar predeterminado'
  }
] as const;

async function fulfillJson(route: Route, body: unknown) {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
}

test('C15 mounted Property Inspector localizes Trend scalar chrome in pt-BR en and es', async ({ page }) => {
  for (const current of localeCases) {
    await page.goto(`${inspectorHarness}?locale=${encodeURIComponent(current.locale)}`);

    const modeField = page.locator('[data-property-key="trendMode"]');
    await expect(modeField).toContainText(current.modeLabel);
    const modeSelect = modeField.locator('select');
    await expect(modeSelect).toHaveValue('history');
    await expect(modeSelect.locator('option')).toHaveText(current.modeOptions);

    const legendField = page.locator('[data-property-key="trendLegendVisible"]');
    await expect(legendField).toContainText(current.legendLabel);
    await expect(legendField).toContainText(current.trueLabel);
    await expect(legendField.getByRole('button', { name: current.resetLabel })).toBeVisible();
  }
});

test('C15 mounted Runtime Trend exposes Portuguese no-data state', async ({ page }) => {
  await page.route('**/api/historical/data-query', async route => {
    const now = Date.now();
    await fulfillJson(route, {
      version: 1,
      queryId: '48bba95f-2817-421e-b524-ac642ecc2eae',
      queryKey: 'runtime-trend-history-v1',
      datasetKey: 'historian.samples',
      retrievalMode: 'sampledFixedStep',
      columns: [],
      rows: [],
      fromUtc: new Date(now - 3600_000).toISOString(),
      toUtc: new Date(now).toISOString(),
      nextCursor: null,
      resultLimit: 200,
      retrievalMode: 'sampledFixedStep'
    });
  });

  await page.goto(`${trendHarness}?locale=pt-BR`);
  await expect(page.getByTestId('visual-trend').first()).toBeVisible();
  await expect(page.getByTestId('visual-trend-empty').first()).toHaveText('Sem dados');
});
