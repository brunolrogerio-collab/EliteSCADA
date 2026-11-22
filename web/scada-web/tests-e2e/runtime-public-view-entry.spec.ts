import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const screenId = '78200000-0000-0000-0000-000000000001';
const sessionId = '78200000-0000-0000-0000-000000000002';
const clientInstanceId = 'public-782-test-client';

const runtimeProjection = {
  mode: 'engineering',
  projectKey: 'public-view-test',
  projectName: 'Public view test',
  revision: 3,
  activatedAtUtc: '2026-10-31T12:00:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 16,
    startupScreenId: screenId,
    screens: [{
      id: screenId,
      key: 'public.overview',
      name: 'Visão pública',
      properties: { backgroundColor: '#15212b' },
      elements: [{
        id: '78200000-0000-0000-0000-000000000003',
        key: 'public-title',
        type: 'core.text',
        properties: { x: 20, y: 20, width: 320, height: 50, text: 'Visão pública liberada' }
      }]
    }],
    popups: [],
    dynamos: [],
    equipment: [],
    templates: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: [],
    runtimePresentation: { alarmsVisible: false, historyVisible: false, historicalPlaybackEnabled: false }
  }
};

test('opens only the public Runtime first and returns to login without reopening it', async ({ page }) => {
  let publicStatusRequests = 0;
  const runtimeSessionRequests: string[] = [];

  await page.context().setExtraHTTPHeaders({ Authorization: '' });
  await page.route('**/api/**', route => route.fulfill({ status: 403, json: { error: 'Forbidden' } }));
  await page.route('**/api/auth/config', route => route.fulfill({ json: {
    authenticationEnabled: true,
    localLoginEnabled: true,
    initialAdministratorRequired: false,
    initialAdministratorSetupAvailable: false,
    passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
  } }));
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401, body: '' }));
  await page.route('**/api/runtime/public/status', route => {
    publicStatusRequests++;
    return route.fulfill({ json: { enabled: true } });
  });
  await page.route('**/api/runtime/public/session', route => {
    runtimeSessionRequests.push(route.request().url());
    return route.fulfill({ status: 201, json: {
      sessionId,
      clientInstanceId,
      requestedClass: 'viewOnly',
      grantedClass: 'viewOnly'
    } });
  });
  await page.route('**/api/runtime/public/session/heartbeat', route => route.fulfill({ json: {
    sessionId,
    clientInstanceId,
    requestedClass: 'viewOnly',
    grantedClass: 'viewOnly'
  } }));
  await page.route('**/api/runtime/public/session/terminate', route => route.fulfill({ status: 204, body: '' }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: ['View'],
    workspace: []
  } }));
  await page.route('**/api/runtime/application', route => route.fulfill({ json: runtimeProjection }));
  await page.route('**/api/tags', route => route.fulfill({ json: [] }));
  await page.route('**/api/tags/current', route => route.fulfill({ json: [] }));
  await page.route('**/api/alarms', route => route.fulfill({ status: 403, json: { error: 'Forbidden' } }));
  await page.route('**/api/alarms/definitions', route => route.fulfill({ status: 403, json: { error: 'Forbidden' } }));

  await page.goto('/');

  await expect(page.getByRole('link', { name: 'Entrar' })).toBeVisible();
  await expect(page.getByText('Visão pública liberada')).toBeVisible();
  await expect(page.locator('.app-navigation')).toHaveCount(0);
  expect(runtimeSessionRequests).toHaveLength(1);
  expect(runtimeSessionRequests[0]).toMatch(/\/api\/runtime\/public\/session$/);

  await page.getByRole('link', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(/\?login=1$/);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  expect(publicStatusRequests).toBe(1);
});
