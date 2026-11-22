import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('Runtime header authoring limits labels and previews a selected visual asset', async ({ page }) => {
  const visualAsset = {
    id: 'header-pump-icon',
    key: 'header-pump-icon',
    name: 'Ícone da bomba',
    originalFileName: 'bomba.svg',
    mediaType: 'image/svg+xml',
    byteLength: 68,
    sha256: 'header-pump-icon-hash',
    pixelWidth: 24,
    pixelHeight: 24
  };
  const screens = [{ id: 'screen-home-id', key: 'home', name: 'Visão geral', elements: [] }];
  const packageView = {
    schema: 'scada.engineering',
    schemaVersion: 30,
    exportedAt: '2026-10-29T00:00:00Z',
    tags: [], alarms: [], dataSources: [], templates: [], equipment: [], dynamos: [],
    screens, popups: [], securityRoles: [], gateways: [], visualAssets: [visualAsset],
    startupScreenId: 'screen-home-id',
    runtimePresentation: { version: 1, header: { links: [{ label: 'Tela', screenKey: 'home' }] } }
  };

  await page.route('**/api/**', route => route.fulfill({ status: 404, contentType: 'application/json', body: '{}' }));
  await page.route('**/api/product/info', route => route.fulfill({ json: {
    productName: 'SCADIVO', channel: 'Test', version: 'test', displayVersion: 'SCADIVO Test',
    informationalVersion: 'SCADIVO Test+test', buildCommit: 'test'
  } }));
  await page.route('**/api/auth/config', route => route.fulfill({ json: {
    authenticationEnabled: false,
    localLoginEnabled: false,
    initialAdministratorRequired: false,
    initialAdministratorSetupAvailable: false
  } }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: false,
    runtime: ['View', 'TrendUse', 'SystemAdmin'],
    workspace: ['EngineeringView', 'EngineeringModify', 'UserRoleAdmin']
  } }));
  await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: { configured: false, locked: false } }));
  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: {
    projectKey: 'runtime-header-test', projectName: 'Cabeçalho de teste', baseRevision: 1,
    isDirty: false, changeVersion: 1, tagCount: 0, alarmCount: 0, dataSourceCount: 0,
    templateCount: 0, equipmentCount: 0, dynamoCount: 1, screenCount: 1, popupCount: 0,
    visualAssetCount: 1
  } }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: packageView }));
  await page.route('**/api/runtime/application', route => route.fulfill({ json: {
    mode: 'engineering', projectKey: 'runtime-header-test', projectName: 'Cabeçalho de teste', revision: 1,
    package: { ...packageView, schema: 'elitescada.hmi', schemaVersion: 1 }
  } }));
  await page.route('**/api/engineering/visual-assets/header-pump-icon/content', route => route.fulfill({
    contentType: 'image/svg+xml',
    body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" fill="#20cbbb"/></svg>'
  }));

  await page.goto('/engineering/branding');
  const settings = page.getByTestId('runtime-header-settings');
  await expect(settings).toBeVisible();
  const labelInput = settings.getByLabel('Texto do botão (até 10 caracteres)');
  await expect(labelInput).toHaveAttribute('maxlength', '10');
  await labelInput.fill('');
  await labelInput.pressSequentially('ABCDEFGHIJK');
  await expect(labelInput).toHaveValue('ABCDEFGHIJ');

  const imageSelector = settings.getByLabel('Imagem do botão');
  await expect(imageSelector.locator('option', { hasText: 'Ícone da bomba' })).toHaveCount(1);
  await imageSelector.selectOption(visualAsset.id);
  const preview = settings.getByTestId('runtime-header-asset-preview');
  await expect(preview).toContainText('Ícone da bomba');
  await expect(preview.locator('img')).toBeVisible();
});
