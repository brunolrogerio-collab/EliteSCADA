import { expect, test, type Locator, type Page, type TestInfo } from '@playwright/test';

/* DEV-863: browser-computed chrome colors, not authored HMI/Runtime content. */
test.use({ locale: 'pt-BR' });
const sourceId = 'e4827754-9884-4751-9ac1-4073bb11d7e7';
const locationId = '099c2645-9233-421a-9fd9-1e3b9e3f4d01';

async function installApi(page: Page, opts: { locked?: boolean; empty?: boolean } = {}) {
  const empty = Boolean(opts.empty);
  const packageView = {
    schema: 'scada.engineering', schemaVersion: 23, exportedAt: '2026-11-06T00:00:00Z',
    tags: [], alarms: [], templates: [], commands: [], dynamos: [], screens: [],
    popups: [], securityRoles: [], gateways: [], visualAssets: [],
    dataSources: empty ? [] : [{
      id: sourceId, key: 'ha-main', name: 'Home Assistant', driver: 'home-assistant',
      enabled: true, settings: { host: 'ha.local', port: '8123' }, secretReferences: {}
    }],
    locations: empty ? [] : [{ id: locationId, name: 'Apartment', kind: 'Building' }],
    equipment: empty ? [] : [{
      id: '29a5a3b2-4a58-47d5-b95c-96552906714d',
      path: 'Home/Shared/Light', name: 'Shared Light', locationId,
      sourceBindings: [{ dataSourceId: sourceId, stableDeviceIdentity: 'ha-device-42', role: 'primary' }],
      capabilities: []
    }]
  };
  const workspace = {
    projectKey: 'dev-863-contrast', projectName: 'DEV-863 Theme', baseRevision: 1,
    checkedOutAtUtc: '2026-11-06T00:00:00Z', lastSavedAtUtc: '2026-11-06T00:00:00Z',
    isDirty: false, changeVersion: 1, tagCount: 0, alarmCount: 0,
    dataSourceCount: packageView.dataSources.length, templateCount: 0,
    equipmentCount: packageView.equipment.length, dynamoCount: 0,
    screenCount: 0, popupCount: 0, securityRoleCount: 0, commandCount: 0,
    visualAssetCount: 0
  };
  const catalog = { dataSourceTypes: empty ? [] : [{
    typeKey: 'home-assistant', displayName: 'Home Assistant local bridge',
    kind: 'communicationDriver', description: 'Local building integration',
    integrationDomains: ['building'], connectionModel: 'directNetwork',
    capabilities: {
      supportsConnectionTest: true, supportsDiscovery: true, supportsBrowse: false,
      supportsFileImport: false, supportsReconcile: false, supportsSharedTransportInfrastructure: false
    },
    configurationSchema: {
      schemaId: 'elitescada.driver.home-assistant', schemaVersion: 1,
      dataSourceFields: [], tagBindingFields: []
    },
    tagBindingSchemaId: 'elitescada.driver.home-assistant', tagBindingSchemaVersion: 1,
    externalDependencies: [{ kind: 'builtIn', requirement: null, description: null }]
  }] };
  await page.route('**/api/**', route => route.fulfill({ json: {} }));
  await page.route('**/api/product/info', route => route.fulfill({
    json: { displayVersion: 'DEV-863', buildCommit: 'fixture' }
  }));
  await page.route('**/api/auth/config', route => route.fulfill({ json: { authenticationEnabled: true } }));
  await page.route('**/api/auth/me', route => route.fulfill({
    json: { subjectId: 'dev-863', username: 'dev-863', displayName: 'DEV-863',
      roles: ['developer'], identityProvider: 'local' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: true, runtime: ['View', 'SystemAdmin'],
      workspace: ['EngineeringView', 'EngineeringModify', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/lock/status', route => route.fulfill({
    json: { configured: Boolean(opts.locked), locked: Boolean(opts.locked) }
  }));
  await page.route('**/api/engineering/workspace', route => route.fulfill({ json: workspace }));
  await page.route('**/api/engineering/export/json', route => route.fulfill({ json: packageView }));
  await page.route('**/api/engineering/data-source-types', route => route.fulfill({ json: catalog }));
}

function luminance(value: string): number {
  const match = /^rgba?\(([^)]+)\)$/.exec(value);
  if (!match) throw new Error('Not a computed RGB color: ' + value);
  const linear = match[1].split(',').slice(0, 3).map(Number).map(channel => {
    const v = channel / 255;
    return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2];
}

async function readable(locator: Locator, minimum = 4.5) {
  const pair = await locator.evaluate(element => {
    const foreground = getComputedStyle(element).color;
    let background = '';
    for (let node: Element | null = element; node; node = node.parentElement) {
      const candidate = getComputedStyle(node).backgroundColor;
      if (candidate && candidate !== 'transparent' &&
          !/rgba\([^)]*,\s*0\s*\)/.test(candidate)) {
        background = candidate;
        break;
      }
    }
    return { foreground, background };
  });
  expect(pair.background, 'computed background').not.toBe('');
  const a = luminance(pair.foreground);
  const b = luminance(pair.background);
  expect((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05),
    'foreground/background ' + JSON.stringify(pair)).toBeGreaterThanOrEqual(minimum);
}

async function switchTheme(page: Page, theme: 'dark' | 'light') {
  await page.locator('.app-theme-control select').first().selectOption(theme);
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', theme);
}

async function evidence(page: Page, info: TestInfo, name: string) {
  await info.attach(name, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
}

test('DEV-863 populated Integrations and Locations: theme switch without reload, navigation and persistence', async ({ page }, info) => {
  await installApi(page);
  await page.goto('/engineering/homeBuilding');
  await switchTheme(page, 'dark');
  const home = page.getByTestId('home-building-workspace');
  await expect(home.getByTestId('home-building-driver-card')).toHaveCount(1);
  for (const theme of ['dark', 'light'] as const) {
    await switchTheme(page, theme);
    await readable(home.locator('.home-building__tabs button span').first());
    await readable(home.locator('.home-building__driver-card-heading strong').first());
    await readable(home.locator('.home-building__driver-card p').first());
    await home.getByTestId('home-building-tab-locations').click();
    await readable(home.getByTestId('home-building-location-name'));
    await readable(home.getByTestId('home-building-location-kind'));
    await readable(home.locator('.home-building__row').first());
    await home.getByTestId('home-building-tab-devices').click();
    await readable(home.getByTestId('home-building-device-source'));
    await readable(home.getByTestId('home-building-device-integration-filter'));
    await readable(home.getByTestId('home-building-device-location-filter'));
    await readable(home.locator('.home-building__registered-location-group h4').first());
    await readable(home.locator('.home-building__registered-device strong').first());
    await evidence(page, info, 'integrations-populated-' + theme);
    await home.getByTestId('home-building-tab-integrations').click();
  }
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await readable(page.locator('.home-building__tabs button span').first());
});

test('DEV-863 empty Integrations: disabled actions, selectors and mobile contrast', async ({ page }, info) => {
  await installApi(page, { empty: true });
  await page.goto('/engineering/homeBuilding');
  const home = page.getByTestId('home-building-workspace');
  await expect(home).toBeVisible();
  for (const theme of ['light', 'dark'] as const) {
    await switchTheme(page, theme);
    await readable(home.locator('.home-building__tabs button span').first());
    await home.getByTestId('home-building-tab-devices').click();
    await expect(home.getByTestId('home-building-discover')).toBeDisabled();
    await readable(home.getByTestId('home-building-discover'), 3);
    await readable(home.getByTestId('home-building-device-source'));
    await readable(home.getByTestId('home-building-device-integration-filter'));
    await readable(home.getByTestId('home-building-device-location-filter'));
    await readable(home.locator('.home-building__callout strong').first());
    await page.setViewportSize({ width: 390, height: 844 });
    await readable(home.getByTestId('home-building-device-source'));
    await evidence(page, info, 'integrations-empty-mobile-' + theme);
    await page.setViewportSize({ width: 1280, height: 800 });
    await home.getByTestId('home-building-tab-locations').click();
    await expect(home.getByTestId('home-building-location-preview')).toBeDisabled();
    await readable(home.getByTestId('home-building-location-preview'), 3);
    await home.getByTestId('home-building-tab-integrations').click();
  }
});

test('DEV-863 restricted Engineering Lock: both themes, disabled unlock and admin panels', async ({ page }, info) => {
  let protectedRequests = 0;
  // Use the existing authenticated E2E API for capability and page mounting;
  // mock only the Engineering Lock state, as in the established lock suite.
  await page.route('**/api/engineering/lock/status', route => route.fulfill({
    json: { configured: true, locked: true }
  }));
  await page.route('**/api/engineering/workspace', route => {
    protectedRequests++;
    return route.fulfill({ status: 403, json: { error: 'locked' } });
  });
  await page.goto('/engineering');
  const locked = page.getByTestId('engineering-lock-restricted');
  await expect(locked).toBeVisible();
  await expect(page.locator('.eng-shell')).toHaveCount(0);
  await expect(locked.locator('.eng-lock-restricted__header h1')).toBeVisible();
  for (const theme of ['light', 'dark'] as const) {
    await switchTheme(page, theme);
    await readable(locked.locator('.eng-lock-restricted__header h1'));
    await readable(locked.locator('.eng-lock-restricted__header p'));
    await readable(locked.locator('.eng-lock-restricted__header-actions > a').first());
    await readable(locked.locator('.eng-lock-card strong').first());
    await readable(locked.locator('.eng-lock-card p'));
    await readable(locked.locator('.eng-lock-boundary h2'));
    await readable(locked.locator('.eng-lock-boundary p'));
    await readable(locked.getByTestId('engineering-lock-unlock-secret'));
    const unlockButton = locked.locator('.eng-lock-card--unlock form button[type="submit"]');
    await expect(unlockButton).toBeDisabled();
    await readable(unlockButton, 3);
    await readable(locked.locator('.eng-project-management__card').first());
    await evidence(page, info, 'engineering-lock-' + theme);
  }
  await switchTheme(page, 'light');
  await page.setViewportSize({ width: 390, height: 844 });
  await readable(locked.locator('.eng-lock-restricted__header h1'));
  await readable(locked.getByTestId('engineering-lock-unlock-secret'));
  await evidence(page, info, 'engineering-lock-light-mobile');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await expect(page.getByTestId('engineering-lock-restricted')).toBeVisible();
  expect(protectedRequests).toBe(0);
});


test('DEV-863 Engineering Lock management popover remains readable unlocked across themes', async ({ page }, info) => {
  await page.route('**/api/engineering/lock/status', route => route.fulfill({
    json: { configured: true, locked: false }
  }));
  await page.goto('/engineering');
  const management = page.getByTestId('engineering-lock-management');
  await expect(management).toBeVisible();
  await management.locator('summary').click();
  await expect(management).toHaveAttribute('open', '');
  for (const theme of ['light', 'dark'] as const) {
    await switchTheme(page, theme);
    await readable(management.locator('.eng-lock-management__state'));
    await readable(management.locator('.eng-lock-management__heading strong'));
    await readable(management.locator('.eng-lock-management__heading span'));
    await readable(management.getByTestId('engineering-lock-configure-secret'));
    await readable(management.locator('.eng-lock-management__body > a'));
    await expect(management.locator('.eng-lock-management__configure button').first()).toBeDisabled();
    await readable(management.locator('.eng-lock-management__configure button').first(), 3);
    await evidence(page, info, 'engineering-lock-unlocked-' + theme);
  }
});
