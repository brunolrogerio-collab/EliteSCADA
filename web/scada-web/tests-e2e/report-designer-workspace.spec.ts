import { expect, test } from '@playwright/test';

const workspace = {
  projectKey: 'demo',
  projectName: 'Demo',
  baseRevision: 4,
  isDirty: false,
  changeVersion: 12,
  tagCount: 0,
  alarmCount: 0,
  dataSourceCount: 0,
  templateCount: 0,
  equipmentCount: 0,
  dynamoCount: 0,
  screenCount: 0,
  popupCount: 0
};

const emptyPackage = {
  schema: 'scada.engineering',
  schemaVersion: 14,
  exportedAt: '2026-08-29T20:00:00Z',
  tags: [],
  alarms: [],
  dataSources: [],
  templates: [],
  equipment: [],
  dynamos: [],
  screens: [],
  popups: [],
  securityRoles: [],
  gateways: [],
  visualAssets: [],
  reports: []
};

test('Report Designer creates, previews and applies one canonical report', async ({ page }) => {
  let packageState: typeof emptyPackage & { reports: unknown[] } = structuredClone(emptyPackage);
  let appliedPackage: any = null;
  let executionRequest: any = null;

  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
  });

  await page.route('**/api/**', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  await page.route('**/api/auth/config', async route => {
    await route.fulfill({ json: { authenticationEnabled: false, localLoginEnabled: false } });
  });
  await page.route('**/api/auth/effective-capabilities', async route => {
    await route.fulfill({
      json: {
        authenticationEnabled: false,
        runtime: ['View', 'TrendUse', 'SystemAdmin'],
        workspace: ['EngineeringModify']
      }
    });
  });
  await page.route('**/api/engineering/workspace', async route => {
    await route.fulfill({ json: workspace });
  });
  await page.route('**/api/engineering/export/json', async route => {
    await route.fulfill({ json: packageState });
  });
  await page.route('**/api/engineering/import/json/preview', async route => {
    const body = route.request().postDataJSON();
    await route.fulfill({
      json: {
        mode: 'Merge',
        createCount: Array.isArray(body.reports) ? body.reports.length : 0,
        updateCount: 0,
        skipCount: 0,
        errorCount: 0,
        items: [],
        canApply: true
      }
    });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    appliedPackage = route.request().postDataJSON();
    packageState = structuredClone(appliedPackage);
    await route.fulfill({ json: { mode: 'Merge', created: 1, updated: 0, skipped: 0, issues: [] } });
  });
  await page.route('**/api/reports/preview', async route => {
    executionRequest = route.request().postDataJSON();
    await route.fulfill({
      json: {
        reportId: null,
        reportKey: executionRequest.report.key,
        parameters: executionRequest.parameters ?? {},
        queries: [
          {
            queryKey: 'main',
            dataset: 'historian.samples',
            columns: [
              { field: 'value', type: 'scalar', filterable: true, sortable: false, searchable: false },
              { field: 'timestamp', type: 'dateTime', filterable: true, sortable: true, searchable: false }
            ],
            rows: [
              {
                cells: {
                  value: { kind: 'int64', value: '9223372036854775807' },
                  timestamp: { kind: 'dateTime', value: '2026-08-29T19:58:00.0000000+00:00' }
                }
              },
              {
                cells: {
                  value: { kind: 'double', value: '42.5' },
                  timestamp: { kind: 'dateTime', value: '2026-08-29T19:59:00.0000000+00:00' }
                }
              }
            ],
            fromUtc: '2026-08-29T19:00:00Z',
            toUtc: '2026-08-29T20:00:00Z'
          }
        ]
      }
    });
  });

  await page.goto('/engineering');
  const navigation = page.locator('.eng-nav');
  await navigation.getByRole('button', { name: /Relatórios/ }).click();

  const designer = page.getByTestId('report-designer-workspace');
  await expect(designer).toBeVisible();
  await expect(designer.getByRole('heading', { name: 'Designer de Relatórios' })).toBeVisible();

  await designer.getByLabel('Nome').fill('Pump history');
  await designer.getByRole('button', { name: /Detail/ }).click();
  await designer.getByRole('button', { name: '+ Campo', exact: true }).click();

  const pageCanvas = designer.locator('.report-page').first();
  await expect(pageCanvas).toHaveAttribute('data-unit', 'millimeter');
  await expect(pageCanvas.locator('[data-kind="dataField"]')).toHaveCount(3);

  await designer.getByRole('button', { name: 'Executar Preview' }).click();
  await expect(designer.getByTestId('report-preview-canvas')).toBeVisible();
  await expect(designer.getByTestId('report-preview-canvas')).toContainText('9223372036854775807');
  await expect(designer.getByTestId('report-preview-canvas')).toContainText('42.5');

  expect(executionRequest.report.name).toBe('Pump history');
  expect(executionRequest.report.queries[0].query.version).toBe(1);
  expect(executionRequest.report.queries[0].query.datasetKey).toBe('historian.samples');
  expect(executionRequest.parameters.periodSeconds).toEqual({ type: 'durationSeconds', value: '3600' });

  await designer.getByRole('button', { name: 'Design' }).click();
  await designer.getByRole('button', { name: 'Validar' }).click();
  await expect(designer.getByText('Validação aprovada')).toBeVisible();

  page.once('dialog', dialog => dialog.accept());
  await designer.getByRole('button', { name: 'Aplicar' }).click();
  await expect.poll(() => appliedPackage?.reports?.[0]?.name).toBe('Pump history');
  expect(appliedPackage.reports[0].sections.map((section: any) => section.kind)).toEqual([
    'reportHeader',
    'detail',
    'reportFooter'
  ]);
  expect(appliedPackage.reports[0].sections[1].controls[0].xMillimeters).toBe(0);
  expect(packageState.reports).toHaveLength(1);
});
