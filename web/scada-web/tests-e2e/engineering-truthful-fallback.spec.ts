import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const workspace = {
  projectKey: 'demo-authoritative', projectName: 'Demo Project', baseRevision: 7,
  checkedOutAtUtc: '2026-09-24T00:00:00Z', lastSavedAtUtc: '2026-09-24T00:00:00Z',
  isDirty: false, changeVersion: 1, tagCount: 0, alarmCount: 0, dataSourceCount: 0,
  templateCount: 0, equipmentCount: 0, dynamoCount: 0, screenCount: 0, popupCount: 0,
  securityRoleCount: 0, commandCount: 0, visualAssetCount: 0
};

const engineeringPackage = {
  schema: 'scada.engineering', schemaVersion: 15, exportedAt: '2026-09-24T00:00:00Z',
  tags: [], alarms: [], dataSources: [], templates: [], equipment: [], dynamos: [],
  screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
};

async function routeSnapshot(page: Page, mode: 'transport' | 'http' | 'success') {
  await page.route('**/api/engineering/workspace', route => {
    if (mode === 'transport') return route.abort('failed');
    if (mode === 'http') return route.fulfill({ status: 503, body: 'maintenance' });
    return route.fulfill({ json: workspace });
  });
  await page.route('**/api/engineering/export/json', route => {
    if (mode === 'transport') return route.abort('failed');
    if (mode === 'http') return route.fulfill({ status: 503, body: 'maintenance' });
    return route.fulfill({ json: engineeringPackage });
  });
}

test('Engineering fallback is neutral for transport failure and does not synthesize a project', async ({ page }) => {
  await routeSnapshot(page, 'transport');
  await page.goto('/engineering');

  await expect(page.getByTestId('engineering-load-error')).toContainText('Transport unavailable while loading');
  await expect(page.getByTestId('engineering-project-identity')).toHaveText('Modelo indisponível');
  await expect(page.getByTestId('engineering-workspace-bar')).toContainText('Modelo indisponível');
  await expect(page.getByRole('button', { name: /TAGs/ })).toBeDisabled();
  await expect(page.getByText('Demo Project', { exact: true })).toHaveCount(0);
});

test('Engineering exposes actual HTTP status and accepts a real Demo Project after retry', async ({ page }) => {
  await routeSnapshot(page, 'http');
  await page.goto('/engineering');

  await expect(page.getByTestId('engineering-load-error')).toContainText('HTTP 503 response while loading');
  await expect(page.getByTestId('engineering-project-identity')).toHaveText('Modelo indisponível');

  await page.unroute('**/api/engineering/workspace');
  await page.unroute('**/api/engineering/export/json');
  await routeSnapshot(page, 'success');
  await page.getByRole('button', { name: 'Tentar novamente' }).click();

  await expect(page.getByTestId('engineering-project-identity')).toHaveText('Demo Project');
  await expect(page.getByRole('button', { name: /TAGs/ })).toBeEnabled();
  await expect(page.getByTestId('engineering-workspace-bar')).toContainText('Sem alterações');
});
