import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const backgroundId = '26000000-0000-0000-0000-000000000903';
const authoredLabelId = '26000000-0000-0000-0000-000000000904';
const openPopupId = '26000000-0000-0000-0000-000000000905';
const popupObjectId = '26000000-0000-0000-0000-000000000906';
const backgroundKey = 'nav-bg-eee.overview';
const authoredLabelKey = 'wet-well-liquid';
const openPopupKey = 'pump-card';
const popupKey = 'eee.popup.pump-p01';
const popupObjectKey = 'popup-internal-pump-object';

const runtimeProjection = {
  mode: 'engineering',
  projectKey: 'c26-runtime-technical-fallbacks',
  projectName: 'C26 Runtime Technical Fallbacks',
  revision: 26,
  activatedAtUtc: '2026-09-07T18:45:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 16,
    startupScreenId: '26000000-0000-0000-0000-000000000901',
    screens: [{
      id: '26000000-0000-0000-0000-000000000901',
      key: 'eee.overview',
      name: 'EEE Principal',
      properties: { backgroundColor: '#15212b' },
      elements: [
        {
          id: backgroundId,
          key: backgroundKey,
          type: 'core.rectangle',
          properties: { x: 40, y: 40, width: 420, height: 160, fillColor: '#203040' }
        },
        {
          id: authoredLabelId,
          key: authoredLabelKey,
          type: 'core.text',
          properties: {
            x: 80,
            y: 80,
            width: 300,
            height: 48,
            text: 'Nível do poço',
            textColor: '#ffffff'
          }
        },
        {
          id: openPopupId,
          key: openPopupKey,
          type: 'core.button',
          properties: { x: 80, y: 240, width: 220, height: 54, text: 'Abrir bomba' },
          actions: [{ eventKey: 'click', kind: 'openPopup', targetKey: popupKey, version: 1 }]
        }
      ]
    }],
    popups: [{
      id: '26000000-0000-0000-0000-000000000902',
      key: popupKey,
      name: 'Detalhes da bomba',
      templateKey: null,
      x: 420,
      y: 260,
      properties: { backgroundColor: '#101820' },
      elements: [{
        id: popupObjectId,
        key: popupObjectKey,
        type: 'core.rectangle',
        properties: { x: 20, y: 20, width: 260, height: 120, fillColor: '#304050' }
      }]
    }],
    dynamos: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: []
  }
} as const;

async function installRuntimeContract(page: Page) {
  await page.route('**/api/auth/config', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      localLoginEnabled: true,
      initialAdministratorRequired: false,
      initialAdministratorSetupAvailable: false,
      initialAdministratorBlockedReason: null,
      passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
    }
  }));
  await page.route('**/api/auth/me', route => route.fulfill({
    json: {
      subjectId: 'c26-technical-fallback-po-id',
      username: 'c26-technical-fallback-po',
      displayName: 'C26 Technical Fallback Product Owner',
      roles: ['developer'],
      identityProvider: 'local'
    }
  }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 'c26-technical-fallback-po' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.route('**/api/runtime/application', route => route.fulfill({ json: runtimeProjection }));
}

test('C26 Runtime keeps technical visual keys in DOM authority but never renders them as operator fallback text', async ({ page }) => {
  await installRuntimeContract(page);
  await page.goto('/');

  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect(page.getByText('Nível do poço', { exact: true })).toBeVisible();

  const background = page.locator(`[data-object-id="${backgroundId}"]`);
  await expect(background).toBeVisible();
  expect(await background.textContent()).toBe('');
  await expect(page.getByText(backgroundKey, { exact: true })).toHaveCount(0);
  await expect(page.getByText(authoredLabelKey, { exact: true })).toHaveCount(0);
  await expect(page.getByText(openPopupKey, { exact: true })).toHaveCount(0);

  await page.getByRole('button', { name: 'Abrir bomba' }).click();
  const popup = page.locator('.runtime-visual-popup').first();
  await expect(popup).toHaveAttribute('data-popup-key', popupKey);
  await expect(popup.locator('.runtime-visual-popup-header strong')).toHaveText('Detalhes da bomba');
  await expect(popup.locator('.runtime-visual-popup-header code')).toHaveCount(0);
  await expect(page.getByText(popupKey, { exact: true })).toHaveCount(0);

  const popupObject = popup.locator(`[data-object-id="${popupObjectId}"]`);
  await expect(popupObject).toBeVisible();
  expect(await popupObject.textContent()).toBe('');
  await expect(page.getByText(popupObjectKey, { exact: true })).toHaveCount(0);
});
