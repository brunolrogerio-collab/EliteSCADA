import { expect, test, type Locator } from '@playwright/test';

test.use({ locale: 'pt-BR' });

async function expectCssToken(locator: Locator, property: string, token: string) {
  const colors = await locator.evaluate((element, args) => {
    const tokenValue = getComputedStyle(document.documentElement).getPropertyValue(args.token).trim();
    const probe = document.createElement('span');
    probe.style.color = tokenValue;
    document.body.append(probe);
    const expected = getComputedStyle(probe).color;
    probe.remove();
    return {
      actual: getComputedStyle(element).getPropertyValue(args.property),
      expected
    };
  }, { property, token });
  expect(colors.actual).toBe(colors.expected);
}

test('primary shell keeps authorized application navigation coherent without Engineering chrome inside Runtime', async ({ page }) => {
  await page.route('**/api/product/info', route => route.fulfill({
    json: {
      productName: 'TestSCADA',
      channel: 'Alpha',
      version: '9.9.9',
      displayVersion: 'TestSCADA Alpha 9.9.9',
      informationalVersion: 'TestSCADA Alpha 9.9.9+abcdef123456',
      buildCommit: 'abcdef123456'
    }
  }));
  await page.goto('/');

  let navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
  await expect(
    page.getByTestId('runtime-engineering-application')
      .or(page.getByTestId('runtime-neutral'))
  ).toBeVisible();
  await expect(page.locator('.eng-shell')).toHaveCount(0);
  await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);

  const theme = page.getByRole('combobox', { name: 'Tema' });
  await expect(theme).toBeVisible();
  await theme.selectOption('light');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');

  const runtimeViews = page.getByRole('navigation', { name: 'Runtime views' });
  if (await runtimeViews.count()) {
    await runtimeViews.getByRole('link', { name: 'Histórico' }).click();
    await expect(page).toHaveURL(/\/runtime\/history$/);
    await expect(page.getByTestId('historical-data-browser-runtime')).toBeVisible();
    await expectCssToken(page.locator('.runtime-history-page'), 'background-color', '--app-bg');
  }

  await page.goto('/engineering');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Engineering/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByText(/Gerenciamento do projeto|Project Management/, { exact: true })).toBeVisible();
  await expectCssToken(page.locator('.eng-shell'), 'background-color', '--app-bg');
  await expect(page.getByTestId('engineering-context-row')).toBeVisible();
  await expect(page.getByTestId('engineering-workspace-state')).toBeVisible();
  await expect(page.getByTestId('engineering-workspace-bar')).toHaveCount(0);

  const engineeringNavigation = page.locator('.eng-nav');
  await engineeringNavigation.getByRole('button', { name: /Informações|Information|Información/ }).click();
  await expect(page.getByTestId('engineering-information')).toBeVisible();
  await expect(page.getByTestId('engineering-product-version')).toHaveText('TestSCADA Alpha 9.9.9');
  const technicalDetails = page.locator('details.eng-information__technical');
  await expect(technicalDetails).not.toHaveAttribute('open', '');
  await technicalDetails.locator('summary').click();
  await expect(technicalDetails).toContainText(/scada\.engineering/i);
  await expect(technicalDetails).toContainText('abcdef123456');

  await engineeringNavigation.getByRole('button', { name: /Diagnósticos|Diagnostics/ }).click();
  await expect(page.getByText(/TAG Monitor/i)).toBeVisible();

  await page.goto('/audit');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('.audit-panel').first()).toBeVisible();
  await expectCssToken(page.locator('.audit-panel').first(), 'background-color', '--app-surface');

  await page.goto('/licensing');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Licenciamento/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('heading', { name: 'Licenciamento' })).toBeVisible();
  await expect(page.locator('.licensing-card').first()).toBeVisible();
  await expectCssToken(page.locator('.licensing-card').first(), 'background-color', '--app-surface');
});

test('shell uses shared locale, updates live from Engineering selector, and preserves personal theme independently', async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.engineering.locale', 'en');
    window.localStorage.setItem('elitescada.app.theme', 'dark');
  });
  await page.goto('/engineering');

  const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Audit/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licensing/ })).toBeVisible();
  await expect(page.getByText('Industrial platform', { exact: true })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'dark');
  await expect(page.getByRole('combobox', { name: 'Theme' })).toHaveValue('dark');
  await expect(page.getByText('Project Management', { exact: true })).toBeVisible();
  await expect(page.getByTestId('engineering-context-row')).toBeVisible();
  await expect(page.getByTestId('engineering-workspace-state')).toBeVisible();

  const locale = page.locator('#engineering-locale');
  await locale.selectOption('es');
  await expect(page.getByText('Plataforma industrial', { exact: true })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Auditoría/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licenciamiento/ })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tema' })).toHaveValue('dark');

  await locale.selectOption('pt-BR');
  await expect(page.getByText('Plataforma industrial', { exact: true })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licenciamento/ })).toBeVisible();
});

test('Engineering visual workspace can reclaim constrained viewport without losing panels', async ({ page }) => {
  await page.setViewportSize({ width: 1024, height: 720 });
  await page.goto('/engineering');

  const engineeringNavigation = page.locator('.eng-nav');
  await engineeringNavigation.getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
  await expect(page.locator('.eng-workspace')).toHaveAttribute('data-section-layout', 'wide');
  await expect(page.locator('.eng-workspace')).toHaveClass(/eng-workspace--wide-section/);

  const canvas = page.locator('.visual-editor-canvas-slot');
  const screens = page.locator('.visual-editor-screens');
  const palette = page.locator('.visual-editor-palette-slot');
  const properties = page.locator('.visual-editor-inspector-slot');
  const initialCanvas = await canvas.boundingBox();
  expect(initialCanvas).not.toBeNull();

  const navigationToggle = page.getByTestId('engineering-navigation-toggle');
  const screensToggle = page.getByTestId('visual-editor-screens-toggle');
  const paletteToggle = page.getByTestId('visual-editor-palette-toggle');
  const propertiesToggle = page.getByTestId('visual-editor-properties-toggle');

  const initialNavigation = await page.locator('.eng-sidebar').boundingBox();
  const initialScreens = await screens.boundingBox();
  const initialPalette = await palette.boundingBox();
  const initialProperties = await properties.boundingBox();
  expect(initialNavigation && initialScreens && initialPalette && initialProperties).toBeTruthy();

  await navigationToggle.click();
  await expect(navigationToggle).toHaveAttribute('aria-expanded', 'false');
  const collapsedNavigation = await page.locator('.eng-sidebar').boundingBox();
  expect(collapsedNavigation).not.toBeNull();
  expect(collapsedNavigation!.width).toBeLessThan(initialNavigation!.width);

  await screensToggle.click();
  await expect(screensToggle).toHaveAttribute('aria-expanded', 'false');
  const collapsedScreens = await screens.boundingBox();
  expect(collapsedScreens).not.toBeNull();
  expect(collapsedScreens!.width).toBeLessThan(initialScreens!.width);

  await paletteToggle.click();
  await expect(paletteToggle).toHaveAttribute('aria-expanded', 'false');
  const collapsedPalette = await palette.boundingBox();
  expect(collapsedPalette).not.toBeNull();
  expect(collapsedPalette!.width).toBeLessThan(initialPalette!.width);

  const reclaimedCanvas = await canvas.boundingBox();
  expect(reclaimedCanvas).not.toBeNull();
  expect(reclaimedCanvas!.width).toBeGreaterThan(initialCanvas!.width);

  await propertiesToggle.click();
  await expect(propertiesToggle).toHaveAttribute('aria-expanded', 'false');
  const collapsedProperties = await properties.boundingBox();
  expect(collapsedProperties).not.toBeNull();
  expect(collapsedProperties!.width).toBeLessThan(initialProperties!.width);
  await propertiesToggle.click();
  await expect(propertiesToggle).toHaveAttribute('aria-expanded', 'true');

  await paletteToggle.click();
  await screensToggle.click();
  await navigationToggle.click();
  await expect(navigationToggle).toHaveAttribute('aria-expanded', 'true');
  await expect(screensToggle).toHaveAttribute('aria-expanded', 'true');
  await expect(paletteToggle).toHaveAttribute('aria-expanded', 'true');
  await expect(propertiesToggle).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('.eng-sidebar')).toBeVisible();
  await expect(screens).toBeVisible();
  await expect(palette).toBeVisible();
  await expect(properties).toBeVisible();

  await expect.poll(() => page.locator('.visual-editor-screen-list').evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
  await expect.poll(() => palette.evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
  await expect.poll(() => properties.evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
});

test('Engineering keeps independent desktop scroll regions and intentional compact document flow', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.goto('/engineering');
  await expect(page.locator('.eng-shell')).toBeVisible();
  await expect.poll(() => page.evaluate(() => {
    const shell = document.querySelector('.eng-shell');
    const body = document.querySelector('.eng-body');
    const sidebar = document.querySelector('.eng-sidebar');
    const workspace = document.querySelector('.eng-workspace');
    if (!shell || !body || !sidebar || !workspace) return null;
    const style = (element: Element) => getComputedStyle(element);
    return { shellHeight: style(shell).height, viewportHeight: `${window.innerHeight}px`, bodyOverflow: style(body).overflowY, sidebarOverflow: style(sidebar).overflowY, workspaceOverflow: style(workspace).overflowY };
  })).toEqual({ shellHeight: '720px', viewportHeight: '720px', bodyOverflow: 'hidden', sidebarOverflow: 'auto', workspaceOverflow: 'auto' });

  await page.setViewportSize({ width: 700, height: 720 });
  await page.reload();
  await expect.poll(() => page.locator('.eng-shell').evaluate(element => getComputedStyle(element).overflowY)).toBe('visible');
  await expect.poll(() => page.locator('.eng-body').evaluate(element => getComputedStyle(element).display)).toBe('block');
  await expect.poll(() => page.locator('.eng-workspace').evaluate(element => getComputedStyle(element).overflowY)).toBe('visible');
});


const brandingActiveProjection = (branding: unknown, visualAssets: unknown[] = []) => ({
  mode: 'engineering',
  projectKey: 'branding-proof',
  projectName: 'Branding Proof',
  revision: 7,
  activatedAtUtc: '2026-09-29T22:00:00Z',
  package: {
    schema: 'scada.engineering', schemaVersion: 20,
    screens: [], popups: [], dynamos: [], scripts: [], scriptVisualEventReferences: [],
    visualAssets, branding
  }
});

test('Active branding mounts in shell while Working preview stays isolated, themed and responsive', async ({ page }) => {
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: brandingActiveProjection({ mode: 'text', text: 'ACTIVE BRAND', subtitle: 'Operations' })
  }));
  await page.setViewportSize({ width: 1280, height: 760 });
  await page.goto('/engineering/branding');

  const brand = page.locator('.app-brand');
  await expect(brand).toHaveAttribute('data-branding-mode', 'text');
  await expect(brand).toContainText('ACTIVE BRAND');
  await page.getByRole('combobox', { name: /Tema|Theme/ }).selectOption('light');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await expect(brand).toBeVisible();

  const editor = page.getByTestId('branding-editor');
  await expect(editor).toBeVisible();
  await editor.getByRole('combobox', { name: /Mode/i }).selectOption('text');
  await editor.getByLabel('Application text').fill('WORKING BRAND');
  await expect(page.getByTestId('branding-working-preview')).toContainText('WORKING BRAND');
  await expect(brand).toContainText('ACTIVE BRAND');
  await expect(brand).not.toContainText('WORKING BRAND');

  await page.setViewportSize({ width: 700, height: 760 });
  await expect(brand).toBeVisible();
  const box = await brand.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(700);
});

test('NONE collapses shell brand slot and invalid Active IMAGE falls back explicitly', async ({ page }) => {
  await page.route('**/api/runtime/application', route => route.fulfill({ json: brandingActiveProjection({ mode: 'none' }) }));
  await page.goto('/engineering');
  await expect(page.locator('.app-brand')).toHaveCount(0);
  await expect(page.locator('.app-bar')).toHaveClass(/app-bar--branding-none/);

  await page.unroute('**/api/runtime/application');
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: brandingActiveProjection({ mode: 'image', visualAssetId: '0a86490c-2364-4e21-8a69-5cb332f77559' })
  }));
  await page.reload();

  const brand = page.locator('.app-brand');
  await expect(brand).toHaveAttribute('data-branding-mode', 'default');
  await expect(brand).toContainText('EliteSCADA');
  await expect(brand.getByRole('status')).toContainText(/missing.*Active application/i);
});
